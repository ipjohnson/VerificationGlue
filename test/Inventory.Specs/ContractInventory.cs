using System.Text.Json;
using System.Text.Json.Serialization;

namespace Inventory.Specs;

// The shape of docs/design/hardening-inventory.json. docs/design/hardening.md, Coverage inventory,
// defines each field. Verification status and run details belong in run reports, so an entry that
// carries them fails to load as an unknown field.
public sealed record ContractInventory
{
    public const string RelativePath = "docs/design/hardening-inventory.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    public required string Baseline { get; init; }

    // True while step 1 builds the inventory. Step 5 sets it to false, and then a supported entry
    // with a gap fails the check.
    public required bool AllowGaps { get; init; }

    public required IReadOnlyList<Area> Areas { get; init; }

    public required IReadOnlyList<Decision> Decisions { get; init; }

    public required IReadOnlyList<Scenario> Scenarios { get; init; }

    public static ContractInventory Parse(string json) =>
        JsonSerializer.Deserialize<ContractInventory>(json, Options) ?? throw new JsonException("The inventory is null.");

    public static ContractInventory Load(string repositoryRoot) =>
        Parse(File.ReadAllText(Path.Combine(repositoryRoot, RelativePath)));
}

public sealed record Area
{
    public required string Prefix { get; init; }

    public required string Name { get; init; }
}

public sealed record Decision
{
    public required string Id { get; init; }

    public required string Question { get; init; }

    public required string ProposedOwner { get; init; }

    public required bool Approved { get; init; }

    public required IReadOnlyList<string> Scenarios { get; init; }
}

public sealed record Scenario
{
    public required string Id { get; init; }

    public required string Contract { get; init; }

    public required string Source { get; init; }

    public required string Support { get; init; }

    public Outcome? Outcome { get; init; }

    public string? Decision { get; init; }

    public IReadOnlyList<string>? Dimensions { get; init; }

    public string? Constraints { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }

    public Gap? Gap { get; init; }

    public string? FollowUp { get; init; }
}

public sealed record Outcome
{
    public required string Kind { get; init; }

    public required string Detail { get; init; }
}

public sealed record Gap
{
    public required string Size { get; init; }

    public required int Step { get; init; }

    public required string Missing { get; init; }
}
