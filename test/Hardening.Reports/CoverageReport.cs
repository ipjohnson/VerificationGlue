using System.Globalization;
using System.Text;

namespace Hardening.Reports;

// The coverage of one run, built from its run report and the pairwise selection. A combination is
// covered when a complete step ran it and every test of that step passed. A combination that no step
// ran, or whose step did not finish, is unverified. A run that is not complete never reports as covered.
public sealed record CoverageReport(RunReport Run, ConfigurationMatrix Matrix, ContractInventory Inventory)
{
    public const string FileName = "coverage.md";

    public const string Covered = "covered";
    public const string Failed = "failed";
    public const string Unverified = "unverified";

    public IReadOnlyList<CombinationStatus> Combinations()
    {
        return [.. Matrix.Select().Select(combination =>
        {
            var steps = Run.Steps.Where(step => step.Configuration.SequenceEqual(combination.Settings)).ToList();
            var status = steps.Count == 0 ? Unverified
                : steps.Any(step => step.Outcome == "failure" || step.Failed > 0) ? Failed
                : steps.Any(step => !step.Complete) ? Unverified
                : Covered;
            return new CombinationStatus(combination, status, [.. steps.Select(step => step.Name)]);
        })];
    }

    public IReadOnlyList<ScenarioRun> Failures => [.. Run.Scenarios.Where(entry => entry.Status == ScenarioStatus.Failed)];

    // Whether the run verified everything it set out to: complete, and nothing failed.
    public bool Passed => Run.Complete && Failures.Count == 0 && Run.Steps.All(step => step.Outcome == "success");

    public string Markdown()
    {
        var combinations = Combinations();
        var text = new StringBuilder();
        text.Append("## Hardening coverage\n\n");
        var commit = Run.Commit.Length >= 12 ? Run.Commit[..12] : Run.Commit;
        var verdict = !Run.Complete ? "**Coverage is incomplete.** The run did not finish every case it set out to run, so it does not count as passed."
            : Passed ? "The run is complete, and every case it ran passed."
            : "The run is complete, and some cases failed.";
        text.Append(CultureInfo.InvariantCulture, $"{verdict} Commit `{commit}`, {Run.Environment.Os} {Run.Environment.Architecture}, {Run.Environment.Runtime}, SDK {Run.Environment.Sdk}.\n\n");
        if (!Run.Complete)
        {
            text.Append("The run is incomplete because:\n\n");
            foreach (var reason in Run.Incomplete)
            {
                text.Append("- ").Append(reason).Append('\n');
            }

            text.Append('\n');
        }

        text.Append("| Step | Profile | Configuration | Outcome | Passed | Failed | Skipped | Complete |\n|---|---|---|---|---|---|---|---|\n");
        foreach (var step in Run.Steps)
        {
            text.Append(CultureInfo.InvariantCulture, $"| {step.Name} | {step.Profile} | {string.Join(" ", step.Configuration)} | {step.Outcome} | {step.Passed} | {step.Failed} | {step.Skipped} | {(step.Complete ? "yes" : "no")} |\n");
        }

        AppendScenarios(text);
        AppendCombinations(text, combinations);
        AppendExclusions(text);
        AppendFailures(text);
        return text.ToString();
    }

    private void AppendScenarios(StringBuilder text)
    {
        var entries = Run.Scenarios;
        var ids = entries.Select(entry => entry.Id).ToHashSet();
        text.Append("\n### Scenarios\n\n");
        text.Append(CultureInfo.InvariantCulture, $"{ids.Count} of the inventory's {Inventory.Scenarios.Count} Scenario IDs have evidence in the projects of this run. ");
        text.Append(CultureInfo.InvariantCulture, $"Of their {entries.Count} entries, {Count(ScenarioStatus.Passed)} passed, {Count(ScenarioStatus.Failed)} failed and {Count(ScenarioStatus.Blocked)} are blocked. ");
        text.Append(CultureInfo.InvariantCulture, $"The other {Inventory.Scenarios.Count - ids.Count} are not verified by this run.\n");
        var open = entries.Where(entry => entry.Status != ScenarioStatus.Passed).ToList();
        if (open.Count > 0)
        {
            text.Append("\n| Scenario ID | Configuration | Status | Detail |\n|---|---|---|---|\n");
            foreach (var entry in open)
            {
                text.Append(CultureInfo.InvariantCulture, $"| `{entry.Id}` | {string.Join(" ", entry.Configuration)} | {entry.Status} | {Cell(entry.Detail)} |\n");
            }
        }

        int Count(string status) => entries.Count(entry => entry.Status == status);
    }

    private void AppendCombinations(StringBuilder text, IReadOnlyList<CombinationStatus> combinations)
    {
        var pairs = Matrix.ValidPairs();
        var covered = pairs.Count(pair => combinations.Any(combination => combination.Status == Covered && combination.Combination.Covers(pair)));
        text.Append("\n### Combinations\n\n");
        text.Append(CultureInfo.InvariantCulture, $"The pairwise selection has {combinations.Count} combinations of {Matrix.Factors.Count} factors, which hold all {pairs.Count} valid pairs of values. ");
        text.Append(CultureInfo.InvariantCulture, $"This run covered {combinations.Count(c => c.Status == Covered)}, failed {combinations.Count(c => c.Status == Failed)} and left {combinations.Count(c => c.Status == Unverified)} unverified. ");
        text.Append(CultureInfo.InvariantCulture, $"The covered combinations hold {covered} of the {pairs.Count} valid pairs.\n\n");
        text.Append("| # | ").Append(string.Join(" | ", Matrix.Factors.Select(factor => factor.Name))).Append(" | Source | Status | Steps |\n");
        text.Append("|---|").Append(string.Concat(Matrix.Factors.Select(_ => "---|"))).Append("---|---|---|\n");
        var number = 0;
        foreach (var combination in combinations)
        {
            number++;
            var values = combination.Combination.Settings.Select(setting => setting[(setting.IndexOf('=') + 1)..]);
            text.Append(CultureInfo.InvariantCulture, $"| {number} | {string.Join(" | ", values)} | {combination.Combination.Origin} | {combination.Status} | {string.Join(", ", combination.Steps)} |\n");
        }

        var outside = Run.Steps.Where(step => combinations.All(combination => !combination.Combination.Settings.SequenceEqual(step.Configuration))).ToList();
        if (outside.Count > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nThese steps ran outside the selection, so they cover no combination: {string.Join(", ", outside.Select(step => $"{step.Name} ({string.Join(" ", step.Configuration)})"))}.\n");
        }
    }

    private void AppendExclusions(StringBuilder text)
    {
        text.Append("\n### Exclusions\n\n");
        text.Append(CultureInfo.InvariantCulture, $"The matrix excludes {Matrix.Exclusions.Count} pairs, and the selection holds none of them.\n\n");
        text.Append("| Pair | Reason |\n|---|---|\n");
        foreach (var exclusion in Matrix.Exclusions)
        {
            text.Append(CultureInfo.InvariantCulture, $"| {string.Join(" ", exclusion.Pair.Select(setting => $"`{setting}`"))} | {Cell(exclusion.Reason)} |\n");
        }
    }

    private void AppendFailures(StringBuilder text)
    {
        text.Append("\n### Failures\n\n");
        var steps = Run.Steps.Where(step => step.Outcome == "failure").ToList();
        if (Failures.Count == 0 && steps.Count == 0)
        {
            text.Append("No scenario and no step failed.\n");
            return;
        }

        foreach (var step in steps)
        {
            text.Append(CultureInfo.InvariantCulture, $"- Step {step.Name} failed. Its TRX file has {step.Failed} failed {(step.Failed == 1 ? "test" : "tests")}.\n");
        }

        foreach (var entry in Failures)
        {
            text.Append(CultureInfo.InvariantCulture, $"- `{entry.Id}` in {string.Join(" ", entry.Configuration)}: {entry.Detail}\n");
        }
    }

    private static string Cell(string? text) =>
        (text ?? "").Replace("|", "\\|", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

public sealed record CombinationStatus(Combination Combination, string Status, IReadOnlyList<string> Steps);
