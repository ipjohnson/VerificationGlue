using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Mocks.Generator;

/// <summary>
/// Finds the mocks that unconfigured members return. The walk starts from every discovered type at
/// level 0 and follows <see cref="MockMemberModel.MockReturn"/> breadth first, so each mock class
/// gets the lowest level any discovered type reaches it at. A member of a class at a level below
/// the depth returns a mock. A member of a class at the depth returns <c>default</c>, and VGM0007
/// reports it. The generator and the analyzer share this walk, so they agree on every member.
/// </summary>
internal sealed class RecursiveMocks
{
    /// <summary>The default of <c>VerificationGlueMocksRecursionDepth</c>, set from the shape sweep.</summary>
    public const int DefaultDepth = 2;

    public const string DepthOption = "build_property.VerificationGlueMocksRecursionDepth";

    private readonly Dictionary<string, Node?> _byName = new(System.StringComparer.Ordinal);
    private readonly List<Node> _nodes = new();

    private RecursiveMocks(int depth)
    {
        Depth = depth;
    }

    public int Depth { get; }

    /// <summary>Every mock class to generate, in walk order.</summary>
    public IReadOnlyList<Node> Nodes => _nodes;

    /// <summary>The depth from <c>VerificationGlueMocksRecursionDepth</c>, or the default when it is missing or not a whole number.</summary>
    public static int ReadDepth(AnalyzerConfigOptions options) =>
        options.TryGetValue(DepthOption, out var text) &&
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var depth)
            ? depth
            : DefaultDepth;

    /// <param name="roots">The discovered types. Refused ones are left out.</param>
    /// <param name="depth">The number of steps the walk takes from a discovered type.</param>
    /// <param name="compilation">The compilation that resolves each return type.</param>
    public static RecursiveMocks Walk(IEnumerable<MockModel> roots, int depth, Compilation compilation)
    {
        var walk = new RecursiveMocks(depth);
        foreach (var root in roots.OrderBy(model => model.MockName, System.StringComparer.Ordinal))
        {
            if (!root.IsRefused && !walk._byName.ContainsKey(root.MockName))
            {
                walk.Add(new Node(root, 0, root.MockName));
            }
        }

        for (var i = 0; i < walk._nodes.Count; i++)
        {
            var node = walk._nodes[i];
            if (node.Level >= depth)
            {
                continue;
            }

            foreach (var member in node.Model.Members)
            {
                if (member.MockReturn is not { } target || walk._byName.ContainsKey(target.MockName))
                {
                    continue;
                }

                var model = Resolve(target, compilation) is { } type ? MockModelBuilder.Build(type, compilation) : null;
                if (model is null || model.IsRefused)
                {
                    walk._byName[target.MockName] = null;
                    continue;
                }

                walk.Add(new Node(model, node.Level + 1, node.Root));
            }
        }

        return walk;
    }

    /// <summary>The model to emit for a node, with <see cref="MockMemberModel.ReturnsMock"/> set on each member that returns a mock.</summary>
    public MockModel Finish(Node node)
    {
        if (node.Level >= Depth || !node.Model.Members.Any(ReturnsMock))
        {
            return node.Model;
        }

        return node.Model with
        {
            Members = node.Model.Members.Select(member => ReturnsMock(member) ? member with { ReturnsMock = true } : member).ToArray(),
        };
    }

    /// <summary>The members beyond the depth that would otherwise return a mock: those of a class at the depth whose return type can be mocked.</summary>
    public IEnumerable<(Node Node, MockMemberModel Member)> BeyondDepth() =>
        _nodes
            .Where(node => node.Level >= Depth)
            .SelectMany(node => node.Model.Members.Where(member => member.MockReturn != null).Select(member => (node, member)));

    private bool ReturnsMock(MockMemberModel member) =>
        member.MockReturn is { } target && _byName.TryGetValue(target.MockName, out var node) && node != null;

    private void Add(Node node)
    {
        _byName[node.Model.MockName] = node;
        _nodes.Add(node);
    }

    private static INamedTypeSymbol? Resolve(MockReturnModel target, Compilation compilation)
    {
        foreach (var symbol in DocumentationCommentId.GetSymbolsForReferenceId(target.ReferenceId, compilation))
        {
            if (symbol is INamedTypeSymbol named && named.ContainingAssembly?.Identity.GetDisplayName() == target.Assembly)
            {
                return named;
            }
        }

        return null;
    }

    /// <param name="Model">The mock class's model.</param>
    /// <param name="Level">0 for a discovered type, and one more for each step of the walk.</param>
    /// <param name="Root">The mock name of the discovered type whose walk reached this class first.</param>
    internal sealed record Node(MockModel Model, int Level, string Root);
}
