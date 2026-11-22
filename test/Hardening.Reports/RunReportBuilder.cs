using System.Text.Json;

namespace Hardening.Reports;

// A step of the run: its name, its profile in the matrix, its outcome in the workflow, and the folder
// that holds its TRX files and the seeds folder its seeded specs write. A step whose folder is
// NoResults writes no TRX file, so its outcome stands for every reference of its profile.
public sealed record StepInput(string Name, string Profile, string Outcome, string Directory)
{
    public const string NoResults = "-";

    public bool HasResults => Directory != NoResults;
}

// Joins the results of each step to the Scenario IDs whose evidence names those tests. Evidence of the
// form path#Type.Method names one test, a path to a .cs file names every test the file declares, and
// any other path names a project folder, a script or a workflow, which passes when every step of its
// profile succeeded. Evidence outside the projects of the run's profiles is not part of the run.
public sealed class RunReportBuilder(string repositoryRoot, ContractInventory inventory, ConfigurationMatrix matrix)
{
    public const string SeedsDirectory = "seeds";

    private readonly TestMethodIndex index = new(repositoryRoot);

    public RunReport Build(IReadOnlyList<StepInput> steps, string commit, RunEnvironment environment, string os, string? location, string reportDirectory)
    {
        var incomplete = new List<string>();
        var runs = steps.Select(step => ReadStep(step, os, reportDirectory)).ToList();
        if (runs.Count == 0)
        {
            incomplete.Add("The run has no steps.");
        }

        incomplete.AddRange(runs.Where(run => !run.Run.Complete).Select(run => $"Step {run.Run.Name}: {run.Run.Detail}."));

        var entries = new List<ScenarioRun>();
        foreach (var profile in runs.Select(run => run.Profile).DistinctBy(profile => profile.Name))
        {
            var profileRuns = runs.Where(run => run.Profile.Name == profile.Name).ToList();
            var profileComplete = profileRuns.All(run => run.Run.Complete);
            foreach (var scenario in inventory.Scenarios)
            {
                var tests = ExpectedTests(scenario, profile);
                if (tests.Count == 0)
                {
                    continue;
                }

                var testRuns = tests.Select(test => Outcome(test, profile, profileRuns)).ToList();
                if (profileComplete)
                {
                    incomplete.AddRange(testRuns
                        .Where(test => test.Outcome == TestOutcome.NotRun)
                        .Select(test => $"{scenario.Id}: the inventory names {test.Test}, and no step of {profile.Name} ran it."));
                }

                var seeds = profileRuns
                    .SelectMany(run => run.Seeds)
                    .Where(seed => tests.Any(test => test.Method == seed.Test))
                    .DistinctBy(seed => seed.Test)
                    .ToList();
                var (status, detail) = Status(scenario, testRuns);
                entries.Add(new ScenarioRun
                {
                    Id = scenario.Id,
                    Status = status,
                    Commit = commit,
                    Configuration = matrix.Configuration(profile, os),
                    Environment = environment,
                    Seeds = seeds.Count > 0 ? seeds : null,
                    Tests = testRuns,
                    Detail = detail,
                    Location = location,
                });
            }
        }

        return new RunReport
        {
            Commit = commit,
            Created = DateTimeOffset.UtcNow,
            Location = location,
            Environment = environment,
            Complete = incomplete.Count == 0,
            Incomplete = incomplete,
            Steps = [.. runs.Select(run => run.Run)],
            Scenarios = entries,
        };
    }

    private (string Status, string? Detail) Status(Scenario scenario, IReadOnlyList<TestRun> tests)
    {
        var failed = tests.Where(test => test.Outcome == TestOutcome.Failed).ToList();
        if (failed.Count > 0)
        {
            return (ScenarioStatus.Failed, $"{failed.Count} of {tests.Count} tests failed: {string.Join(", ", failed.Select(test => test.Test))}");
        }

        var decision = inventory.Decisions.FirstOrDefault(decision => !decision.Approved && (decision.Id == scenario.Decision || decision.Scenarios.Contains(scenario.Id)));
        if (decision is not null)
        {
            return (ScenarioStatus.Blocked, $"the contract decision {decision.Id} is not approved");
        }

        var missing = tests.Where(test => test.Outcome != TestOutcome.Passed).ToList();
        return missing.Count == 0
            ? (ScenarioStatus.Passed, null)
            : (ScenarioStatus.Blocked, $"{missing.Count} of {tests.Count} tests did not pass or did not run: {string.Join(", ", missing.Select(test => $"{test.Test} ({test.Outcome})"))}");
    }

    // A test of a step with TRX files takes the results of its own assembly. A case that a test of another
    // assembly runs as a row, such as an NSubstituteComparison case, takes the row whose argument is
    // "Type.Method". A folder, a file or a test of a step without TRX files takes the step's outcome.
    private TestRun Outcome(ExpectedTest test, Profile profile, IReadOnlyList<ReadRun> runs)
    {
        var results = new List<(string Step, string Outcome)>();
        foreach (var run in runs)
        {
            if (test.Method is null || !run.HasResults)
            {
                var outcome = run.Run.Outcome == "success" && run.Run.Complete ? TestOutcome.Passed
                    : run.Run.Outcome == "failure" ? TestOutcome.Failed
                    : TestOutcome.NotRun;
                results.Add((run.Run.Name, outcome));
                continue;
            }

            var own = run.Results.Where(result => (result.Assembly is null || test.Assembly is null || result.Assembly == test.Assembly) && result.Is(test.Type!, test.Method)).ToList();
            if (own.Count == 0)
            {
                var row = $"\"{test.Type}.{test.Method}\"";
                own = [.. run.Results.Where(result => result.Name.Contains(row, StringComparison.Ordinal))];
            }

            results.AddRange(own.Select(result => (run.Run.Name, result.Outcome)));
        }

        var found = results.Where(result => result.Outcome != TestOutcome.NotRun).ToList();
        var combined = found.Count == 0 ? TestOutcome.NotRun
            : found.Any(result => result.Outcome == TestOutcome.Failed) ? TestOutcome.Failed
            : found.All(result => result.Outcome == TestOutcome.Passed) && found.Count == results.Count ? TestOutcome.Passed
            : found.All(result => result.Outcome == TestOutcome.Passed) ? TestOutcome.NotRun
            : TestOutcome.Skipped;
        return new TestRun
        {
            Test = test.Reference,
            Outcome = combined,
            Step = found.Count > 0 ? string.Join(", ", found.Select(result => result.Step).Distinct()) : null,
        };
    }

    private List<ExpectedTest> ExpectedTests(Scenario scenario, Profile profile)
    {
        var tests = new List<ExpectedTest>();
        foreach (var reference in scenario.Evidence)
        {
            if (profile.ProjectOf(reference) is not { } project)
            {
                continue;
            }

            var assembly = AssemblyOf(project);
            var hash = reference.IndexOf('#');
            if (hash >= 0)
            {
                tests.Add(ExpectedTest.Of(reference[..hash], reference[(hash + 1)..], assembly));
            }
            else if (reference.EndsWith(".cs", StringComparison.Ordinal))
            {
                tests.AddRange(index.Tests(reference).Select(member => ExpectedTest.Of(reference, member, assembly)));
            }
            else
            {
                tests.Add(new ExpectedTest(reference, null, null, null));
            }
        }

        return [.. tests.DistinctBy(test => test.Reference)];
    }

    // The assembly of the project in a folder, which is the name of its one project file.
    private string? AssemblyOf(string project)
    {
        var folder = Path.Combine(repositoryRoot, project);
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.csproj") : [];
        return files.Length == 1 ? Path.GetFileNameWithoutExtension(files[0]) : null;
    }

    private ReadRun ReadStep(StepInput step, string os, string reportDirectory)
    {
        var profile = matrix.Profile(step.Profile) ?? throw new ArgumentException($"The matrix has no profile {step.Profile}.", nameof(step));
        var details = new List<string>();
        if (step.Outcome is not ("success" or "failure"))
        {
            details.Add($"its outcome is {step.Outcome}");
        }

        var assemblies = profile.Projects.Select(AssemblyOf).OfType<string>().ToHashSet();
        var results = new List<TrxResult>();
        string? first = null;
        if (step.HasResults)
        {
            var files = Directory.Exists(step.Directory) ? Directory.EnumerateFiles(step.Directory, "*.trx").Order(StringComparer.Ordinal).ToList() : [];
            foreach (var file in files)
            {
                var trx = TrxFile.Read(file);
                // A folder may hold the TRX files of several test assemblies, as a solution run writes them.
                if (trx.Results.FirstOrDefault(result => result.Assembly is not null)?.Assembly is { } assembly && !assemblies.Contains(assembly))
                {
                    continue;
                }

                first ??= file;
                results.AddRange(trx.Results);
                if (trx.Aborted)
                {
                    details.Add($"{Path.GetFileName(file)} says its run is {trx.Outcome}");
                }
            }

            if (first is null)
            {
                details.Add("it wrote no TRX file");
            }
        }

        var seeds = step.HasResults ? ReadSeeds(Path.Combine(step.Directory, SeedsDirectory)) : [];
        details.AddRange(seeds
            .Where(seed => seed.Truncated)
            .Select(seed => seed.Case is { } single
                ? $"{seed.Test} replayed only case {single} of seed {seed.Seed}"
                : $"{seed.Test} ran {seed.Cases} of its {seed.Budget} cases"));

        var run = new StepRun
        {
            Name = step.Name,
            Profile = profile.Name,
            Configuration = matrix.Configuration(profile, os),
            Outcome = step.Outcome,
            Complete = details.Count == 0,
            Results = first is not null ? Path.GetRelativePath(reportDirectory, first).Replace('\\', '/') : null,
            Passed = results.Count(result => result.Outcome == TestOutcome.Passed),
            Failed = results.Count(result => result.Outcome == TestOutcome.Failed) + (!step.HasResults && step.Outcome == "failure" ? 1 : 0),
            Skipped = results.Count(result => result.Outcome == TestOutcome.Skipped),
            Detail = details.Count > 0 ? string.Join("; ", details) : null,
        };
        return new ReadRun(run, profile, step.HasResults, results, seeds);
    }

    private static List<SeedRun> ReadSeeds(string directory) =>
        Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*.json")
                .Order(StringComparer.Ordinal)
                .Select(file => JsonSerializer.Deserialize<SeedRun>(File.ReadAllText(file), ConfigurationMatrix.Options) ?? throw new JsonException($"{file} is null."))]
            : [];

    private sealed record ReadRun(StepRun Run, Profile Profile, bool HasResults, IReadOnlyList<TrxResult> Results, IReadOnlyList<SeedRun> Seeds);

    // Type and Method are null for a reference to a folder or a file other than a .cs file.
    private sealed record ExpectedTest(string Reference, string? Type, string? Method, string? Assembly)
    {
        public static ExpectedTest Of(string path, string member, string? assembly)
        {
            var dot = member.LastIndexOf('.');
            return new ExpectedTest($"{path}#{member}", dot > 0 ? member[..dot] : member, member[(dot + 1)..], assembly);
        }
    }
}
