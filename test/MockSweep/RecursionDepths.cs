using System.Text;
using Microsoft.CodeAnalysis;
using VerificationGlue.Mocks.Generator;

namespace MockSweep;

/// <summary>What the recursive mock walk adds when a test mocks one swept interface, at one depth.</summary>
/// <param name="Depth">The recursion depth.</param>
/// <param name="Interfaces">The swept interfaces that could be mocked.</param>
/// <param name="Reaching">The interfaces whose walk adds at least one mock.</param>
/// <param name="AddedMocks">The mocks the walks add, summed over the interfaces.</param>
/// <param name="AddedLines">The generated lines those mocks add, summed over the interfaces.</param>
/// <param name="MostMocks">The most mocks one interface's walk adds.</param>
/// <param name="MostLines">The most generated lines one interface's walk adds.</param>
/// <param name="MostType">The interface whose walk adds the most mocks.</param>
/// <param name="P95Lines">The generated lines added for the interface at the 95th percentile of those whose walk adds a mock.</param>
public sealed record DepthRow(int Depth, int Interfaces, int Reaching, int AddedMocks, long AddedLines, int MostMocks, long MostLines, string MostType, long P95Lines);

/// <summary>
/// Measures the recursive mock walk for each swept interface as if a test named only that
/// interface. One walk at the largest depth gives every smaller depth too, because each class's
/// level is its shortest distance from the interface.
/// </summary>
public static class RecursionDepths
{
    public const int Largest = 4;

    public static IReadOnlyList<DepthRow> Measure(Compilation compilation, IEnumerable<INamedTypeSymbol> interfaces)
    {
        var lines = new Dictionary<string, int>(StringComparer.Ordinal);
        var walks = new List<(string Type, int[] Mocks, long[] Lines)>();
        foreach (var type in interfaces)
        {
            var root = MockModelBuilder.Build(type, compilation);
            if (root.IsRefused)
            {
                continue;
            }

            var mocks = new int[Largest + 1];
            var added = new long[Largest + 1];
            foreach (var node in RecursiveMocks.Walk([root], Largest, compilation).Nodes.Where(node => node.Level > 0))
            {
                if (!lines.TryGetValue(node.Model.MockName, out var count))
                {
                    count = MockEmitter.Emit(node.Model).Count(c => c == '\n');
                    lines[node.Model.MockName] = count;
                }

                for (var depth = node.Level; depth <= Largest; depth++)
                {
                    mocks[depth]++;
                    added[depth] += count;
                }
            }

            walks.Add((root.TypeName, mocks, added));
        }

        return Enumerable.Range(1, Largest)
            .Select(depth =>
            {
                var most = walks.OrderByDescending(w => w.Mocks[depth]).ThenBy(w => w.Type, StringComparer.Ordinal).FirstOrDefault();
                var reaching = walks.Where(w => w.Mocks[depth] > 0).Select(w => w.Lines[depth]).Order().ToList();
                return new DepthRow(
                    depth,
                    walks.Count,
                    walks.Count(w => w.Mocks[depth] > 0),
                    walks.Sum(w => w.Mocks[depth]),
                    walks.Sum(w => w.Lines[depth]),
                    most.Mocks?[depth] ?? 0,
                    walks.Count == 0 ? 0 : walks.Max(w => w.Lines[depth]),
                    most.Type ?? "",
                    reaching.Count == 0 ? 0 : reaching[(int)Math.Ceiling(reaching.Count * 0.95) - 1]);
            })
            .ToList();
    }

    public static string Markdown(IReadOnlyList<DepthRow> rows)
    {
        var text = new StringBuilder();
        text.AppendLine("## Recursive mocks by depth");
        text.AppendLine();
        text.AppendLine($"Each swept interface is walked as if a test named only that interface. The generator's default depth is {RecursiveMocks.DefaultDepth}.");
        text.AppendLine();
        text.AppendLine("| Depth | Interfaces | Walk adds a mock | Mocks added | Lines added | Lines for one interface, 95th percentile | Most mocks for one interface | Most lines for one interface |");
        text.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var row in rows)
        {
            text.AppendLine($"| {row.Depth} | {row.Interfaces} | {row.Reaching} | {row.AddedMocks} | {row.AddedLines} | {row.P95Lines} | {row.MostMocks} (`{row.MostType}`) | {row.MostLines} |");
        }

        return text.ToString();
    }
}
