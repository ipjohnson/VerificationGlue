using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ColdBuild;

/// <summary>The limits for one configuration: added seconds at 100,000 calls and the ratio to the generator-off build.</summary>
public sealed record TierBudget(double MaxAddedSecondsAt100000Calls, double MaxRatioToGeneratorOff);

/// <summary>One limit compared with what the run measured.</summary>
public sealed record BudgetCheck(Tier Tier, string Measure, double Limit, double Measured)
{
    public bool Passed => Measured <= Limit;
}

/// <summary>
/// The approved cold-build budget, read from <c>budgets.json</c>. The figures and where they come
/// from are in <c>docs/design/verification-glue.md</c>, Performance.
/// </summary>
public sealed record Budgets(TierBudget Tier2, TierBudget Tier3)
{
    public static Budgets Load(string path) =>
        JsonSerializer.Deserialize<Budgets>(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} holds no budget.");

    public IReadOnlyList<BudgetCheck> Check(Report report) =>
        report.Results.Where(result => result.Tier != Tier.GeneratorOff).SelectMany(result =>
        {
            var budget = result.Tier == Tier.Tier2 ? Tier2 : Tier3;
            return new[]
            {
                new BudgetCheck(result.Tier, "Added seconds at 100,000 calls", budget.MaxAddedSecondsAt100000Calls, result.AddedSecondsAt100000Calls),
                new BudgetCheck(result.Tier, "Ratio to generator off", budget.MaxRatioToGeneratorOff, result.RatioToGeneratorOff),
            };
        }).ToArray();

    public static string ToMarkdown(IReadOnlyList<BudgetCheck> checks)
    {
        var text = new StringBuilder("\n### Budget\n\n| Build | Measure | Limit | Measured | Result |\n|---|---|---:|---:|---|\n");
        foreach (var check in checks)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"| {Report.Name(check.Tier)} | {check.Measure} | {check.Limit:F2} | {check.Measured:F2} | {(check.Passed ? "Passed" : "Over budget")} |\n");
        }

        return text.ToString();
    }
}
