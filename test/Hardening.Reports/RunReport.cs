using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hardening.Reports;

// What one run of the hardening job verified. docs/design/hardening.md, Run and coverage reports,
// defines each field. Complete is false when a step was cancelled, skipped or cut short, or when a
// test the inventory names did not run, and then Incomplete says why.
public sealed partial record RunReport
{
    public const string FileName = "run-report.json";

    public static readonly IReadOnlySet<string> Statuses = new HashSet<string> { ScenarioStatus.Passed, ScenarioStatus.Failed, ScenarioStatus.Blocked };

    public required string Commit { get; init; }

    public required DateTimeOffset Created { get; init; }

    public string? Location { get; init; }

    public required RunEnvironment Environment { get; init; }

    public required bool Complete { get; init; }

    public required IReadOnlyList<string> Incomplete { get; init; }

    public required IReadOnlyList<StepRun> Steps { get; init; }

    public required IReadOnlyList<ScenarioRun> Scenarios { get; init; }

    public static RunReport Parse(string json) =>
        JsonSerializer.Deserialize<RunReport>(json, ConfigurationMatrix.Options) ?? throw new JsonException("The run report is null.");

    public string Json() => JsonSerializer.Serialize(this, ConfigurationMatrix.Options);

    // The problems that make the report unusable as evidence. Each entry must name a Scenario ID that
    // the inventory defines, so a report cannot claim a contract the inventory does not have.
    public IReadOnlyList<string> Validate(ContractInventory inventory)
    {
        var problems = new List<string>();
        if (!ShaPattern().IsMatch(Commit))
        {
            problems.Add($"the commit '{Commit}' is not a full commit sha");
        }

        var ids = inventory.Scenarios.Select(scenario => scenario.Id).ToHashSet();
        var seen = new HashSet<string>();
        foreach (var entry in Scenarios)
        {
            if (!ids.Contains(entry.Id))
            {
                problems.Add($"{entry.Id}: the run report names a Scenario ID that the inventory does not define");
            }

            if (!Statuses.Contains(entry.Status))
            {
                problems.Add($"{entry.Id}: the status '{entry.Status}' is not one of {string.Join(", ", Statuses)}");
            }

            if (entry.Commit != Commit)
            {
                problems.Add($"{entry.Id}: the entry's commit {entry.Commit} is not the run's commit {Commit}");
            }

            if (!seen.Add($"{entry.Id} {string.Join(" ", entry.Configuration)}"))
            {
                problems.Add($"{entry.Id}: the run report has more than one entry for the configuration {string.Join(" ", entry.Configuration)}");
            }
        }

        return problems;
    }

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex ShaPattern();
}

public sealed record RunEnvironment
{
    public required string Os { get; init; }

    public required string Architecture { get; init; }

    public required string Runtime { get; init; }

    public required string Sdk { get; init; }

    public string? Runner { get; init; }
}

// Outcome is the step's outcome in the workflow: success, failure, cancelled or skipped. Results is
// the TRX file, relative to the report, or null when the step wrote none.
public sealed record StepRun
{
    public required string Name { get; init; }

    public required string Profile { get; init; }

    public required IReadOnlyList<string> Configuration { get; init; }

    public required string Outcome { get; init; }

    public required bool Complete { get; init; }

    public string? Results { get; init; }

    public required int Passed { get; init; }

    public required int Failed { get; init; }

    public required int Skipped { get; init; }

    public string? Detail { get; init; }
}

// One Scenario ID in one configuration. Tests are the inventory's evidence in the profile's project.
public sealed record ScenarioRun
{
    public required string Id { get; init; }

    public required string Status { get; init; }

    public required string Commit { get; init; }

    public required IReadOnlyList<string> Configuration { get; init; }

    public required RunEnvironment Environment { get; init; }

    public IReadOnlyList<SeedRun>? Seeds { get; init; }

    public required IReadOnlyList<TestRun> Tests { get; init; }

    public string? Detail { get; init; }

    public string? Location { get; init; }
}

public sealed record TestRun
{
    public required string Test { get; init; }

    public required string Outcome { get; init; }

    public string? Step { get; init; }
}

// Case is set when the run replayed one case. Budget is the number of cases the spec runs by default.
public sealed record SeedRun
{
    public required string Test { get; init; }

    public required int Seed { get; init; }

    public required int Cases { get; init; }

    public required int Budget { get; init; }

    public int? Case { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Truncated => Case is not null || Cases < Budget;
}

public static class ScenarioStatus
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
}
