using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hardening.Mutations;

// The shape of docs/design/hardening-mutations.json. Each mutation replaces one exact piece of text in one
// source file. An explanation records why the suite once let a mutation survive.
public sealed record MutationCatalog
{
    public const string RelativePath = "docs/design/hardening-mutations.json";

    // The candidate mutations that docs/design/hardening.md, Test effectiveness and execution budgets, names.
    public static readonly IReadOnlyList<string> Kinds =
    [
        "Evaluate a skipped operand",
        "Record setup as a call",
        "Reverse setup precedence",
        "Consume a response during verification",
        "Skip a cleanup stage",
    ];

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public required IReadOnlyList<Mutation> Mutations { get; init; }

    public static MutationCatalog Parse(string json) =>
        JsonSerializer.Deserialize<MutationCatalog>(json, Options) ?? throw new JsonException("The catalog is null.");

    public static MutationCatalog Load(string repositoryRoot) =>
        Parse(File.ReadAllText(Path.Combine(repositoryRoot, RelativePath)));

    // Duplicate IDs, a kind the design doc does not name, a named kind with no mutation, an incomplete
    // explanation, and a mutation whose text is not in its file under the root exactly once.
    public IReadOnlyList<string> Validate(string repositoryRoot)
    {
        var problems = new List<string>();
        foreach (var id in Mutations.GroupBy(mutation => mutation.Id).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            problems.Add($"{id} is used by more than one mutation.");
        }

        foreach (var kind in Kinds.Where(kind => Mutations.All(mutation => mutation.Kind != kind)))
        {
            problems.Add($"No mutation is of the kind '{kind}'.");
        }

        foreach (var mutation in Mutations)
        {
            if (!Kinds.Contains(mutation.Kind))
            {
                problems.Add($"{mutation.Id} has the kind '{mutation.Kind}', which docs/design/hardening.md does not name.");
            }

            if (mutation.Explanation is { } explanation && explanation.Problem() is { } explanationProblem)
            {
                problems.Add($"{mutation.Id}: {explanationProblem}");
            }

            var path = Path.Combine(repositoryRoot, mutation.File);
            if (!File.Exists(path))
            {
                problems.Add($"{mutation.Id} changes {mutation.File}, which does not exist.");
            }
            else if (mutation.Apply(File.ReadAllText(path)) is null)
            {
                problems.Add($"{mutation.Id}: the text it replaces is not in {mutation.File} exactly once.");
            }
        }

        return problems;
    }
}

public sealed record Mutation
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public required string Change { get; init; }

    public required string File { get; init; }

    public required string Find { get; init; }

    public required string Replace { get; init; }

    public Explanation? Explanation { get; init; }

    // The text with Find replaced, or null when Find is not in it exactly once.
    public string? Apply(string text)
    {
        var first = text.IndexOf(Find, StringComparison.Ordinal);
        if (Find.Length == 0 || first < 0 || text.IndexOf(Find, first + 1, StringComparison.Ordinal) >= 0)
        {
            return null;
        }

        return string.Concat(text.AsSpan(0, first), Replace, text.AsSpan(first + Find.Length));
    }
}

// Why a mutation survived. A missing assertion names the case added to catch it. An equivalent mutation
// changes no behavior the contract covers, and the reason says why.
public sealed record Explanation
{
    public const string MissingAssertion = "missing-assertion";

    public const string Equivalent = "equivalent";

    public required string Kind { get; init; }

    public string? Case { get; init; }

    public required string Reason { get; init; }

    public string? Problem() => Kind switch
    {
        MissingAssertion when string.IsNullOrWhiteSpace(Case) => "a missing-assertion explanation must name the case that catches the mutation.",
        Equivalent when Case is not null => "an equivalent-mutation explanation names no case.",
        MissingAssertion or Equivalent when string.IsNullOrWhiteSpace(Reason) => "the explanation has no reason.",
        MissingAssertion or Equivalent => null,
        _ => $"the explanation kind '{Kind}' is neither {MissingAssertion} nor {Equivalent}.",
    };
}
