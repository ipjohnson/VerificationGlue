using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hardening.Campaign;

// The shape of docs/design/hardening-campaign.json: who triages a failure, the issue label that marks it,
// how long the run's artifacts are kept, and the case count and time limit of each run.
public sealed record CampaignConfig
{
    public const string RelativePath = "docs/design/hardening-campaign.json";

    // The project that holds the generator specs.
    public const string SpecsProject = "test/Hardening.Specs";

    public const string MutationsName = "mutations";

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // The GitHub login that each failure issue is assigned to.
    public required string Owner { get; init; }

    public required string Label { get; init; }

    public required int RetentionDays { get; init; }

    public required IReadOnlyList<Generator> Generators { get; init; }

    public required MutationRun Mutations { get; init; }

    public int TotalLimitMinutes => Generators.Sum(generator => generator.LimitMinutes) + Mutations.LimitMinutes;

    public static CampaignConfig Parse(string json) =>
        JsonSerializer.Deserialize<CampaignConfig>(json, Options) ?? throw new JsonException("The campaign configuration is null.");

    public static CampaignConfig Load(string repositoryRoot) =>
        Parse(File.ReadAllText(Path.Combine(repositoryRoot, RelativePath)));

    // The seed of a run on the given UTC date, such as 20261123, so each run on a new day uses a new seed.
    public static int SeedFor(DateOnly date) => int.Parse(date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Owner))
        {
            problems.Add("The campaign names no owner.");
        }

        if (string.IsNullOrWhiteSpace(Label))
        {
            problems.Add("The campaign names no issue label.");
        }

        if (RetentionDays <= 0)
        {
            problems.Add("retentionDays must be positive.");
        }

        if (Generators.Count == 0)
        {
            problems.Add("The campaign runs no generator.");
        }

        foreach (var name in Generators.Select(generator => generator.Name).Append(MutationsName).GroupBy(name => name).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            problems.Add($"More than one run is named {name}.");
        }

        foreach (var generator in Generators)
        {
            if (generator.Cases <= 0)
            {
                problems.Add($"{generator.Name} must run at least one case.");
            }

            if (generator.LimitMinutes <= 0)
            {
                problems.Add($"{generator.Name} must have a positive limit.");
            }
        }

        if (Mutations.LimitMinutes <= 0)
        {
            problems.Add($"{MutationsName} must have a positive limit.");
        }

        return problems;
    }
}

// A generator spec of test/Hardening.Specs, run with Cases as HARDENING_CASES and stopped at LimitMinutes.
public sealed record Generator
{
    public required string Name { get; init; }

    public required string Test { get; init; }

    public required int Cases { get; init; }

    public required int LimitMinutes { get; init; }

    public string ReplayCommand(int seed) =>
        $"HARDENING_SEED={seed} HARDENING_CASES={Cases} dotnet test --project {CampaignConfig.SpecsProject} -c Release --vg-filter \"name:{Test}\"";
}

public sealed record MutationRun
{
    public required int LimitMinutes { get; init; }
}
