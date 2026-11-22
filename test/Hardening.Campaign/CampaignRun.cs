using System.Globalization;
using Hardening.Mutations;
using Hardening.Reports;

namespace Hardening.Campaign;

public static class RunStatus
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Incomplete = "incomplete";
}

// How a run's process ended. Stopped is true when the tool stopped it at its limit.
public sealed record ProcessOutcome(int ExitCode, bool Stopped, TimeSpan Elapsed);

// One failure of a run. Key names the failure in the title of its issue, such as "MUT-03 does not match
// the catalog", so a repeat of it finds that issue. Detail holds what the run printed: the replay command, the seed, the source or sequence, and the
// expected and actual observations.
public sealed record RunFailure(string Key, string Detail);

// The result of one generator or of the mutation run. A run is failed when a case failed, and otherwise
// incomplete when it did not run its whole case budget, such as when the tool stopped it at its limit.
public sealed record CampaignRun
{
    public required string Name { get; init; }

    public string? Test { get; init; }

    public int? Seed { get; init; }

    public int? Cases { get; init; }

    public required int LimitMinutes { get; init; }

    public required double ElapsedSeconds { get; init; }

    public required string Status { get; init; }

    public required bool Overrun { get; init; }

    public required IReadOnlyList<string> Incomplete { get; init; }

    public required IReadOnlyList<RunFailure> Failures { get; init; }

    // The command that runs this run again.
    public required string Replay { get; init; }

    // The files the run wrote for its failures, as paths from the campaign's output folder.
    public IReadOnlyList<string> Files { get; init; } = [];

    public static CampaignRun ForGenerator(Generator generator, int seed, ProcessOutcome process, TrxFile? trx, SeedRun? seedRun, IReadOnlyList<string> files)
    {
        var incomplete = new List<string>();
        if (process.Stopped)
        {
            incomplete.Add(Overran(generator.LimitMinutes, process.Elapsed));
        }

        if (trx is null)
        {
            incomplete.Add("it wrote no TRX file");
        }
        else if (trx.Aborted)
        {
            incomplete.Add($"its TRX file says the run is {trx.Outcome}");
        }
        else if (trx.Results.Count == 0)
        {
            incomplete.Add($"no test matched {generator.Test}");
        }

        if (seedRun is null)
        {
            incomplete.Add("it wrote no seed file, so it did not start its cases");
        }
        else if (seedRun.Seed != seed || seedRun.Cases != generator.Cases || seedRun.Case is not null)
        {
            incomplete.Add($"it ran {(seedRun.Case is { } single ? $"case {single}" : $"{seedRun.Cases} cases")} of seed {seedRun.Seed}, not the {generator.Cases} cases of seed {seed}");
        }

        var failures = (trx?.Results ?? [])
            .Where(result => result.Outcome == TestOutcome.Failed)
            .Select(result => new RunFailure($"{result.Name} fails", result.Message ?? "The TRX file has no message for this case."))
            .ToList();
        if (failures.Count == 0 && process.ExitCode != 0 && !process.Stopped && incomplete.Count == 0)
        {
            incomplete.Add($"dotnet test exited with {process.ExitCode} and no case failed");
        }

        return new CampaignRun
        {
            Name = generator.Name,
            Test = generator.Test,
            Seed = seed,
            Cases = generator.Cases,
            LimitMinutes = generator.LimitMinutes,
            ElapsedSeconds = Math.Round(process.Elapsed.TotalSeconds, 1),
            Status = StatusOf(failures, incomplete),
            Overrun = process.Stopped,
            Incomplete = incomplete,
            Failures = failures,
            Replay = generator.ReplayCommand(seed),
            Files = files,
        };
    }

    public static CampaignRun ForMutations(int limitMinutes, ProcessOutcome process, MutationReport? report, int catalogCount)
    {
        var incomplete = new List<string>();
        if (process.Stopped)
        {
            incomplete.Add(Overran(limitMinutes, process.Elapsed));
        }

        var failures = new List<RunFailure>();
        if (report is null)
        {
            incomplete.Add($"it wrote no {MutationReport.JsonFileName}");
        }
        else
        {
            if (report.BaselineProblems.Count > 0)
            {
                failures.Add(new RunFailure("the mutation suite fails without a mutation", string.Join('\n', [.. report.BaselineProblems, $"  replay:     {MutationReplay(null)}"])));
            }
            else if (report.Mutations.Count < catalogCount)
            {
                incomplete.Add($"it ran {report.Mutations.Count} of the {catalogCount} mutations");
            }

            failures.AddRange(report.Mutations.Where(mutation => mutation.Problems.Count > 0).Select(Describe));
        }

        if (failures.Count == 0 && process.ExitCode != 0 && !process.Stopped && incomplete.Count == 0)
        {
            incomplete.Add($"Hardening.Mutations exited with {process.ExitCode} and reported no problem");
        }

        return new CampaignRun
        {
            Name = CampaignConfig.MutationsName,
            LimitMinutes = limitMinutes,
            ElapsedSeconds = Math.Round(process.Elapsed.TotalSeconds, 1),
            Status = StatusOf(failures, incomplete),
            Overrun = process.Stopped,
            Incomplete = incomplete,
            Failures = failures,
            Replay = MutationReplay(null),
        };
    }

    public static string MutationReplay(string? id) =>
        "dotnet run --project test/Hardening.Mutations -c Release -- --output artifacts/mutations" + (id is null ? "" : " --only " + id);

    // A mutation whose result does not match the catalog: the expected result, the actual one, and the cases.
    private static RunFailure Describe(MutationResult mutation)
    {
        var expected = mutation.Explanation switch
        {
            null => "detected by a failing case",
            { Kind: Explanation.MissingAssertion } => $"detected by {mutation.Explanation.Case}, the case its explanation names",
            _ => "survived, because the catalog explains it as an equivalent mutation",
        };
        var lines = new List<string>
        {
            $"{mutation.Id} {mutation.Kind}: {mutation.Change}",
            $"  expected:   {expected}",
            $"  actual:     {mutation.Status}{(mutation.Detail is { } detail ? ": " + detail : "")}",
            $"  replay:     {MutationReplay(mutation.Id)}",
        };
        lines.AddRange(mutation.Problems.Select(problem => "  problem:    " + problem));
        lines.AddRange(mutation.FailedRuns.Select(run => "  failed run: " + run));
        lines.AddRange(mutation.FailingCases.Take(20).Select(name => "  case:       " + name));
        return new RunFailure($"{mutation.Id} does not match the catalog", string.Join('\n', lines));
    }

    // A generator that did not start, such as when test/Hardening.Specs does not build.
    public static CampaignRun NotRun(Generator generator, int seed, string reason) => new()
    {
        Name = generator.Name,
        Test = generator.Test,
        Seed = seed,
        Cases = generator.Cases,
        LimitMinutes = generator.LimitMinutes,
        ElapsedSeconds = 0,
        Status = RunStatus.Incomplete,
        Overrun = false,
        Incomplete = [reason],
        Failures = [],
        Replay = generator.ReplayCommand(seed),
    };

    private static string Overran(int limitMinutes, TimeSpan elapsed) =>
        string.Create(CultureInfo.InvariantCulture, $"it ran over its {limitMinutes}-minute limit and was stopped after {elapsed.TotalMinutes:0.0} minutes");

    private static string StatusOf(IReadOnlyList<RunFailure> failures, IReadOnlyList<string> incomplete) =>
        failures.Count > 0 ? RunStatus.Failed : incomplete.Count > 0 ? RunStatus.Incomplete : RunStatus.Passed;
}
