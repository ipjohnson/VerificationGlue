using System.Globalization;

namespace Hardening.Specs.Mocks;

/// <summary>
/// Generates legal operation sequences over the factors of docs/design/hardening.md, Operations and
/// sequences: setup style, matching, response, member, async result, verification, identity and
/// context. Every ordering it makes is legal under the CD-04 rules in docs/design/mocks.md. Case
/// <c>i</c> of a seed comes from its own <see cref="Random"/>, so a replay regenerates one case.
/// </summary>
public static class SequenceGenerator
{
    public const int MinOps = 4;

    public const int MaxOps = 24;

    /// <summary>Slots 0 and 1 hold mocks of <see cref="IChild"/>, and slots 2 and 3 hold mocks of <see cref="ILeaf"/>.</summary>
    public static bool IsChildSlot(int slot) => slot < 2;

    /// <summary>The root mock a slot's mock is reached from: slots 0 and 2 from root 0, and slots 1 and 3 from root 1.</summary>
    public static int OwnerOf(int slot) => slot % 2;

    /// <summary>The root mock a site's mock is, or is reached from.</summary>
    public static int OwnerOf(Site site) => site.OnSlot ? OwnerOf(site.Target) : site.Target;

    private static readonly Member[] s_rootMembers =
        [Member.Get2, Member.Get1, Member.Name, Member.EchoInt, Member.EchoString, Member.Index, Member.Count, Member.Load, Member.Peek, Member.Reset, Member.TryGet, Member.Swap, Member.Child];

    private static readonly Member[] s_parallelMembers =
        [Member.Get2, Member.Get1, Member.Name, Member.EchoInt, Member.Index, Member.Count, Member.Load, Member.Peek, Member.Reset, Member.TryGet, Member.Swap];

    public static Sequence Generate(int seed, int index, int cases)
    {
        var random = new Random(unchecked((seed * 1_000_003) + index));
        var length = MinOps + ((MaxOps - MinOps) * index / Math.Max(1, cases - 1));
        var builder = new Builder(random);
        var ops = new List<Op>();
        for (var i = 0; i < length; i++)
        {
            ops.Add(builder.Next());
        }

        return new Sequence(string.Create(CultureInfo.InvariantCulture, $"Case_{index}"), ops);
    }

    /// <summary>
    /// The one-step reductions of a sequence: drop an op, run a group's statements in this flow one
    /// after another, or drop a statement inside a group. Each keeps the sequence legal.
    /// </summary>
    public static IEnumerable<Sequence> Reductions(Sequence sequence)
    {
        var ops = sequence.Ops;
        for (var i = 0; i < ops.Count; i++)
        {
            yield return With(sequence, [.. ops.Take(i), .. ops.Skip(i + 1)]);
        }

        for (var i = 0; i < ops.Count; i++)
        {
            var flattened = ops[i] switch
            {
                FinishInChild finish => [finish.Setup],
                Interleave interleave => (IReadOnlyList<Op>)[interleave.A, interleave.B],
                _ => ops[i].Inner,
            };
            if (flattened.Count > 0)
            {
                yield return With(sequence, [.. ops.Take(i), .. flattened, .. ops.Skip(i + 1)]);
            }

            foreach (var smaller in Smaller(ops[i]))
            {
                yield return With(sequence, [.. ops.Take(i), smaller, .. ops.Skip(i + 1)]);
            }
        }
    }

    private static Sequence With(Sequence sequence, IReadOnlyList<Op> ops) => sequence with { Ops = ops };

    private static IEnumerable<Op> Smaller(Op op)
    {
        switch (op)
        {
            case ChildTask { Ops.Count: > 1 } child:
                for (var i = 0; i < child.Ops.Count; i++)
                {
                    yield return new ChildTask([.. child.Ops.Take(i), .. child.Ops.Skip(i + 1)]);
                }

                break;
            case ParallelCalls { Branches.Count: > 1 } parallel:
                for (var i = 0; i < parallel.Branches.Count; i++)
                {
                    yield return new ParallelCalls([.. parallel.Branches.Take(i), .. parallel.Branches.Skip(i + 1)]);
                }

                break;
            case ParallelSetups { Branches.Count: > 1 } setups:
                for (var i = 0; i < setups.Branches.Count; i++)
                {
                    yield return new ParallelSetups([.. setups.Branches.Take(i), .. setups.Branches.Skip(i + 1)]);
                }

                break;
        }
    }

    /// <summary>
    /// Builds the ops of one sequence. Most sites use the few members the sequence focuses on, and
    /// half the calls and checks reuse a site an earlier statement used, so setups, calls and checks
    /// often meet on the same call.
    /// </summary>
    private sealed class Builder(Random random)
    {
        private readonly HashSet<int> _slots = [];
        private readonly List<Member> _focus = [.. s_rootMembers.OrderBy(_ => random.Next()).Take(3)];
        private readonly List<Site> _recent = [];
        private int _ids;

        public Op Next() => Pick(
            (24, Invoke),
            (12, SetupNs),
            (10, SetupTyped),
            (5, When),
            (8, Check),
            (5, CountCalls),
            (5, Snapshot),
            (5, Fetch),
            (3, SetProperty),
            (4, Event),
            (2, () => new Yield()),
            (3, Child),
            (2, Finish),
            (2, () => new AbandonInChild()),
            (6, Interleave),
            (3, Parallel),
            (2, ParallelSetups));

        private Op Invoke() => new Invoke(Reused(Style.Invoke) ?? Site(Style.Invoke, Members(Style.Invoke)));

        /// <summary>
        /// Half the time, a site an earlier statement used, with each argument this style cannot
        /// write turned into a value the earlier argument matches.
        /// </summary>
        private Site? Reused(Style style)
        {
            if (_recent.Count == 0 || random.Next(2) == 0)
            {
                return null;
            }

            var site = _recent[random.Next(_recent.Count)];
            var (_, strings) = Inputs.ParametersOf(site.Member);
            return site with
            {
                Args = [.. site.Args.Select((arg, i) =>
                    site.Member == Member.TryGet && i == 1 ? Arg(style, site.Member, i, strings) :
                    Kinds(style, site.Member, i).Contains(arg.Kind) ? arg :
                    ArgSpec.Exact(Instance(arg, strings)))],
            };
        }

        /// <summary>A value that <paramref name="arg"/> matches.</summary>
        private string? Instance(ArgSpec arg, bool strings) => arg.Kind switch
        {
            ArgKind.Exact or ArgKind.IsValue => arg.Value,
            ArgKind.Any => strings ? String() : Int(),
            _ => arg.Pred switch
            {
                Pred.Even => random.Next(3) == 0 ? "0" : "2",
                Pred.AboveOne => random.Next(2) == 0 ? "2" : "3",
                _ => random.Next(2) == 0 ? null : "",
            },
        };

        private Op SetupNs() => SetupNs(Site(Style.NSubstitute, Members(Style.NSubstitute)), random.Next(4) == 0);

        private SetupNs SetupNs(Site site, bool await = false) => new(site, Response(site, typed: false), await);

        private Op SetupTyped()
        {
            var site = Site(Style.Typed, Members(Style.Typed));
            var parameters = Inputs.ParametersOf(site.Member).Count;
            var does = random.Next(3) == 0 ? new Callback(++_ids, parameters > 0 && random.Next(3) == 0) : null;
            var response = Inputs.ResultOf(site.Member) == Inputs.Result.Void
                ? random.Next(2) == 0 ? new Response(ResponseKind.Throws, [], ++_ids) : null
                : does is null || random.Next(3) > 0 ? Response(site, typed: true) : null;
            return new SetupTyped(site, response, does ?? (response is null ? new Callback(++_ids) : null));
        }

        private Op When()
        {
            var site = Site(Style.NSubstitute, [Member.Get2, Member.Get1, Member.Name, Member.EchoInt, Member.Load, Member.Reset, Member.TryGet, Member.Swap, .. SlotMembers(Member.ChildValue, Member.LeafValue)]);
            var throws = random.Next(4) == 0;
            return new When(site, new Callback(++_ids, !throws && random.Next(3) == 0), throws);
        }

        private Op Check()
        {
            var site = Reused(Style.Check) ?? Site(Style.Check, Members(Style.Check));
            var count = random.Next(4);
            return random.Next(4) switch
            {
                0 => new Check(site, 1, int.MaxValue),
                1 => new Check(site, 0, 0),
                _ => new Check(site, count, count),
            };
        }

        private Op CountCalls() => new CountCalls(Reused(Style.Typed) ?? Site(Style.Typed, Members(Style.Check)));

        private Op Snapshot()
        {
            var slots = _slots.ToList();
            return slots.Count > 0 && random.Next(3) == 0 ? new Snapshot(slots[random.Next(slots.Count)], true) : new Snapshot(Root(), false);
        }

        private Op Fetch()
        {
            var children = _slots.Where(IsChildSlot).ToList();
            if (children.Count > 0 && random.Next(2) == 0)
            {
                var child = children[random.Next(children.Count)];
                _slots.Add(child + 2);
                _focus.AddRange([Member.LeafValue, Member.LeafNext]);
                return new Fetch(child + 2, new Site(Member.ChildLeaf, child, []));
            }

            var root = Root();
            _slots.Add(root);
            _focus.AddRange([Member.ChildValue, Member.ChildLeaf]);
            return new Fetch(root, new Site(Member.Child, root, [ArgSpec.Exact(Int())]));
        }

        private Op SetProperty() => new SetProperty(Root(), random.Next(2) == 0, random.Next(4), 5 + random.Next(5));

        private Op Event()
        {
            var mock = Root();
            var handler = 1 + random.Next(3);
            return random.Next(5) switch
            {
                0 or 1 => new Subscribe(mock, handler),
                2 => new Unsubscribe(mock, handler),
                _ => new RaiseEvent(mock, random.Next(10)),
            };
        }

        private Op Child()
        {
            var ops = new List<Op>();
            var count = 1 + random.Next(3);
            for (var i = 0; i < count; i++)
            {
                ops.Add(Statement());
            }

            return new ChildTask(ops);
        }

        private Op Finish() => new FinishInChild(SetupNs(Site(Style.NSubstitute, RootMembers(Style.NSubstitute))));

        /// <summary>
        /// A setup or a check in this flow, with a whole statement of another flow before its mock call
        /// or, for a setup, between its mock call and <c>Returns</c>. Before <c>Returns</c>, the other
        /// statement is not a check or a count, because CD-04 leaves what those see unsupported.
        /// </summary>
        private Op Interleave()
        {
            var at = random.Next(2) == 0 ? Point.BeforeCall : Point.BeforeReturns;
            Op a;
            do
            {
                a = at == Point.BeforeReturns || random.Next(2) == 0
                    ? SetupNs(Site(Style.NSubstitute, RootMembers(Style.NSubstitute)))
                    : new Check(Site(Style.NSubstitute, RootMembers(Style.Check)), 1, int.MaxValue);
            }
            while (at == Point.BeforeCall && (a is SetupNs { Site.Exacts: 0 } || a is Check { Site.Exacts: 0 }));

            var b = at == Point.BeforeCall ? Statement() : Pick((3, Invoke), (2, SetupNs), (2, SetupTyped), (1, When));
            return new Interleave(a, at, at == Point.BeforeReturns && random.Next(2) == 0, b);
        }

        private Op Parallel()
        {
            var branches = new List<IReadOnlyList<Op>>();
            var count = 2 + random.Next(3);
            for (var i = 0; i < count; i++)
            {
                var calls = new List<Op>();
                var length = 1 + random.Next(3);
                for (var j = 0; j < length; j++)
                {
                    calls.Add(new Invoke(Site(Style.Invoke, s_parallelMembers)));
                }

                branches.Add(calls);
            }

            return new ParallelCalls(branches);
        }

        /// <summary>Setups in parallel flows, each on its own pair of exact arguments, so no two match the same call.</summary>
        private Op ParallelSetups()
        {
            var mock = Root();
            var pairs = Enumerable.Range(0, 16).OrderBy(_ => random.Next()).Take(2 + random.Next(2)).ToList();
            return new ParallelSetups([.. pairs.Select(pair =>
            {
                var site = new Site(Member.Get2, mock, [ArgSpec.Exact((pair / 4).ToString(CultureInfo.InvariantCulture)), ArgSpec.Exact((pair % 4).ToString(CultureInfo.InvariantCulture))]);
                var response = new Response(ResponseKind.Value, [Result(Member.Get2)]);
                return random.Next(2) == 0 ? (Op)new SetupNs(site, response) : new SetupTyped(site, response);
            })]);
        }

        private Op Statement() => Pick((4, Invoke), (3, SetupNs), (3, SetupTyped), (2, Check), (2, CountCalls), (1, When));

        private enum Style
        {
            Invoke,
            NSubstitute,
            Typed,
            Check,
        }

        private Member[] Members(Style style) => [.. RootMembers(style), .. SlotMembers(style == Style.Check || style == Style.Invoke
            ? [Member.ChildValue, Member.ChildLeaf, Member.LeafValue, Member.LeafNext]
            : [Member.ChildValue, Member.ChildLeaf, Member.LeafValue])];

        private static Member[] RootMembers(Style style) => style is Style.Invoke or Style.Check or Style.Typed
            ? s_rootMembers
            : [.. s_rootMembers.Where(member => Inputs.ResultOf(member) != Inputs.Result.Void)];

        /// <summary>The members of slot mocks, when a slot of their kind may hold one.</summary>
        private Member[] SlotMembers(params Member[] members) => [.. members.Where(member => member switch
        {
            Member.ChildValue or Member.ChildLeaf => _slots.Any(IsChildSlot),
            _ => _slots.Any(slot => !IsChildSlot(slot)),
        })];

        private Site Site(Style style, Member[] members)
        {
            var focused = members.Intersect(_focus).ToArray();
            var member = focused.Length > 0 && random.Next(5) > 0 ? focused[random.Next(focused.Length)] : members[random.Next(members.Length)];
            var target = member switch
            {
                Member.ChildValue or Member.ChildLeaf => Slot(child: true),
                Member.LeafValue or Member.LeafNext => Slot(child: false),
                _ => Root(),
            };
            var (count, strings) = Inputs.ParametersOf(member);
            var args = new List<ArgSpec>();
            for (var i = 0; i < count; i++)
            {
                args.Add(Arg(style, member, i, strings));
            }

            var site = new Site(member, target, args);
            _recent.Add(site);
            return site;
        }

        /// <summary>
        /// The kinds of argument a style can write at a position, with exact values twice as likely. A
        /// call passes values. An NSubstitute-style statement uses the matcher patterns the interpreter
        /// has call sites for, and a typed one uses any matcher. The out argument of TryGet is 0 in a
        /// call and <c>Any</c> in an NSubstitute-style statement.
        /// </summary>
        private static ArgKind[] Kinds(Style style, Member member, int position) => (style, member, position) switch
        {
            (Style.Invoke, _, _) => [ArgKind.Exact],
            (Style.Typed, _, _) => [ArgKind.Exact, ArgKind.Exact, ArgKind.Any, ArgKind.Pred, ArgKind.IsValue],
            (_, Member.TryGet, 1) => [ArgKind.Any],
            (_, Member.TryGet, _) => [ArgKind.Exact, ArgKind.Any],
            (_, Member.Get2, _) => [ArgKind.Exact, ArgKind.Exact, ArgKind.Any, ArgKind.Pred],
            (_, Member.Get1 or Member.Name, _) => [ArgKind.Exact, ArgKind.Exact, ArgKind.Any, ArgKind.Pred, ArgKind.IsValue],
            _ => [ArgKind.Exact, ArgKind.Exact, ArgKind.Any],
        };

        private ArgSpec Arg(Style style, Member member, int position, bool strings)
        {
            if (member == Member.TryGet && position == 1)
            {
                return style == Style.Invoke ? ArgSpec.Exact("0") : ArgSpec.Any;
            }

            var kinds = Kinds(style, member, position);
            return kinds[random.Next(kinds.Length)] switch
            {
                ArgKind.Exact => ArgSpec.Exact(strings ? String() : Int()),
                ArgKind.Any => ArgSpec.Any,
                ArgKind.Pred => new ArgSpec(ArgKind.Pred, null, strings ? Pred.NullOrEmpty : random.Next(2) == 0 ? Pred.Even : Pred.AboveOne),
                _ => new ArgSpec(ArgKind.IsValue, strings ? String() : Int()),
            };
        }

        /// <summary>A response for the site's member. A recursive member returns a mock reached from the same root, or throws.</summary>
        private Response Response(Site site, bool typed)
        {
            var member = site.Member;
            var result = Inputs.ResultOf(member);
            if (result is Inputs.Result.Child or Inputs.Result.Leaf)
            {
                var slots = _slots.Where(slot => IsChildSlot(slot) == (result == Inputs.Result.Child) && OwnerOf(slot) == OwnerOf(site)).ToList();
                return slots.Count > 0 && random.Next(4) > 0
                    ? new Response(ResponseKind.SameAs, [], slots[random.Next(slots.Count)])
                    : new Response(ResponseKind.Throws, [], ++_ids);
            }

            var kinds = result switch
            {
                Inputs.Result.Task when typed => [ResponseKind.Value, ResponseKind.Factory, ResponseKind.Throws, ResponseKind.Faulted, ResponseKind.Cancelled],
                Inputs.Result.Task => [ResponseKind.Value, ResponseKind.Sequence, ResponseKind.Factory, ResponseKind.Throws, ResponseKind.Faulted, ResponseKind.Cancelled],
                Inputs.Result.ValueTask => [ResponseKind.Value, ResponseKind.Factory, ResponseKind.Throws, ResponseKind.Faulted, ResponseKind.Cancelled],
                _ => new[] { ResponseKind.Value, ResponseKind.Value, ResponseKind.Sequence, ResponseKind.Factory, ResponseKind.Throws },
            };
            var kind = kinds[random.Next(kinds.Length)];
            return kind switch
            {
                ResponseKind.Value => new Response(kind, [Result(member)]),
                ResponseKind.Sequence => new Response(kind, [.. Enumerable.Range(0, 2 + random.Next(2)).Select(_ => Result(member))]),
                ResponseKind.Throws or ResponseKind.Faulted => new Response(kind, [], ++_ids),
                _ => new Response(kind, []),
            };
        }

        private string? Result(Member member) => Inputs.ResultOf(member) switch
        {
            Inputs.Result.String => random.Next(4) switch
            {
                0 => null,
                1 => "y",
                _ => "x",
            },
            Inputs.Result.Bool => random.Next(2) == 0 ? "true" : "false",
            _ => (5 + random.Next(5)).ToString(CultureInfo.InvariantCulture),
        };

        private int Root() => random.Next(4) == 0 ? 1 : 0;

        private int Slot(bool child)
        {
            var slots = _slots.Where(slot => IsChildSlot(slot) == child).ToList();
            return slots[random.Next(slots.Count)];
        }

        /// <summary>0 to 3, mostly 1 or 2. 0 is the default a matcher call passes.</summary>
        private string Int() => (random.Next(6) switch
        {
            0 => 0,
            1 => 3,
            var roll => 1 + (roll % 2),
        }).ToString(CultureInfo.InvariantCulture);

        private string? String() => random.Next(3) switch
        {
            0 => null,
            1 => "",
            _ => "a",
        };

        private Op Pick(params (int Weight, Func<Op> Make)[] choices)
        {
            var roll = random.Next(choices.Sum(choice => choice.Weight));
            foreach (var (weight, make) in choices)
            {
                if (roll < weight)
                {
                    return make();
                }

                roll -= weight;
            }

            throw new HarnessException("No choice was picked.");
        }
    }
}
