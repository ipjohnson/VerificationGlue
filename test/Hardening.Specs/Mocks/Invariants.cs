using System.Globalization;
using System.Text.RegularExpressions;

namespace Hardening.Specs.Mocks;

/// <summary>
/// The outcome of one invariant on one sequence: how many comparisons it made, and the failure, if
/// any. A run whose checks make no comparison shows that the check could not have failed.
/// </summary>
public sealed record InvariantResult(int Comparisons, string? Failure);

/// <summary>
/// The mock invariants of docs/design/hardening.md, Operations and sequences. Each runs a generated
/// sequence on the mocks, runs a changed sequence that the invariant says must give the same
/// observations, and compares the two runs. None of them uses the reference model.
/// </summary>
public static partial class Invariants
{
    public static readonly IReadOnlyList<(string Name, Func<Sequence, Random, Task<InvariantResult>> Check)> All =
    [
        ("Setup and verification do not add received calls", SetupAndVerificationAddNoCallsAsync),
        ("Verification does not consume response sequences", VerificationDoesNotConsumeSequencesAsync),
        ("Separate instances do not share configuration or recorded calls", InstancesShareNothingAsync),
        ("Reconfiguration follows the declared precedence rules", ReconfigurationFollowsPrecedenceAsync),
        ("Recursive mock identity and depth follow their contracts", RecursiveIdentityAndDepthAsync),
        ("Matchers and pending setup state follow the documented async context rules", ContextRulesAsync),
        ("Exceptions and abandoned setup state do not silently corrupt subsequent operations", FailuresDoNotCorruptAsync),
    ];

    /// <summary>
    /// Takes a snapshot of the mock before and after each setup or verification statement that
    /// finishes. The two snapshots must be equal. An NSubstitute-style setup whose call throws is a
    /// call, not a setup, and is left out.
    /// </summary>
    public static async Task<InvariantResult> SetupAndVerificationAddNoCallsAsync(Sequence sequence, Random random)
    {
        var ops = new List<Op>();
        var probes = new List<int>();
        foreach (var op in sequence.Ops)
        {
            if (op is SetupNs or SetupTyped or When or Check or CountCalls && SiteOf(op) is { } site)
            {
                var snapshot = new Snapshot(site.Target, site.OnSlot);
                probes.Add(ops.Count);
                ops.AddRange([snapshot, op, snapshot]);
            }
            else
            {
                ops.Add(op);
            }
        }

        var observations = await RunAsync(sequence with { Ops = ops });
        var spans = Spans(ops);
        var comparisons = 0;
        foreach (var index in probes)
        {
            var (before, statement, after) = (observations[spans[index]], observations[spans[index + 1]], observations[spans[index + 2]]);
            if (statement.Contains("throws", StringComparison.Ordinal) || before.EndsWith("is empty", StringComparison.Ordinal))
            {
                continue;
            }

            comparisons++;
            if (before != after)
            {
                return new InvariantResult(comparisons, $"{ops[index + 1]} gave {statement} and changed {before} to {after}");
            }
        }

        return new InvariantResult(comparisons, null);
    }

    /// <summary>
    /// Runs each check and call count twice. The second gives the same result as the first, and every
    /// other statement, including the calls that take values of a response sequence, gives the same
    /// observation.
    /// </summary>
    public static async Task<InvariantResult> VerificationDoesNotConsumeSequencesAsync(Sequence sequence, Random random)
    {
        var original = await RunAsync(sequence);
        var spans = Spans(sequence.Ops);
        var ops = new List<Op>();
        var expected = new List<string>();
        var comparisons = 0;
        for (var i = 0; i < sequence.Ops.Count; i++)
        {
            var op = sequence.Ops[i];
            ops.Add(op);
            expected.AddRange(Observations(original, spans, i));
            if (op is Check or CountCalls)
            {
                ops.Add(op);
                expected.AddRange(Observations(original, spans, i));
                comparisons++;
            }
        }

        return Compare(comparisons, expected, await RunAsync(sequence with { Ops = ops }), normalize: false);
    }

    /// <summary>
    /// Leaves out every statement that touches the second root mock, or a mock reached from it, also
    /// inside a child task, an interleaving or a parallel group. Each statement that is left gives the
    /// same observation as in the whole sequence. A parallel group that held statements of both mocks
    /// gives one observation for all of them, so it is not compared.
    /// </summary>
    public static async Task<InvariantResult> InstancesShareNothingAsync(Sequence sequence, Random random)
    {
        var original = await RunAsync(sequence);
        var spans = Spans(sequence.Ops);
        var ops = new List<Op>();
        var expected = new List<string?>();
        var left = 0;
        static bool Kept(Op inner) => !Touches(inner);
        for (var i = 0; i < sequence.Ops.Count; i++)
        {
            var op = sequence.Ops[i];
            var observed = Observations(original, spans, i);
            switch (op)
            {
                case ChildTask child:
                    var kept = Enumerable.Range(0, child.Ops.Count).Where(j => Kept(child.Ops[j])).ToList();
                    var inner = Spans(child.Ops);
                    ops.Add(new ChildTask([.. kept.Select(j => child.Ops[j])]));
                    expected.AddRange(kept.SelectMany(j => observed.GetRange(inner[j], inner[j + 1] - inner[j])));
                    break;
                case Interleave interleave when Kept(interleave.A) != Kept(interleave.B):
                    ops.Add(Kept(interleave.A) ? interleave.A : interleave.B);
                    expected.Add(Kept(interleave.A) ? observed[0] : observed[1]["other flow: ".Length..]);
                    break;
                case ParallelCalls parallel when !Kept(parallel):
                    var branches = parallel.Branches.Select(branch => (IReadOnlyList<Op>)[.. branch.Where(Kept)]).Where(branch => branch.Count > 0).ToList();
                    if (branches.Count > 0)
                    {
                        ops.Add(new ParallelCalls(branches));
                        expected.Add(null);
                    }

                    break;
                case ParallelSetups setups when !Kept(setups):
                    var setupBranches = setups.Branches.Where(Kept).ToList();
                    if (setupBranches.Count > 0)
                    {
                        ops.Add(new ParallelSetups(setupBranches));
                        expected.Add(null);
                    }

                    break;
                default:
                    if (Kept(op))
                    {
                        ops.Add(op);
                        expected.AddRange(observed);
                    }

                    break;
            }

            left += Kept(op) ? 0 : 1;
        }

        var actual = await RunAsync(sequence with { Ops = ops });
        if (actual.Count != expected.Count)
        {
            return new InvariantResult(left, string.Create(CultureInfo.InvariantCulture, $"expected {expected.Count} observations, got {actual.Count}"));
        }

        var compared = Enumerable.Range(0, expected.Count).Where(i => expected[i] is not null).ToList();
        return Compare(left, [.. compared.Select(i => expected[i]!)], [.. compared.Select(i => actual[i])], normalize: true);
    }

    /// <summary>
    /// After the sequence, sets up one call four times: typed, NSubstitute-style, then with wildcards
    /// in each style. After each setup that finishes, the call gives that setup's value, unless a
    /// callback the sequence added throws first.
    /// </summary>
    public static async Task<InvariantResult> ReconfigurationFollowsPrecedenceAsync(Sequence sequence, Random random)
    {
        var member = new[] { Member.Get2, Member.Get1, Member.Name }[random.Next(3)];
        var (count, strings) = Inputs.ParametersOf(member);
        var exact = new Site(member, 0, [.. Enumerable.Range(0, count).Select(_ => ArgSpec.Exact(strings ? "a" : (1 + random.Next(3)).ToString(CultureInfo.InvariantCulture)))]);
        var any = exact with { Args = [.. exact.Args.Select(_ => ArgSpec.Any)] };
        var values = strings ? new[] { "v1", "v2", "v3", "v4" } : ["901", "902", "903", "904"];
        Op[] probes =
        [
            new SetupTyped(exact, new Response(ResponseKind.Value, [values[0]])), new Invoke(exact),
            new SetupNs(exact, new Response(ResponseKind.Value, [values[1]])), new Invoke(exact),
            new SetupTyped(any, new Response(ResponseKind.Value, [values[2]])), new Invoke(exact),
            new SetupNs(any, new Response(ResponseKind.Value, [values[3]])), new Invoke(exact),
        ];
        var observations = await RunAsync(sequence with { Ops = [.. sequence.Ops, .. probes] });
        var tail = observations.TakeLast(probes.Length).ToList();
        var throwing = Flattened(sequence.Ops).OfType<When>().Where(when => when.Throw).Select(when => "throws r" + when.Callback.Id.ToString(CultureInfo.InvariantCulture)).ToHashSet();
        string? newest = null;
        for (var i = 0; i < probes.Length; i += 2)
        {
            if (tail[i] == "set up")
            {
                newest = Inputs.Show(Inputs.ParseResult(member, values[i / 2]));
            }

            var answer = tail[i + 1].StartsWith('[') ? tail[i + 1][(tail[i + 1].IndexOf("] ", StringComparison.Ordinal) + 2)..] : tail[i + 1];
            if (answer != newest && !throwing.Contains(answer))
            {
                return new InvariantResult(i / 2 + 1, $"after {probes[i]} gave {tail[i]}, {probes[i + 1]} gave {tail[i + 1]}, not {newest}");
            }
        }

        return new InvariantResult(probes.Length / 2, null);
    }

    /// <summary>
    /// After the sequence, reaches a recursive member twice in each way. Each access gives the same
    /// mock, and the member of a mock at the default depth of 2 gives null.
    /// </summary>
    public static async Task<InvariantResult> RecursiveIdentityAndDepthAsync(Sequence sequence, Random random)
    {
        var key = ArgSpec.Exact((1 + random.Next(3)).ToString(CultureInfo.InvariantCulture));
        var child = new Site(Member.Child, 0, [key]);
        Op[] probes =
        [
            new Invoke(child), new Invoke(child), new Fetch(0, child),
            new Fetch(2, new Site(Member.ChildLeaf, 0, [])), new Invoke(new Site(Member.ChildLeaf, 0, [])),
            new Invoke(new Site(Member.LeafNext, 2, [])),
        ];
        var tail = (await RunAsync(sequence with { Ops = [.. sequence.Ops, .. probes] })).TakeLast(probes.Length).Select(Answer).ToList();
        if (tail[0].StartsWith("throws", StringComparison.Ordinal))
        {
            return new InvariantResult(0, null);
        }

        (string, string)[] pairs = [(tail[0], tail[1]), (tail[0], tail[2].Replace("slot0 = ", "", StringComparison.Ordinal)), (tail[3].Replace("slot2 = ", "", StringComparison.Ordinal), tail[4])];
        foreach (var (first, second) in pairs)
        {
            if (first != second && !first.StartsWith("throws", StringComparison.Ordinal))
            {
                return new InvariantResult(1, $"the same access gave {first} and then {second}: {string.Join(" / ", tail)}");
            }
        }

        return tail[3].StartsWith("slot2 = m", StringComparison.Ordinal) && tail[5] != "null"
            ? new InvariantResult(2, $"a member beyond the depth gave {tail[5]}")
            : new InvariantResult(2, null);
    }

    /// <summary>
    /// Moves state between flows only in ways the CD-04 rules say change nothing: a statement another
    /// flow ran before a mock call runs before the whole statement, a blocking interleave awaits and
    /// an awaited one blocks, a child task's statements and a setup finished in a child run in this
    /// flow, a setup's await is removed, and a matcher abandoned in a child task is left out.
    /// </summary>
    public static async Task<InvariantResult> ContextRulesAsync(Sequence sequence, Random random)
    {
        var original = await RunAsync(sequence);
        var spans = Spans(sequence.Ops);
        var ops = new List<Op>();
        var expected = new List<string>();
        var comparisons = 0;
        for (var i = 0; i < sequence.Ops.Count; i++)
        {
            var observed = Observations(original, spans, i);
            switch (sequence.Ops[i])
            {
                case Interleave { At: Point.BeforeCall } interleave:
                    ops.AddRange([interleave.B, interleave.A]);
                    expected.AddRange([observed[1]["other flow: ".Length..], observed[0]]);
                    break;
                case Interleave interleave:
                    ops.Add(interleave with { Await = !interleave.Await });
                    expected.AddRange(observed);
                    break;
                case ChildTask child:
                    ops.AddRange(child.Ops);
                    expected.AddRange(observed);
                    break;
                case FinishInChild finish:
                    ops.Add(finish.Setup);
                    expected.AddRange(observed);
                    break;
                case SetupNs { Await: true } setup:
                    ops.Add(setup with { Await = false });
                    expected.AddRange(observed);
                    break;
                case AbandonInChild:
                    break;
                default:
                    ops.Add(sequence.Ops[i]);
                    expected.AddRange(observed);
                    continue;
            }

            comparisons++;
        }

        return Compare(comparisons, expected, await RunAsync(sequence with { Ops = ops }), normalize: true);
    }

    /// <summary>
    /// Adds statements that fail and leave nothing behind: a check that fails, and a matcher abandoned
    /// in a child task. Each failure is reported, and every other statement gives the same observation.
    /// </summary>
    public static async Task<InvariantResult> FailuresDoNotCorruptAsync(Sequence sequence, Random random)
    {
        var original = await RunAsync(sequence);
        var spans = Spans(sequence.Ops);
        var ops = new List<Op>();
        var expected = new List<string>();
        var inserted = new List<int>();
        for (var i = 0; i <= sequence.Ops.Count; i++)
        {
            if (random.Next(3) == 0)
            {
                Op failure = random.Next(2) == 0
                    ? new Check(new Site(Member.Get1, random.Next(2), [ArgSpec.Exact("77")]), 1, int.MaxValue)
                    : new AbandonInChild();
                inserted.Add(ops.Count);
                ops.Add(failure);
                expected.Add(failure is Check ? "check fails: received 0" : "abandoned a matcher in a child task");
            }

            if (i < sequence.Ops.Count)
            {
                ops.Add(sequence.Ops[i]);
                expected.AddRange(Observations(original, spans, i));
            }
        }

        var actual = (await RunAsync(sequence with { Ops = ops })).ToList();
        var changed = Spans(ops);
        foreach (var index in inserted.Where(index => ops[index] is Check))
        {
            var text = actual[changed[index]];
            actual[changed[index]] = text.StartsWith("check fails: received 0,", StringComparison.Ordinal) ? "check fails: received 0" : text;
        }

        return Compare(inserted.Count, expected, actual, normalize: false);
    }

    private static async Task<IReadOnlyList<string>> RunAsync(Sequence sequence) =>
        await Interpreter.RunAsync(sequence).WaitAsync(MockCampaign.Timeout);

    /// <summary>The index of the first observation of each op. An await gives none, and the others give one per statement they run in this order.</summary>
    private static int[] Spans(IReadOnlyList<Op> ops)
    {
        var starts = new int[ops.Count + 1];
        for (var i = 0; i < ops.Count; i++)
        {
            starts[i + 1] = starts[i] + Count(ops[i]);
        }

        return starts;
    }

    private static int Count(Op op) => op switch
    {
        Yield => 0,
        ChildTask child => child.Ops.Sum(Count),
        Interleave => 2,
        _ => 1,
    };

    private static List<string> Observations(IReadOnlyList<string> observations, int[] spans, int index) =>
        [.. observations.Skip(spans[index]).Take(spans[index + 1] - spans[index])];

    private static InvariantResult Compare(int comparisons, IReadOnlyList<string> expected, IReadOnlyList<string> actual, bool normalize)
    {
        var left = normalize ? Normalize(expected) : expected;
        var right = normalize ? Normalize(actual) : actual;
        for (var i = 0; i < Math.Min(left.Count, right.Count); i++)
        {
            if (left[i] != right[i])
            {
                return new InvariantResult(comparisons, string.Create(CultureInfo.InvariantCulture, $"observation {i}: expected {left[i]}, got {right[i]}"));
            }
        }

        return left.Count == right.Count
            ? new InvariantResult(comparisons, null)
            : new InvariantResult(comparisons, string.Create(CultureInfo.InvariantCulture, $"expected {left.Count} observations, got {right.Count}"));
    }

    /// <summary>Mock labels follow the order mocks are first seen, which leaving out or moving a statement changes.</summary>
    private static List<string> Normalize(IReadOnlyList<string> observations) => [.. observations.Select(text => Label().Replace(text, "m"))];

    [GeneratedRegex(@"\bm\d+\b")]
    private static partial Regex Label();

    private static string Answer(string observation) =>
        observation.StartsWith('[') ? observation[(observation.IndexOf("] ", StringComparison.Ordinal) + 2)..] : observation;

    private static Site? SiteOf(Op op) => op switch
    {
        SetupNs setup => setup.Site,
        SetupTyped typed => typed.Site,
        When select => select.Site,
        Check check => check.Site,
        CountCalls count => count.Site,
        _ => null,
    };

    private static IEnumerable<Op> Flattened(IEnumerable<Op> ops) => ops.SelectMany(op => Flattened(op.Inner).Prepend(op));

    /// <summary>True when an op touches the second root mock, or a mock reached from it.</summary>
    private static bool Touches(Op op)
    {
        static bool Site(Site site) => SequenceGenerator.OwnerOf(site) == 1;
        static bool Response(Response? response) => response is { Kind: ResponseKind.SameAs } && SequenceGenerator.OwnerOf(response.Id) == 1;

        return op switch
        {
            Invoke invoke => Site(invoke.Site),
            SetupNs setup => Site(setup.Site) || Response(setup.Response),
            SetupTyped typed => Site(typed.Site) || Response(typed.Response),
            When select => Site(select.Site),
            Check check => Site(check.Site),
            CountCalls count => Site(count.Site),
            Snapshot snapshot => (snapshot.Slot ? SequenceGenerator.OwnerOf(snapshot.Target) : snapshot.Target) == 1,
            Fetch fetch => Site(fetch.Site),
            SetProperty set => set.Mock == 1,
            Subscribe subscribe => subscribe.Mock == 1,
            Unsubscribe unsubscribe => unsubscribe.Mock == 1,
            RaiseEvent raise => raise.Mock == 1,
            FinishInChild finish => Touches(finish.Setup),
            _ => op.Inner.Any(Touches),
        };
    }
}
