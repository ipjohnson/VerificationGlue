namespace ColdBuild.Specs;

public class ReportSpecs
{
    [Fact]
    public void Added_seconds_are_scaled_to_100000_calls_and_the_ratio_is_to_the_generator_off_median()
    {
        Run[] runs =
        [
            new(Tier.GeneratorOff, 0, [10, 10]),
            new(Tier.GeneratorOff, 1, [12, 12]),
            new(Tier.GeneratorOff, 2, [11, 11]),
            new(Tier.Tier2, 0, [15, 15]),
            new(Tier.Tier2, 1, [16, 16]),
            new(Tier.Tier2, 2, [40, 40]),
            new(Tier.Tier3, 0, [20, 20]),
            new(Tier.Tier3, 1, [22, 22]),
            new(Tier.Tier3, 2, [21, 21]),
        ];
        var generated = Enum.GetValues<Tier>().ToDictionary(tier => tier, _ => new GeneratedCounts(0, 0, 0));

        var report = new Report(2, 1_000, 50_000, runs, generated, "");

        var tier2 = report.Results.Single(result => result.Tier == Tier.Tier2);
        Assert.Equal(32, tier2.MedianSeconds);
        Assert.Equal(10, tier2.AddedSeconds);
        Assert.Equal(20, tier2.AddedSecondsAt100000Calls);
        Assert.Equal(32.0 / 22, tier2.RatioToGeneratorOff, 6);

        var tier3 = report.Results.Single(result => result.Tier == Tier.Tier3);
        Assert.Equal(40, tier3.AddedSecondsAt100000Calls);
        Assert.Contains("| Tier 3 with redirects | 42.0 | 20.0 | 40.0 | 1.91 |", report.ToMarkdown(), StringComparison.Ordinal);
    }
}
