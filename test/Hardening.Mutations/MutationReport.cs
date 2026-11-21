using System.Text;
using System.Text.Json;

namespace Hardening.Mutations;

// mutations.json and mutations.md: the result of each mutation, and the problems that fail the run.
public sealed record MutationReport
{
    public const string JsonFileName = "mutations.json";

    public const string MarkdownFileName = "mutations.md";

    // The failing cases the summary table names for each mutation. The details list up to ListedCases.
    private const int NamedCases = 2;

    private const int ListedCases = 50;

    public required string Commit { get; init; }

    public required DateTimeOffset Date { get; init; }

    public required int BaselineCases { get; init; }

    public required IReadOnlyList<string> BaselineProblems { get; init; }

    public required IReadOnlyList<MutationResult> Mutations { get; init; }

    public IReadOnlyList<string> Problems => [.. BaselineProblems, .. Mutations.SelectMany(mutation => mutation.Problems)];

    public string Json() => JsonSerializer.Serialize(this, MutationCatalog.Options) + "\n";

    public string Markdown()
    {
        var text = new StringBuilder()
            .AppendLine("## Targeted mutations")
            .AppendLine()
            .AppendLine($"Commit `{Commit}`. Each mutation of `{MutationCatalog.RelativePath}` was applied alone to a copy of the repository, and the suite ran against it. Without a mutation, the suite ran {BaselineCases} cases.")
            .AppendLine();

        if (Problems.Count > 0)
        {
            text.AppendLine("Problems:").AppendLine();
            foreach (var problem in Problems)
            {
                text.AppendLine($"- {problem}");
            }

            text.AppendLine();
        }

        text.AppendLine("| ID | Mutation | Change | Result | Failing cases | Explanation |").AppendLine("|---|---|---|---|---|---|");
        foreach (var mutation in Mutations)
        {
            text.AppendLine($"| `{mutation.Id}` | {mutation.Kind} | {Cell(mutation.Change)} | {mutation.Status} | {Cell(Summary(mutation))} | {Cell(ExplanationText(mutation.Explanation))} |");
        }

        foreach (var mutation in Mutations.Where(mutation => mutation.FailingCases.Count > 0 || mutation.FailedRuns.Count > 0 || mutation.Detail is not null))
        {
            text.AppendLine().AppendLine($"### {mutation.Id}").AppendLine();
            if (mutation.Detail is { } detail)
            {
                text.AppendLine(detail).AppendLine();
            }

            foreach (var run in mutation.FailedRuns)
            {
                text.AppendLine($"- {run}");
            }

            foreach (var name in mutation.FailingCases.Take(ListedCases))
            {
                text.AppendLine($"- `{name}`");
            }

            if (mutation.FailingCases.Count > ListedCases)
            {
                text.AppendLine($"- and {mutation.FailingCases.Count - ListedCases} more");
            }
        }

        return text.ToString();
    }

    // The console line for one mutation, such as "MUT-02 Record setup as a call: detected by 3 cases, such as A.B".
    public static string Line(MutationResult mutation) => $"{mutation.Id} {mutation.Kind}: {mutation.Status}" + mutation.Status switch
    {
        MutationStatus.Detected => " " + Summary(mutation),
        MutationStatus.Survived => mutation.Explanation is null ? ", with no explanation" : $", explained as {mutation.Explanation.Kind}",
        _ => $": {mutation.Detail}",
    };

    private static string Summary(MutationResult mutation)
    {
        if (mutation.Status != MutationStatus.Detected)
        {
            return "";
        }

        if (mutation.FailingCases.Count == 0)
        {
            return "by " + string.Join(", ", mutation.FailedRuns);
        }

        var named = string.Join(", ", mutation.FailingCases.Take(NamedCases).Select(name => $"`{name}`"));
        return mutation.FailingCases.Count <= NamedCases
            ? $"by {named}"
            : $"by {mutation.FailingCases.Count} cases, such as {named}";
    }

    // The explanation as the report and the table in docs/design/hardening.md write it.
    public static string ExplanationText(Explanation? explanation) => explanation switch
    {
        null => "",
        { Kind: Explanation.MissingAssertion } => $"Missing assertion, now caught by `{explanation.Case}`. {explanation.Reason}",
        _ => $"Equivalent mutation. {explanation.Reason}",
    };

    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
