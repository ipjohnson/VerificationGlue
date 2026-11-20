using System.Text;

namespace MockSweep;

/// <summary>
/// The expected outcome of each swept interface, checked in as
/// <c>test/MockSweep/classifications.tsv</c>. Each line is <c>status, source, type</c> separated by tabs.
/// </summary>
public static class SweepClassifications
{
    public const string Compiled = "compiled";

    public const string CompiledWithThrowingMembers = "compiled with VGM0003";

    public const string Refused = "refused";

    public const string Failed = "failed";

    /// <summary>Statuses from best to worst. A move to a later one is a regression.</summary>
    private static readonly string[] Order = [Compiled, CompiledWithThrowingMembers, Refused, Failed];

    public static string Of(MockOutcome outcome) =>
        outcome.Failed ? Failed : outcome.Refused ? Refused : outcome.Diagnostics.Count > 0 ? CompiledWithThrowingMembers : Compiled;

    public static IReadOnlyDictionary<(string Source, string Type), string> Of(IEnumerable<SourceResult> sources) =>
        sources
            .SelectMany(s => s.Outcomes.Select(o => (Key: (s.Source, o.TypeName), Status: Of(o))))
            .ToDictionary(row => row.Key, row => row.Status);

    public static string Write(IReadOnlyDictionary<(string Source, string Type), string> classifications)
    {
        var text = new StringBuilder();
        text.Append("# The expected outcome of each interface the mock shape sweep mocks. docs/design/mocks-shape-sweep.md#pinned-classifications\n");
        text.Append("# says how to update it. Each line is: status, source, type.\n");
        foreach (var ((source, type), status) in classifications
            .OrderBy(pair => pair.Key.Source, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Type, StringComparer.Ordinal))
        {
            text.Append(status).Append('\t').Append(source).Append('\t').Append(type).Append('\n');
        }

        return text.ToString();
    }

    public static IReadOnlyDictionary<(string Source, string Type), string> Read(string text)
    {
        var classifications = new Dictionary<(string Source, string Type), string>();
        foreach (var line in text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0 && !l.StartsWith('#')))
        {
            var parts = line.Split('\t');
            if (parts.Length != 3 || !Order.Contains(parts[0]))
            {
                throw new FormatException($"The classifications line '{line}' is not 'status, source, type' separated by tabs, with a status of {string.Join(", ", Order)}.");
            }

            classifications.Add((parts[1], parts[2]), parts[0]);
        }

        return classifications;
    }

    public static ClassificationChanges Compare(
        IReadOnlyDictionary<(string Source, string Type), string> expected,
        IReadOnlyDictionary<(string Source, string Type), string> actual)
    {
        var regressions = new List<ClassificationChange>();
        var improvements = new List<ClassificationChange>();
        var added = new List<ClassificationChange>();
        var removed = new List<ClassificationChange>();
        foreach (var key in expected.Keys.Union(actual.Keys).OrderBy(k => k.Source, StringComparer.Ordinal).ThenBy(k => k.Type, StringComparer.Ordinal))
        {
            var was = expected.GetValueOrDefault(key);
            var now = actual.GetValueOrDefault(key);
            var change = new ClassificationChange(key.Source, key.Type, was, now);
            if (was is null)
            {
                added.Add(change);
            }
            else if (now is null)
            {
                removed.Add(change);
            }
            else if (Array.IndexOf(Order, now) > Array.IndexOf(Order, was))
            {
                regressions.Add(change);
            }
            else if (now != was)
            {
                improvements.Add(change);
            }
        }

        return new ClassificationChanges(regressions, improvements, added, removed);
    }
}

/// <summary>One interface whose outcome differs from its pinned one. A null status means the interface is not in that set.</summary>
public sealed record ClassificationChange(string Source, string Type, string? Expected, string? Actual)
{
    public override string ToString() => $"{Source}: {Type}, {Expected ?? "not swept"} -> {Actual ?? "not swept"}";
}

/// <summary>
/// The differences from the pinned classifications. Changes to the input set, interfaces added or
/// removed, are kept apart from changes in outcome, so a new package cannot hide a regression.
/// </summary>
public sealed record ClassificationChanges(
    IReadOnlyList<ClassificationChange> Regressions,
    IReadOnlyList<ClassificationChange> Improvements,
    IReadOnlyList<ClassificationChange> Added,
    IReadOnlyList<ClassificationChange> Removed)
{
    public bool None => Regressions.Count == 0 && Improvements.Count == 0 && Added.Count == 0 && Removed.Count == 0;
}
