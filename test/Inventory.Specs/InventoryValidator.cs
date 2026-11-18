using System.Text.RegularExpressions;

namespace Inventory.Specs;

// The checks that docs/design/hardening.md, Coverage inventory, asks CI to run on each pull request.
public sealed partial class InventoryValidator(string repositoryRoot)
{
    public const string GapListHeading = "### Gap list";
    public const string DecisionsHeading = "### Contract decisions";

    private static readonly HashSet<string> Supports = ["supported", "unsupported", "unclassified"];
    private static readonly HashSet<string> OutcomeKinds = ["refusal", "diagnostic", "throws", "fallback", "failure"];
    private static readonly HashSet<string> Sizes = ["xs", "s", "m", "l", "xl"];

    private readonly TestMethodIndex tests = new(repositoryRoot);
    private readonly Dictionary<string, IReadOnlySet<string>> anchors = [];

    public IReadOnlyList<string> Validate(ContractInventory inventory)
    {
        var problems = new List<string>();
        if (!ShaPattern().IsMatch(inventory.Baseline))
        {
            problems.Add($"baseline '{inventory.Baseline}' is not a full commit sha");
        }

        var prefixes = inventory.Areas.Select(area => area.Prefix).ToHashSet();
        var ids = new HashSet<string>();
        foreach (var scenario in inventory.Scenarios)
        {
            var match = IdPattern().Match(scenario.Id);
            if (!match.Success)
            {
                problems.Add($"{scenario.Id}: the Scenario ID is not of the form AREA-000");
            }
            else if (!prefixes.Contains(match.Groups["area"].Value))
            {
                problems.Add($"{scenario.Id}: the Scenario ID names the unknown area {match.Groups["area"].Value}");
            }

            if (!ids.Add(scenario.Id))
            {
                problems.Add($"{scenario.Id}: the Scenario ID is used more than once");
            }
        }

        var decisionIds = new HashSet<string>();
        foreach (var decision in inventory.Decisions)
        {
            if (!decisionIds.Add(decision.Id))
            {
                problems.Add($"{decision.Id}: the decision ID is used more than once");
            }

            if (string.IsNullOrWhiteSpace(decision.ProposedOwner))
            {
                problems.Add($"{decision.Id}: the decision has no proposed owner");
            }

            problems.AddRange(decision.Scenarios
                .Where(id => !ids.Contains(id))
                .Select(id => $"{decision.Id}: the decision names the unknown Scenario ID {id}"));
        }

        foreach (var scenario in inventory.Scenarios)
        {
            problems.AddRange(Check(scenario, inventory.AllowGaps, decisionIds).Select(problem => $"{scenario.Id}: {problem}"));
        }

        return problems;
    }

    // Checks that the gap list and the decision table in the design doc match the inventory.
    public IReadOnlyList<string> ValidateDesignDoc(ContractInventory inventory, string markdown)
    {
        var problems = new List<string>();

        var gapRows = MarkdownTable.Read(markdown, GapListHeading);
        if (gapRows is null)
        {
            problems.Add($"the design doc has no '{GapListHeading}' table");
        }
        else
        {
            var expected = inventory.Scenarios
                .Where(scenario => scenario.Gap is not null)
                .Select(scenario => $"{scenario.Id} {scenario.Gap!.Size} {scenario.Gap.Step}")
                .ToHashSet();
            var listed = gapRows.Select(row => row.Count >= 4 ? $"{row[0]} {row[2]} {row[3]}" : string.Join(" | ", row)).ToHashSet();
            problems.AddRange(expected.Except(listed).Order().Select(gap => $"the gap list is missing or misstates the gap '{gap}' (ID, size, step)"));
            problems.AddRange(listed.Except(expected).Order().Select(gap => $"the gap list has the row '{gap}' (ID, size, step), which no inventory gap matches"));
        }

        var decisionRows = MarkdownTable.Read(markdown, DecisionsHeading);
        if (decisionRows is null)
        {
            problems.Add($"the design doc has no '{DecisionsHeading}' table");
        }
        else
        {
            var expected = inventory.Decisions.Select(decision => $"{decision.Id} {decision.ProposedOwner}").ToHashSet();
            var listed = decisionRows.Select(row => row.Count >= 3 ? $"{row[0]} {row[2]}" : string.Join(" | ", row)).ToHashSet();
            problems.AddRange(expected.Except(listed).Order().Select(d => $"the decision table is missing or misstates '{d}' (ID, proposed owner)"));
            problems.AddRange(listed.Except(expected).Order().Select(d => $"the decision table has the row '{d}' (ID, proposed owner), which no inventory decision matches"));
        }

        return problems;
    }

    private IEnumerable<string> Check(Scenario scenario, bool allowGaps, IReadOnlySet<string> decisionIds)
    {
        if (string.IsNullOrWhiteSpace(scenario.Contract))
        {
            yield return "the contract is empty";
        }

        if (SourceProblem(scenario.Source) is { } source)
        {
            yield return source;
        }

        if (!Supports.Contains(scenario.Support))
        {
            yield return $"the support status '{scenario.Support}' is not one of {string.Join(", ", Supports)}";
        }

        if (scenario.Support == "unsupported")
        {
            if (scenario.Outcome is null)
            {
                yield return "an unsupported entry needs its expected refusal, diagnostic or fallback in outcome";
            }
            else if (!OutcomeKinds.Contains(scenario.Outcome.Kind))
            {
                yield return $"the outcome kind '{scenario.Outcome.Kind}' is not one of {string.Join(", ", OutcomeKinds)}";
            }
            else if (string.IsNullOrWhiteSpace(scenario.Outcome.Detail))
            {
                yield return "the outcome has no detail";
            }
        }
        else if (scenario.Outcome is not null)
        {
            yield return "only an unsupported entry has an outcome";
        }

        if (scenario.Decision is { } decision && !decisionIds.Contains(decision))
        {
            yield return $"the entry names the unknown decision {decision}";
        }

        foreach (var reference in scenario.Evidence)
        {
            if (EvidenceProblem(reference) is { } problem)
            {
                yield return $"evidence '{reference}': {problem}";
            }
        }

        if (scenario.Gap is { } gap)
        {
            if (!Sizes.Contains(gap.Size))
            {
                yield return $"the gap size '{gap.Size}' is not one of {string.Join(", ", Sizes)}";
            }

            if (gap.Step is < 2 or > 5)
            {
                yield return $"the gap names step {gap.Step}, and only steps 2 to 5 fill gaps";
            }

            if (string.IsNullOrWhiteSpace(gap.Missing))
            {
                yield return "the gap does not say what is missing";
            }

            if (!allowGaps && scenario.Support == "supported")
            {
                yield return "a supported entry has a gap, and the inventory no longer allows gaps";
            }
        }
        else if (scenario.Evidence.Count == 0)
        {
            yield return "the entry has no evidence and is not marked as an explicit gap";
        }
    }

    private string? EvidenceProblem(string reference)
    {
        var hash = reference.IndexOf('#');
        var path = hash < 0 ? reference : reference[..hash];
        if (PathProblem(path) is { } problem)
        {
            return problem;
        }

        if (hash >= 0)
        {
            return tests.Problem(path, reference[(hash + 1)..]);
        }

        var full = Path.Combine(repositoryRoot, path);
        if (File.Exists(full))
        {
            return null;
        }

        if (!Directory.Exists(full))
        {
            return $"{path} does not exist";
        }

        return Directory.EnumerateFiles(full, "*.csproj").Any() ? null : $"the directory {path} holds no project";
    }

    private string? SourceProblem(string source)
    {
        var hash = source.IndexOf('#');
        var path = hash < 0 ? source : source[..hash];
        if (PathProblem(path) is { } problem)
        {
            return $"source: {problem}";
        }

        var full = Path.Combine(repositoryRoot, path);
        if (!File.Exists(full))
        {
            return $"source: {path} does not exist";
        }

        if (hash < 0)
        {
            return null;
        }

        if (!anchors.TryGetValue(path, out var known))
        {
            known = MarkdownTable.Anchors(File.ReadAllText(full));
            anchors[path] = known;
        }

        var anchor = source[(hash + 1)..];
        return known.Contains(anchor) ? null : $"source: {path} has no heading with the anchor #{anchor}";
    }

    private static string? PathProblem(string path) =>
        path.Length == 0 || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Contains("..")
            ? $"'{path}' is not a path relative to the repository root"
            : null;

    [GeneratedRegex("^(?<area>[A-Z]+)-[0-9]{3}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex ShaPattern();
}
