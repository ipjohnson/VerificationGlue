using Hardening.Mutations;

// Usage: Hardening.Mutations [--output <folder>] [--work <folder>] [--only <id>] ...
// Copies the repository to the work folder, runs the suite there without a mutation and then with each
// mutation of docs/design/hardening-mutations.json in turn, and writes mutations.json, mutations.md and
// the output of each command to the output folder. Exits with 1 when the suite fails without a mutation,
// when a mutation cannot be applied or built, or when a result does not match the mutation's explanation.
var output = "artifacts/mutations";
string? work = null;
var only = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--output" when i + 1 < args.Length:
            output = args[++i];
            break;
        case "--work" when i + 1 < args.Length:
            work = args[++i];
            break;
        case "--only" when i + 1 < args.Length:
            only.Add(args[++i]);
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument '{args[i]}'. Usage: [--output <folder>] [--work <folder>] [--only <id>] ...");
            return 2;
    }
}

var root = RepositoryRoot();
var catalog = MutationCatalog.Load(root);
var problems = catalog.Validate(root);
if (problems.Count > 0)
{
    foreach (var problem in problems)
    {
        Console.Error.WriteLine($"{MutationCatalog.RelativePath}: {problem}");
    }

    return 1;
}

if (only.FirstOrDefault(id => catalog.Mutations.All(mutation => mutation.Id != id)) is { } unknown)
{
    Console.Error.WriteLine($"{unknown} is not a mutation in {MutationCatalog.RelativePath}.");
    return 2;
}

output = Path.GetFullPath(output);
var ownsWork = work is null;
work = Path.GetFullPath(work ?? Path.Combine(Path.GetTempPath(), "vg-mutations-" + Guid.NewGuid().ToString("N")[..8]));
var repository = Path.Combine(work, "repo");
Directory.CreateDirectory(output);
Suite.Copy(root, repository);
Console.WriteLine($"Copied the repository to {repository}.");

var suite = new Suite(repository, Path.Combine(output, "logs"));
var results = new List<MutationResult>();
IReadOnlyList<RunResult> baseline = [];
var baselineProblems = new List<string>();
if (await suite.BuildAsync("baseline") is { } baselineBuild)
{
    baselineProblems.Add($"Without a mutation, {baselineBuild}");
}
else
{
    baseline = await suite.TestAsync("baseline", Path.Combine(output, "baseline"));
    baselineProblems.AddRange(MutationResult.BaselineProblems(baseline));
}

Console.WriteLine(baselineProblems.Count == 0
    ? $"Without a mutation, the suite ran {baseline.Sum(run => run.Cases.Count)} cases and passed."
    : string.Join("\n", baselineProblems));

if (baselineProblems.Count == 0)
{
    foreach (var mutation in catalog.Mutations.Where(mutation => only.Count == 0 || only.Contains(mutation.Id)))
    {
        var path = Path.Combine(repository, mutation.File);
        var original = File.ReadAllText(path);
        MutationResult result;
        if (mutation.Apply(original) is not { } mutated)
        {
            result = MutationResult.NotRun(mutation, MutationStatus.NotApplied, $"The text it replaces is not in {mutation.File} exactly once.");
        }
        else
        {
            File.WriteAllText(path, mutated);
            try
            {
                result = await suite.BuildAsync(mutation.Id) is { } failure
                    ? MutationResult.NotRun(mutation, MutationStatus.BuildFailed, failure)
                    : MutationResult.Classify(mutation, baseline, await suite.TestAsync(mutation.Id, Path.Combine(output, mutation.Id)));
            }
            finally
            {
                File.WriteAllText(path, original);
            }
        }

        results.Add(result);
        Console.WriteLine(MutationReport.Line(result));
    }
}

var report = new MutationReport
{
    Commit = Suite.Run(root, "git", ["rev-parse", "HEAD"]).Trim(),
    Date = DateTimeOffset.UtcNow,
    BaselineCases = baseline.Sum(run => run.Cases.Count),
    BaselineProblems = baselineProblems,
    Mutations = results,
};
File.WriteAllText(Path.Combine(output, MutationReport.JsonFileName), report.Json());
File.WriteAllText(Path.Combine(output, MutationReport.MarkdownFileName), report.Markdown());
foreach (var problem in report.Problems)
{
    Console.Error.WriteLine($"Problem: {problem}");
}

if (ownsWork)
{
    try
    {
        Directory.Delete(work, recursive: true);
    }
    catch (IOException)
    {
        Console.Error.WriteLine($"Could not delete {work}.");
    }
}

Console.WriteLine($"Wrote {Path.Combine(output, MutationReport.MarkdownFileName)}.");
return report.Problems.Count == 0 ? 0 : 1;

static string RepositoryRoot()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "VerificationGlue.slnx")))
        {
            return directory.FullName;
        }
    }

    throw new InvalidOperationException($"No VerificationGlue.slnx above {Directory.GetCurrentDirectory()}.");
}
