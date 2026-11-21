using System.Text.Json;

namespace Hardening.Reports;

// A step of the run: its name, its profile in the matrix, its outcome in the workflow, and the folder
// that holds its TRX file and the seeds folder its seeded specs write.
public sealed record StepInput(string Name, string Profile, string Outcome, string Directory);

// Joins the results of each step to the Scenario IDs whose evidence names those tests. Evidence of the
// form path#Type.Method names one test, and a path to a .cs file names every test the file declares.
// Evidence outside the projects of the run's profiles is not part of the run.
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

                var testRuns = tests.Select(test => Outcome(test, profileRuns)).ToList();
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

    private static TestRun Outcome(ExpectedTest test, IReadOnlyList<ReadRun> runs)
    {
        var results = runs
            .SelectMany(run => run.Results.Where(result => result.Is(test.Type, test.Method)).Select(result => (run.Run.Name, result.Outcome)))
            .ToList();
        var outcome = results.Count == 0 ? TestOutcome.NotRun
            : results.Any(result => result.Outcome == TestOutcome.Failed) ? TestOutcome.Failed
            : results.All(result => result.Outcome == TestOutcome.Passed) ? TestOutcome.Passed
            : TestOutcome.Skipped;
        return new TestRun
        {
            Test = test.Reference,
            Outcome = outcome,
            Step = results.Count > 0 ? string.Join(", ", results.Select(result => result.Name).Distinct()) : null,
        };
    }

    private List<ExpectedTest> ExpectedTests(Scenario scenario, Profile profile)
    {
        var tests = new List<ExpectedTest>();
        foreach (var reference in scenario.Evidence.Where(reference => reference.StartsWith(profile.Project + "/", StringComparison.Ordinal)))
        {
            var hash = reference.IndexOf('#');
            if (hash >= 0)
            {
                tests.Add(ExpectedTest.Of(reference[..hash], reference[(hash + 1)..]));
            }
            else if (reference.EndsWith(".cs", StringComparison.Ordinal))
            {
                tests.AddRange(index.Tests(reference).Select(member => ExpectedTest.Of(reference, member)));
            }
        }

        return [.. tests.DistinctBy(test => test.Reference)];
    }

    private ReadRun ReadStep(StepInput step, string os, string reportDirectory)
    {
        var profile = matrix.Profile(step.Profile) ?? throw new ArgumentException($"The matrix has no profile {step.Profile}.", nameof(step));
        var details = new List<string>();
        if (step.Outcome is not ("success" or "failure"))
        {
            details.Add($"its outcome is {step.Outcome}");
        }

        var files = Directory.Exists(step.Directory) ? Directory.EnumerateFiles(step.Directory, "*.trx").Order(StringComparer.Ordinal).ToList() : [];
        var results = new List<TrxResult>();
        if (files.Count == 0)
        {
            details.Add("it wrote no TRX file");
        }

        foreach (var file in files)
        {
            var trx = TrxFile.Read(file);
            results.AddRange(trx.Results);
            if (trx.Aborted)
            {
                details.Add($"{Path.GetFileName(file)} says its run is {trx.Outcome}");
            }
        }

        var seeds = ReadSeeds(Path.Combine(step.Directory, SeedsDirectory));
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
            Results = files.Count > 0 ? Path.GetRelativePath(reportDirectory, files[0]).Replace('\\', '/') : null,
            Passed = results.Count(result => result.Outcome == TestOutcome.Passed),
            Failed = results.Count(result => result.Outcome == TestOutcome.Failed),
            Skipped = results.Count(result => result.Outcome == TestOutcome.Skipped),
            Detail = details.Count > 0 ? string.Join("; ", details) : null,
        };
        return new ReadRun(run, profile, results, seeds);
    }

    private static List<SeedRun> ReadSeeds(string directory) =>
        Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*.json")
                .Order(StringComparer.Ordinal)
                .Select(file => JsonSerializer.Deserialize<SeedRun>(File.ReadAllText(file), ConfigurationMatrix.Options) ?? throw new JsonException($"{file} is null."))]
            : [];

    private sealed record ReadRun(StepRun Run, Profile Profile, IReadOnlyList<TrxResult> Results, IReadOnlyList<SeedRun> Seeds);

    private sealed record ExpectedTest(string Reference, string Type, string Method)
    {
        public static ExpectedTest Of(string path, string member)
        {
            var dot = member.LastIndexOf('.');
            return new ExpectedTest($"{path}#{member}", dot > 0 ? member[..dot] : member, member[(dot + 1)..]);
        }
    }
}
