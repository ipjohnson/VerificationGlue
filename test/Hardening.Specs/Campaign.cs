using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Hardening.Specs;

/// <summary>
/// The seed and case budget of a run. <c>HARDENING_SEED</c>, <c>HARDENING_CASES</c> and
/// <c>HARDENING_CASE</c> override them, so a replay command can run one case of a seed again.
/// <c>HARDENING_OUTPUT</c> names a folder for the report and the files of each mismatch.
/// </summary>
public sealed record CampaignSettings(int Seed, int Cases, int? Case = null, string? Output = null)
{
    public const int DefaultSeed = 20261119;

    public const int DefaultCases = 300;

    public const string SpecName = "A_fixed_seed_run_keeps_plain_CSharp_behavior";

    public static CampaignSettings FromEnvironment() => new(
        Number("HARDENING_SEED") ?? DefaultSeed,
        Number("HARDENING_CASES") ?? DefaultCases,
        Number("HARDENING_CASE"),
        Environment.GetEnvironmentVariable("HARDENING_OUTPUT") is { Length: > 0 } output ? output : null);

    public IEnumerable<int> Indices => Case is { } index ? [index] : Enumerable.Range(0, Cases);

    public string ReplayCommand(int index) =>
        $"HARDENING_SEED={Seed} HARDENING_CASES={Cases} HARDENING_CASE={index} dotnet test --project test/Hardening.Specs -c Release --vg-filter \"name:{SpecName}\"";

    private static int? Number(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } text ? int.Parse(text, CultureInfo.InvariantCulture) : null;
}

/// <summary>A mismatch and the smallest expression that still gives the same kind of mismatch in the same form.</summary>
public sealed record Shrunk(FormResult Original, FormResult Minimal, int Index);

public static class Campaign
{
    /// <summary>The number of mismatches a run shrinks, which bounds the time a failing run takes.</summary>
    public const int MaxShrunk = 3;

    public static async Task<CampaignReport> RunAsync(CampaignSettings settings, GeneratorMode mode = GeneratorMode.Rewrite, Func<string, string>? assertedText = null)
    {
        var clock = Stopwatch.StartNew();
        var cases = settings.Indices.Select(index => Grammar.Generate(settings.Seed, index, settings.Cases)).ToList();
        var results = await CaseRunner.RunAsync(cases, mode, assertedText);
        var shrunk = new List<Shrunk>();
        foreach (var mismatch in results.Where(result => result.Classification == Classification.Mismatch).DistinctBy(result => result.Case.Name).Take(MaxShrunk))
        {
            var minimal = await ShrinkAsync(mismatch, mode, assertedText);
            shrunk.Add(new Shrunk(mismatch, minimal, int.Parse(mismatch.Case.Name["Case_".Length..], CultureInfo.InvariantCulture)));
        }

        var report = new CampaignReport(settings, mode, results, shrunk, clock.Elapsed);
        report.Write();
        return report;
    }

    /// <summary>
    /// Replaces parts of the expression with smaller ones while the same form still gives the same
    /// kind of mismatch. Each round compiles every one-step reduction in one batch and keeps the smallest
    /// that fails, until no reduction fails.
    /// </summary>
    public static async Task<FormResult> ShrinkAsync(FormResult mismatch, GeneratorMode mode, Func<string, string>? assertedText)
    {
        var current = mismatch;
        while (current.Case.Tree is { } tree)
        {
            var candidates = Grammar.Reductions(tree).Select((node, i) => new ExpressionCase($"Shrink_{i}", node)).ToList();
            if (candidates.Count == 0)
            {
                break;
            }

            var results = await CaseRunner.RunAsync(candidates, mode, assertedText);
            var smaller = results
                .Where(result => result.Form == mismatch.Form && result.MismatchKind == mismatch.MismatchKind)
                .MinBy(result => (result.Case.Tree!.Size, result.Case.Text.Length));
            if (smaller is null)
            {
                break;
            }

            current = smaller;
        }

        return current;
    }
}

public sealed record CampaignReport(CampaignSettings Settings, GeneratorMode Mode, IReadOnlyList<FormResult> Results, IReadOnlyList<Shrunk> Shrunk, TimeSpan Elapsed)
{
    public IReadOnlyList<FormResult> Mismatches => [.. Results.Where(result => result.Classification == Classification.Mismatch)];

    public string Markdown()
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"## Step 2: generated expressions against plain C#\n\n");
        text.Append(CultureInfo.InvariantCulture, $"Seed {Settings.Seed}, {Settings.Indices.Count()} of {Settings.Cases} cases, depth up to {Grammar.MaxDepth}, generator mode {Mode}, {Results.Count} form runs in {Elapsed.TotalSeconds:0.0} s.\n\n");
        var baselines = Results.DistinctBy(result => result.Case.Name).Select(result => result.Baseline).ToList();
        text.Append(CultureInfo.InvariantCulture, $"In plain C#, {baselines.Count(b => b.Outcome == "passed")} expressions are true, {baselines.Count(b => b.Outcome == "failed")} are false and {baselines.Count(b => b.Outcome.StartsWith("throws ", StringComparison.Ordinal))} throw. {baselines.Count(b => b.Skipped.Count > 0)} skip a probe call. {Results.DistinctBy(r => r.Case.Name).Count(r => r.Case.IsAsync)} await and {Results.DistinctBy(r => r.Case.Name).Count(r => r.Case.WritesLocal)} write the local n.\n\n");
        text.Append("| Form | Expected tier | Ran under | Passed | Fallback | Mismatch |\n|---|---|---|---|---|---|\n");
        foreach (var group in Results.GroupBy(result => (result.Form, result.Expected.Tier, result.Ran)).OrderBy(group => group.Key.Form).ThenBy(group => group.Key.Tier))
        {
            text.Append(CultureInfo.InvariantCulture, $"| {group.Key.Form} | {Forms.Describe(group.Key.Tier)} | {(group.Key.Ran is { } ran ? Forms.Describe(ran) : "unknown")} | {group.Count(r => r.Classification == Classification.Passed)} | {group.Count(r => r.Classification == Classification.Fallback)} | {group.Count(r => r.Classification == Classification.Mismatch)} |\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"\n{Mismatches.Count} mismatches.\n");
        foreach (var shrunk in Shrunk)
        {
            text.Append("\n```text\n").Append(Describe(shrunk)).Append("\n```\n");
        }

        return text.ToString();
    }

    /// <summary>The failure text of one mismatch: what differed, the replay command and the regression row.</summary>
    public string Describe(Shrunk shrunk)
    {
        var original = shrunk.Original;
        var minimal = shrunk.Minimal;
        return string.Join('\n',
            $"{original.Case.Name} of seed {Settings.Seed}, {original.Form}: {original.MismatchKind} mismatch: {original.Detail}",
            $"  expression: {original.Case.Text}",
            $"  shrunk:     {minimal.Case.Text}",
            $"  shrunk run: {minimal.MismatchKind} mismatch: {minimal.Detail}",
            $"  plain C#:   {minimal.Baseline}",
            $"  assertion:  {minimal.Actual}",
            $"  tier:       expected {Forms.Describe(minimal.Expected.Tier)}, ran under {(minimal.Ran is { } ran ? Forms.Describe(ran) : "no known tier")}",
            $"  replay:     {Settings.ReplayCommand(shrunk.Index)}",
            $"  regression: add [Row({Literal(minimal.Case.Text)})] to RegressionSpecs.A_regression_case_keeps_plain_CSharp_behavior in test/Hardening.Specs/RegressionSpecs.cs");
    }

    public string FailureText() =>
        Shrunk.Count == 0 ? "" : $"{Mismatches.Count} form runs do not keep plain C# behavior.\n\n" + string.Join("\n\n", Shrunk.Select(Describe));

    /// <summary>Writes the report, and the source, configuration and observations of each shrunk mismatch.</summary>
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
            var source = new CaseSource([shrunk.Original.Case]);
            File.WriteAllText(
                Path.Combine(output, $"{shrunk.Original.Case.Name}.{shrunk.Original.Form}.txt"),
                string.Join('\n',
                    Describe(shrunk),
                    "",
                    $"Compiler: {CaseCompiler.Configuration}, generator mode {Mode}",
                    $"Environment: {Environment.OSVersion}, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}",
                    $"Original plain C#:  {shrunk.Original.Baseline}",
                    $"Original assertion: {shrunk.Original.Actual}",
                    $"Original message:   {shrunk.Original.Message}",
                    "",
                    source.CaseText(shrunk.Original.Case.Name),
                    "",
                    source.Baseline));
        }
    }

    private static string Literal(string text) => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
