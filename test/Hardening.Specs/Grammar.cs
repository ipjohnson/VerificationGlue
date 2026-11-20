namespace Hardening.Specs;

public enum Kind
{
    Bool,
    Int,
}

/// <summary>
/// A node of a generated expression. <see cref="Op"/> names the shape, <see cref="Id"/> numbers a probe
/// call, and <see cref="Value"/> holds the literal arguments of a leaf.
/// </summary>
public sealed record Node(string Op, Kind Kind, IReadOnlyList<Node> Children, int Id = 0, string Value = "")
{
    public int Size => 1 + Children.Sum(child => child.Size);

    public override string ToString() => Render(this, top: true);

    /// <summary>True when the leftmost part is a probe call, so every run records at least one event.</summary>
    public bool StartsWithCall => Op switch
    {
        "B" or "Fail" or "I" or "State" or "Bump" or "Await" or "Cancel" or "Length" or "TextPattern" or "TextNull" or "BoxPattern" or "TryOut" or "Coalesce" => true,
        "true" or "false" or "int" or "n++" => false,
        _ => Children[0].StartsWithCall,
    };

    /// <summary>
    /// True when the expression starts with a probe call and every divisor does too. A divisor made of
    /// literals could be the constant 0, which does not compile.
    /// </summary>
    public bool IsValid => StartsWithCall && DivisorsStartWithCall(this);

    private static bool DivisorsStartWithCall(Node node) =>
        (node is not { Op: "arith", Value: "/" } || node.Children[1].StartsWithCall) && node.Children.All(DivisorsStartWithCall);

    /// <summary>Numbers the probe calls 1, 2, 3 in source order.</summary>
    public Node Renumber()
    {
        var next = 0;
        return Walk(this);

        Node Walk(Node node)
        {
            var id = node.Id > 0 ? ++next : 0;
            return node with { Id = id, Children = [.. node.Children.Select(Walk)] };
        }
    }

    private static string Render(Node node, bool top)
    {
        var text = node.Op switch
        {
            "B" => $"p.B({node.Id}, {node.Value})",
            "true" or "false" => node.Op,
            "Fail" => $"p.Fail({node.Id})",
            "TextPattern" => $"p.S({node.Id}, {node.Value.Split('|')[0]}) is {{ Length: > {node.Value.Split('|')[1]} }}",
            "TextNull" => $"p.S({node.Id}, {node.Value}) is null",
            "BoxPattern" => $"p.O({node.Id}, {node.Value.Split('|')[0]}) is {{ V: {node.Value.Split('|')[1]} }}",
            "TryOut" => $"p.Try({node.Id}, {node.Value.Split('|')[0]}, out var o{node.Id}) && o{node.Id} > {node.Value.Split('|')[1]}",
            "!" => $"!{Render(node.Children[0], top: false)}",
            "&&" or "||" => $"{Render(node.Children[0], false)} {node.Op} {Render(node.Children[1], false)}",
            "?:" => $"{Render(node.Children[0], false)} ? {Render(node.Children[1], false)} : {Render(node.Children[2], false)}",
            "cmp" or "arith" => $"{Render(node.Children[0], false)} {node.Value} {Render(node.Children[1], false)}",
            "range" => $"{Render(node.Children[0], false)} is > {node.Value.Split('|')[0]} and < {node.Value.Split('|')[1]}",
            "I" => $"p.I({node.Id}, {node.Value})",
            "int" => node.Value,
            "State" => "p.State",
            "Bump" => $"p.Bump({node.Id})",
            "n++" => "n++",
            "Await" => $"await p.A({node.Id}, {node.Value})",
            "Cancel" => $"await p.Cancelled({node.Id})",
            "Length" => $"p.S({node.Id}, {node.Value.Split('|')[0]})?.Length ?? {node.Value.Split('|')[1]}",
            "Coalesce" => $"p.N({node.Id}, {node.Value}) ?? {Render(node.Children[0], false)}",
            _ => throw new InvalidOperationException($"Unknown node {node.Op}"),
        };

        return top || IsAtom(node) ? text : $"({text})";
    }

    private static bool IsAtom(Node node) => node.Op is "B" or "true" or "false" or "Fail" or "I" or "int" or "State" or "Bump";
}

/// <summary>
/// Generates expressions from the bounded grammar. Case <c>index</c> of a seed comes from its own
/// <see cref="Random"/>, so a replay regenerates one case without the others.
/// </summary>
public static class Grammar
{
    public const int MaxDepth = 4;

    private static readonly string[] Comparisons = ["==", "!=", "<", "<=", ">", ">="];
    private static readonly string[] Texts = ["null", "\"\"", "\"ab\"", "\"abc\""];

    public static ExpressionCase Generate(int seed, int index, int cases)
    {
        var random = new Random(unchecked((seed * 1_000_003) + index));
        var depth = 1 + (index * MaxDepth / Math.Max(1, cases));
        var builder = new Builder(random, async: random.NextDouble() < 0.25, local: random.NextDouble() < 0.2);
        return new ExpressionCase($"Case_{index}", builder.Bool(depth, leftmost: true).Renumber());
    }

    private sealed class Builder(Random random, bool async, bool local)
    {
        public Node Bool(int depth, bool leftmost)
        {
            if (depth <= 0 || random.NextDouble() < 0.25)
            {
                return BoolLeaf(leftmost);
            }

            return random.Next(6) switch
            {
                0 => new Node("!", Kind.Bool, [Bool(depth - 1, leftmost)]),
                1 => new Node("&&", Kind.Bool, [Bool(depth - 1, leftmost), Bool(depth - 1, false)]),
                2 => new Node("||", Kind.Bool, [Bool(depth - 1, leftmost), Bool(depth - 1, false)]),
                3 => new Node("?:", Kind.Bool, [Bool(depth - 1, leftmost), Bool(depth - 1, false), Bool(depth - 1, false)]),
                4 => new Node("cmp", Kind.Bool, [Int(depth - 1, leftmost), Int(depth - 1, false)], Value: Pick(Comparisons)),
                _ => Range(Int(depth - 1, leftmost), random.Next(3)),
            };
        }

        // An int range with no value in it does not compile, so the range always holds one value or more.
        private Node Range(Node value, int low) => new("range", Kind.Bool, [value], Value: $"{low}|{low + 2 + random.Next(2)}");

        public Node Int(int depth, bool leftmost)
        {
            if (depth <= 0 || random.NextDouble() < 0.3)
            {
                return IntLeaf(leftmost);
            }

            return random.Next(4) switch
            {
                0 => new Node("?:", Kind.Int, [Bool(depth - 1, leftmost), Int(depth - 1, false), Int(depth - 1, false)]),
                1 => new Node("Coalesce", Kind.Int, [Int(depth - 1, false)], 1, random.Next(3) == 0 ? "null" : $"{random.Next(4)}"),
                2 => new Node("arith", Kind.Int, [Int(depth - 1, leftmost), Int(depth - 1, leftmost: true)], Value: "/"),
                _ => new Node("arith", Kind.Int, [Int(depth - 1, leftmost), Int(depth - 1, false)], Value: random.Next(2) == 0 ? "+" : "-"),
            };
        }

        private Node BoolLeaf(bool leftmost)
        {
            while (true)
            {
                switch (random.Next(10))
                {
                    case 0 or 1 or 2 or 3:
                        return new Node("B", Kind.Bool, [], 1, random.Next(2) == 0 ? "true" : "false");
                    case 4 when !leftmost:
                        return new Node(random.Next(2) == 0 ? "true" : "false", Kind.Bool, []);
                    case 5:
                        return new Node("Fail", Kind.Bool, [], 1);
                    case 6:
                        return new Node("TextPattern", Kind.Bool, [], 1, $"{Pick(Texts)}|{random.Next(3)}");
                    case 7:
                        return new Node("TextNull", Kind.Bool, [], 1, Pick(Texts));
                    case 8:
                        return new Node("BoxPattern", Kind.Bool, [], 1, $"{random.Next(3)}|{random.Next(3)}");
                    case 9:
                        return new Node("TryOut", Kind.Bool, [], 1, $"{random.Next(3)}|{random.Next(2)}");
                }
            }
        }

        private Node IntLeaf(bool leftmost)
        {
            while (true)
            {
                switch (random.Next(10))
                {
                    case 0 or 1 or 2:
                        return new Node("I", Kind.Int, [], 1, $"{random.Next(4)}");
                    case 3 when !leftmost:
                        return new Node("int", Kind.Int, [], Value: $"{random.Next(4)}");
                    case 4:
                        return new Node("State", Kind.Int, []);
                    case 5:
                        return new Node("Bump", Kind.Int, [], 1);
                    case 6 or 7 when local && !leftmost:
                        return new Node("n++", Kind.Int, []);
                    case 6 or 7 when async:
                        return new Node("Await", Kind.Int, [], 1, $"{random.Next(4)}");
                    case 9 when async:
                        return new Node("Cancel", Kind.Int, [], 1);
                    case 8:
                        return new Node("Length", Kind.Int, [], 1, $"{Pick(Texts)}|{random.Next(3)}");
                }
            }
        }

        private string Pick(string[] items) => items[random.Next(items.Length)];
    }

    /// <summary>
    /// The expressions one step smaller than <paramref name="root"/>: each node replaced by one of its
    /// children of the same kind, or by a single probe call or literal. Each candidate is valid and
    /// renumbered.
    /// </summary>
    public static IEnumerable<Node> Reductions(Node root)
    {
        var seen = new HashSet<string> { root.ToString() };
        foreach (var candidate in Replace(root))
        {
            var renumbered = candidate.Renumber();
            if (renumbered.IsValid && Smaller(renumbered, root) && seen.Add(renumbered.ToString()))
            {
                yield return renumbered;
            }
        }
    }

    private static bool Smaller(Node candidate, Node root) =>
        candidate.Size < root.Size || (candidate.Size == root.Size && candidate.ToString().Length < root.ToString().Length);

    private static IEnumerable<Node> Replace(Node node)
    {
        foreach (var child in node.Children.Where(child => child.Kind == node.Kind))
        {
            yield return child;
        }

        foreach (var leaf in Leaves(node.Kind))
        {
            yield return leaf;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            foreach (var replaced in Replace(node.Children[i]))
            {
                var children = node.Children.ToArray();
                children[i] = replaced;
                yield return node with { Children = children };
            }
        }
    }

    private static IEnumerable<Node> Leaves(Kind kind) => kind == Kind.Bool
        ?
        [
            new Node("B", Kind.Bool, [], 1, "true"),
            new Node("B", Kind.Bool, [], 1, "false"),
            new Node("true", Kind.Bool, []),
            new Node("false", Kind.Bool, []),
        ]
        :
        [
            new Node("I", Kind.Int, [], 1, "0"),
            new Node("I", Kind.Int, [], 1, "1"),
            new Node("int", Kind.Int, [], Value: "0"),
            new Node("int", Kind.Int, [], Value: "1"),
        ];
}

/// <summary>
/// One expression under test. A generated case keeps its <see cref="Tree"/> for shrinking. A
/// regression case from the deterministic suite has only its text.
/// </summary>
public sealed record ExpressionCase(string Name, string Text, Node? Tree = null)
{
    public ExpressionCase(string name, Node tree)
        : this(name, tree.ToString(), tree)
    {
    }

    public bool IsAsync => Text.Contains("await ", StringComparison.Ordinal);

    public bool WritesLocal => Text.Contains("n++", StringComparison.Ordinal);

    /// <summary>The ids of the probe calls, which tell the parts that ran from the parts that were skipped.</summary>
    public IReadOnlyList<int> Ids { get; } =
        [.. System.Text.RegularExpressions.Regex.Matches(Text, @"p\.\w+\((\d+)").Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))];
}
