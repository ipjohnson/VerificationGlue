namespace ColdBuild.Specs;

public class ReportSpecs
{
    [VerifyTest]
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

        var report = new Report(2, 1_000, 50_000, 1_100, runs, generated, "");

        var tier2 = report.Results.Single(result => result.Tier == Tier.Tier2);
        Assert.Equal(32, tier2.MedianSeconds);
        Assert.Equal(10, tier2.AddedSeconds);
        Assert.Equal(20, tier2.AddedSecondsAt100000Calls);
        Assert.Equal(32.0 / 22, tier2.RatioToGeneratorOff, 6);

        var tier3 = report.Results.Single(result => result.Tier == Tier.Tier3);
        Assert.Equal(40, tier3.AddedSecondsAt100000Calls);
        Assert.Contains("| Tier 3 with redirects | 42.0 | 20.0 | 40.0 | 1.91 |", report.ToMarkdown(), StringComparison.Ordinal);
        Assert.Contains("1,000 tests and 50,000 calls in 2 assemblies, of which 1,100 are `VerifyEquivalent`", report.ToMarkdown(), StringComparison.Ordinal);
    }

    [VerifyTest]
    public void A_configuration_over_either_limit_fails_the_budget()
    {
        Run[] runs =
        [
            new(Tier.GeneratorOff, 0, [10]),
            new(Tier.Tier2, 0, [60]),
            new(Tier.Tier3, 0, [30]),
        ];
        var generated = Enum.GetValues<Tier>().ToDictionary(tier => tier, _ => new GeneratedCounts(0, 0, 0));
        var report = new Report(1, 1_000, 100_000, 0, runs, generated, "");
        var budgets = new Budgets(new TierBudget(160, 5), new TierBudget(120, 7));

        var checks = budgets.Check(report);

        Assert.Equal(
            [(Tier.Tier2, "Added seconds at 100,000 calls", true), (Tier.Tier2, "Ratio to generator off", false), (Tier.Tier3, "Added seconds at 100,000 calls", true), (Tier.Tier3, "Ratio to generator off", true)],
            checks.Select(check => (check.Tier, check.Measure, check.Passed)));
        Assert.Contains("| Tier 2 alone | Ratio to generator off | 5.00 | 6.00 | Over budget |", Budgets.ToMarkdown(checks), StringComparison.Ordinal);
    }

    [VerifyTest]
    public void The_committed_budget_is_the_approved_one()
    {
        var budgets = Budgets.Load(Path.Combine(AppContext.BaseDirectory, "budgets.json"));

        Assert.Equal(new Budgets(new TierBudget(160, 8.5), new TierBudget(120, 7.0)), budgets);
    }
}
