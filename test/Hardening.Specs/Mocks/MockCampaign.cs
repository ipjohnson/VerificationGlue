using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Hardening.Specs.Mocks;

/// <summary>
/// One sequence run on the reference model and on VerificationGlue. <see cref="MismatchKind"/> is null
/// when the observations agree, <c>observation</c> when they differ, <c>harness</c> when the harness
/// cannot run the sequence, and <c>hang</c> when the run does not finish.
/// </summary>
public sealed record SequenceResult(Sequence Sequence, IReadOnlyList<string> Expected, IReadOnlyList<string> Actual, string? MismatchKind, string? Detail)
{
    public bool Passed => MismatchKind is null;
}

/// <summary>A mismatch, the smallest sequence that still gives the same kind of mismatch, and the case index that replays it.</summary>
public sealed record ShrunkSequence(SequenceResult Original, SequenceResult Minimal, int Index);

public static class MockCampaign
{
    public const int DefaultCases = 2000;

    public const string SpecName = "A_fixed_seed_run_of_mock_sequences_matches_the_reference_model";

    /// <summary>The time one sequence may take. A scheduling fault that blocks a flow fails the case instead of the run.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static CampaignSettings Settings() => CampaignSettings.FromEnvironment(DefaultCases, SpecName);

    public static async Task<SequenceResult> RunAsync(Sequence sequence, ModelFault fault = ModelFault.None)
    {
        IReadOnlyList<string> expected;
        try
        {
            expected = ReferenceModel.Run(sequence, fault);
        }
        catch (Exception exception)
        {
            return new SequenceResult(sequence, [], [], "harness", "the model failed: " + exception);
        }

        IReadOnlyList<string> actual;
        try
        {
            actual = await Interpreter.RunAsync(sequence).WaitAsync(Timeout);
        }
        catch (TimeoutException)
        {
            return new SequenceResult(sequence, expected, [], "hang", $"the run did not finish in {Timeout.TotalSeconds} s");
        }
        catch (Exception exception)
        {
            return new SequenceResult(sequence, expected, [], "harness", "the interpreter failed: " + exception);
        }

        var length = Math.Min(expected.Count, actual.Count);
        for (var i = 0; i < length; i++)
        {
            if (expected[i] != actual[i])
            {
                return new SequenceResult(sequence, expected, actual, "observation", string.Create(CultureInfo.InvariantCulture, $"observation {i}: expected {expected[i]}, got {actual[i]}"));
            }
        }

        return expected.Count == actual.Count
            ? new SequenceResult(sequence, expected, actual, null, null)
            : new SequenceResult(sequence, expected, actual, "observation", string.Create(CultureInfo.InvariantCulture, $"expected {expected.Count} observations, got {actual.Count}"));
    }

    public static async Task<IReadOnlyList<SequenceResult>> RunAllAsync(IReadOnlyList<Sequence> sequences, ModelFault fault = ModelFault.None)
    {
        var results = new List<SequenceResult>();
        foreach (var sequence in sequences)
        {
            results.Add(await RunAsync(sequence, fault));
        }

        return results;
    }

    public static async Task<MockReport> RunCampaignAsync(CampaignSettings settings, ModelFault fault = ModelFault.None)
    {
        var clock = Stopwatch.StartNew();
        var sequences = settings.Indices.Select(index => SequenceGenerator.Generate(settings.Seed, index, settings.Cases)).ToList();
        var results = await RunAllAsync(sequences, fault);
        var shrunk = new List<ShrunkSequence>();
        foreach (var mismatch in results.Where(result => !result.Passed).Take(Campaign.MaxShrunk))
        {
            var minimal = await ShrinkAsync(mismatch, fault);
            shrunk.Add(new ShrunkSequence(mismatch, minimal, int.Parse(mismatch.Sequence.Name["Case_".Length..], CultureInfo.InvariantCulture)));
        }

        var report = new MockReport(settings, fault, results, shrunk, clock.Elapsed);
        report.Write();
        return report;
    }

    /// <summary>Shrinks a mismatch with step 2's shrinker, keeping reductions that give the same kind of mismatch.</summary>
    public static Task<SequenceResult> ShrinkAsync(SequenceResult mismatch, ModelFault fault) =>
        Shrinker.ShrinkAsync(
            mismatch,
            result => SequenceGenerator.Reductions(result.Sequence),
            candidates => RunAllAsync(candidates, fault),
            result => result.MismatchKind == mismatch.MismatchKind,
            result => (result.Sequence.Size, result.Sequence.Json().Length));
}

public sealed record MockReport(CampaignSettings Settings, ModelFault Fault, IReadOnlyList<SequenceResult> Results, IReadOnlyList<ShrunkSequence> Shrunk, TimeSpan Elapsed)
{
    public IReadOnlyList<SequenceResult> Mismatches => [.. Results.Where(result => !result.Passed)];

    /// <summary>The number of sequences that hold each kind of op, at any depth, which shows each factor was generated.</summary>
    public IReadOnlyDictionary<string, int> Coverage => Results
        .SelectMany(result => All(result.Sequence.Ops).Select(Kind).Distinct())
        .GroupBy(kind => kind)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count());

    public string Markdown()
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"## Step 3: generated mock sequences against the reference model\n\n");
        text.Append(CultureInfo.InvariantCulture, $"Seed {Settings.Seed}, {Settings.Indices.Count()} of {Settings.Cases} sequences of {SequenceGenerator.MinOps} to {SequenceGenerator.MaxOps} ops, model fault {Fault}, {Results.Sum(result => result.Expected.Count)} observations in {Elapsed.TotalSeconds:0.0} s.\n\n");
        text.Append("| Op | Sequences |\n|---|---|\n");
        foreach (var (kind, count) in Coverage)
        {
            text.Append(CultureInfo.InvariantCulture, $"| {kind} | {count} |\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"\n{Mismatches.Count} mismatches.\n");
        foreach (var shrunk in Shrunk)
        {
            text.Append("\n```text\n").Append(Describe(shrunk)).Append("\n```\n");
        }

        return text.ToString();
    }

    /// <summary>The failure text of one mismatch: what differed, the shrunk sequence, the replay command and the regression row.</summary>
    public string Describe(ShrunkSequence shrunk)
    {
        var original = shrunk.Original;
        var minimal = shrunk.Minimal;
        return string.Join('\n',
            $"{original.Sequence.Name} of seed {Settings.Seed}: {original.MismatchKind} mismatch: {original.Detail}",
            $"  shrunk from {original.Sequence.Ops.Count} to {minimal.Sequence.Ops.Count} ops:",
            minimal.Sequence.ToString(),
            $"  shrunk run: {minimal.MismatchKind} mismatch: {minimal.Detail}",
            $"  model:      {string.Join(" / ", minimal.Expected)}",
            $"  mocks:      {string.Join(" / ", minimal.Actual)}",
            $"  replay:     {Settings.ReplayCommand(shrunk.Index)}",
            $"  regression: add [Row(\"\"\"{minimal.Sequence.Json()}\"\"\")] to MockRegressionSpecs.A_regression_sequence_matches_the_reference_model in test/Hardening.Specs/Mocks/MockRegressionSpecs.cs");
    }

    public string FailureText() =>
        Mismatches.Count == 0 ? "" : $"{Mismatches.Count} sequences do not match the reference model.\n\n" + string.Join("\n\n", Shrunk.Select(Describe));

    /// <summary>Writes the report, and the sequence and observations of each shrunk mismatch.</summary>
    public void Write()
    {
        if (Settings.Output is not { } output)
        {
            return;
        }

        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "report.md"), Markdown());
        foreach (var shrunk in Shrunk)
        {
            File.WriteAllText(
                Path.Combine(output, $"{shrunk.Original.Sequence.Name}.txt"),
                string.Join('\n',
                    Describe(shrunk),
                    "",
                    $"Environment: {Environment.OSVersion}, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}",
                    "Original sequence:",
                    shrunk.Original.Sequence.ToString(),
                    $"Original model: {string.Join(" / ", shrunk.Original.Expected)}",
                    $"Original mocks: {string.Join(" / ", shrunk.Original.Actual)}",
                    $"Original JSON:  {shrunk.Original.Sequence.Json()}"));
        }
    }

    private static IEnumerable<Op> All(IEnumerable<Op> ops) => ops.SelectMany(op => All(op.Inner).Prepend(op));

    private static string Kind(Op op) => op switch
    {
        Interleave interleave => $"Interleave {interleave.At}{(interleave.Await ? " awaited" : "")}",
        SetupNs { Await: true } => "SetupNs with an await",
        Invoke invoke => "Invoke " + invoke.Site.Member,
        _ => op.GetType().Name,
    };
}
