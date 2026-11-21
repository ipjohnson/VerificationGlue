using Hardening.Reports;

namespace Hardening.Mutations;

// One test command of the suite: its exit code, whether it timed out, the cases of its TRX files, and the
// failed cases its console output names, for a project that writes no TRX file.
public sealed record RunResult(string Name, int ExitCode, bool TimedOut, IReadOnlyList<CaseResult> Cases, IReadOnlyList<string> ConsoleFailures)
{
    public bool Failed => TimedOut || ExitCode != 0;

    public string Describe() => TimedOut ? $"{Name} timed out" : $"{Name} exited with {ExitCode}";
}

// Name is the full name of the test method, with the row arguments when the runner writes them.
public sealed record CaseResult(string Name, string Outcome);

public static class MutationStatus
{
    public const string Detected = "detected";
    public const string Survived = "survived";
    public const string NotApplied = "not applied";
    public const string BuildFailed = "build failed";
}

public sealed record MutationResult
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public required string Change { get; init; }

    public required string Status { get; init; }

    // The cases that failed, then the cases that passed without the mutation and did not run with it.
    public required IReadOnlyList<string> FailingCases { get; init; }

    // The test commands that failed or timed out.
    public required IReadOnlyList<string> FailedRuns { get; init; }

    public string? Detail { get; init; }

    public Explanation? Explanation { get; init; }

    public required IReadOnlyList<string> Problems { get; init; }

    public static MutationResult NotRun(Mutation mutation, string status, string detail) => new()
    {
        Id = mutation.Id,
        Kind = mutation.Kind,
        Change = mutation.Change,
        Status = status,
        FailingCases = [],
        FailedRuns = [],
        Detail = detail,
        Explanation = mutation.Explanation,
        Problems = [$"{mutation.Id} {status}: {detail}"],
    };

    // Detected when a case fails, when a case that passed without the mutation does not run, or when a
    // test command fails. Checks the result against the mutation's explanation.
    public static MutationResult Classify(Mutation mutation, IReadOnlyList<RunResult> baseline, IReadOnlyList<RunResult> mutated)
    {
        var cases = mutated.SelectMany(run => run.Cases).ToList();
        var failed = cases.Where(result => result.Outcome == TestOutcome.Failed).Select(result => result.Name)
            .Concat(mutated.SelectMany(run => run.ConsoleFailures))
            .Distinct()
            .Order(StringComparer.Ordinal);
        var ran = cases.Select(result => result.Name).ToHashSet();
        var missing = baseline.SelectMany(run => run.Cases)
            .Where(result => result.Outcome == TestOutcome.Passed && !ran.Contains(result.Name))
            .Select(result => result.Name)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .Select(name => $"{name} (not run)");
        var failing = failed.Concat(missing).ToList();
        var failedRuns = mutated.Where(run => run.Failed).Select(run => run.Describe()).ToList();
        var detected = failing.Count > 0 || failedRuns.Count > 0;

        var problems = new List<string>();
        switch (mutation.Explanation)
        {
            case null when !detected:
                problems.Add($"{mutation.Id} survived, and {MutationCatalog.RelativePath} records no explanation.");
                break;
            case { Kind: Explanation.MissingAssertion, Case: { } expected } when !failing.Any(name => Names(name, expected)):
                problems.Add($"{mutation.Id} is not caught by {expected}, the case its explanation names.");
                break;
            case { Kind: Explanation.Equivalent } when detected:
                problems.Add($"{mutation.Id} is caught, so the explanation that it is an equivalent mutation is out of date.");
                break;
        }

        return new MutationResult
        {
            Id = mutation.Id,
            Kind = mutation.Kind,
            Change = mutation.Change,
            Status = detected ? MutationStatus.Detected : MutationStatus.Survived,
            FailingCases = failing,
            FailedRuns = failedRuns,
            Explanation = mutation.Explanation,
            Problems = problems,
        };
    }

    // Why the suite cannot judge mutations: a command or a case that fails without one.
    public static IReadOnlyList<string> BaselineProblems(IReadOnlyList<RunResult> baseline) =>
    [
        .. baseline.Where(run => run.Failed).Select(run => $"Without a mutation, {run.Describe()}."),
        .. baseline.SelectMany(run => run.Cases).Where(result => result.Outcome == TestOutcome.Failed)
            .Select(result => $"Without a mutation, {result.Name} fails."),
    ];

    // Whether a failing case, which may end with row arguments or "(not run)", is the case Type.Method or Namespace.Type.Method.
    public static bool Names(string name, string expected)
    {
        var open = name.IndexOf('(', StringComparison.Ordinal);
        var bare = (open < 0 ? name : name[..open]).TrimEnd();
        return bare == expected || bare.EndsWith("." + expected, StringComparison.Ordinal);
    }
}
