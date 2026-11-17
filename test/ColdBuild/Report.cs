using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColdBuild;

public enum Tier
{
    GeneratorOff,
    Tier2,
    Tier3,
}

/// <summary>One cold build of every benchmark assembly in one configuration.</summary>
public sealed record Run(Tier Tier, int Repeat, IReadOnlyList<double> AssemblySeconds)
{
    public double TotalSeconds => AssemblySeconds.Sum();
}

/// <summary>What the generator put into the built assemblies, read from their metadata.</summary>
public sealed record GeneratedCounts(int Interceptors, int Copies, int Redirects);

public sealed record TierResult(
    Tier Tier,
    double MedianSeconds,
    double AddedSeconds,
    double AddedSecondsAt100000Calls,
    double RatioToGeneratorOff,
    GeneratedCounts Generated);

public sealed class Report
{
    public const int ReferenceCalls = 100_000;

    public Report(int assemblies, int tests, int calls, IReadOnlyList<Run> runs, IReadOnlyDictionary<Tier, GeneratedCounts> generated, string environment)
    {
        Assemblies = assemblies;
        Tests = tests;
        Calls = calls;
        Runs = runs;
        Environment = environment;

        var baseline = Median(Tier.GeneratorOff);
        Results = Enum.GetValues<Tier>().Select(tier =>
        {
            var median = Median(tier);
            var added = median - baseline;
            return new TierResult(tier, median, added, added * ReferenceCalls / calls, median / baseline, generated[tier]);
        }).ToArray();
    }

    public int Assemblies { get; }

    public int Tests { get; }

    public int Calls { get; }

    public IReadOnlyList<Run> Runs { get; }

    public string Environment { get; }

    public IReadOnlyList<TierResult> Results { get; }

    public static string Name(Tier tier) => tier switch
    {
        Tier.GeneratorOff => "Generator off",
        Tier.Tier2 => "Tier 2 alone",
        _ => "Tier 3 with redirects",
    };

    public string ToMarkdown()
    {
        var text = new StringBuilder();
        text.Append("## Cold-build benchmark\n\n");
        text.Append(CultureInfo.InvariantCulture, $"{Tests:N0} tests and {Calls:N0} `Verify` calls in {Assemblies} assemblies. ");
        text.Append(CultureInfo.InvariantCulture, $"Each build is a cold rebuild with build servers off, run {Runs.Count(run => run.Tier == Tier.GeneratorOff)} times per configuration in rotating order. ");
        text.Append("Seconds are the median over runs of the summed build time of all assemblies. ");
        text.Append(CultureInfo.InvariantCulture, $"Added seconds are scaled to {ReferenceCalls:N0} calls. {Environment}\n\n");
        text.Append("| Build | Median seconds | Added seconds | Added seconds at 100,000 calls | Ratio to generator off | Interceptors | Copies | Redirects |\n");
        text.Append("|---|---:|---:|---:|---:|---:|---:|---:|\n");
        foreach (var result in Results)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"| {Name(result.Tier)} | {result.MedianSeconds:F1} | {result.AddedSeconds:F1} | {result.AddedSecondsAt100000Calls:F1} | {result.RatioToGeneratorOff:F2} | {result.Generated.Interceptors:N0} | {result.Generated.Copies:N0} | {result.Generated.Redirects:N0} |\n");
        }

        text.Append("\n| Run | Build | Seconds per assembly | Total seconds |\n|---|---|---|---:|\n");
        foreach (var run in Runs)
        {
            var perAssembly = string.Join(", ", run.AssemblySeconds.Select(seconds => seconds.ToString("F1", CultureInfo.InvariantCulture)));
            text.Append(CultureInfo.InvariantCulture, $"| {run.Repeat + 1} | {Name(run.Tier)} | {perAssembly} | {run.TotalSeconds:F1} |\n");
        }

        return text.ToString();
    }

    private double Median(Tier tier)
    {
        var totals = Runs.Where(run => run.Tier == tier).Select(run => run.TotalSeconds).Order().ToArray();
        if (totals.Length == 0)
        {
            throw new InvalidOperationException($"No runs of {tier}.");
        }

        var middle = totals.Length / 2;
        return totals.Length % 2 == 1 ? totals[middle] : (totals[middle - 1] + totals[middle]) / 2;
    }
}
