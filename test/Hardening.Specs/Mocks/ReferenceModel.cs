using System.Globalization;

namespace Hardening.Specs.Mocks;

/// <summary>
/// Mutations of the reference model. A spec turns one on to check that a comparison run detects a
/// broken rule, and that the harness shrinks and replays the mismatch.
/// </summary>
public enum ModelFault
{
    None,

    /// <summary>The first matching setup answers a call instead of the last one.</summary>
    FirstSetupWins,

    /// <summary>A check consumes a value of each response sequence it matches.</summary>
    CheckConsumesSequences,
}

/// <summary>
/// An independent model of the mock contract in docs/design/mocks.md: matching, setup precedence,
/// response sequences, callbacks, recorded calls, recursive mocks and the orderings of CD-04. It
/// runs a <see cref="Sequence"/> and gives the observations VerificationGlue must give. It does not
/// call VerificationGlue.Mocks.
/// </summary>
public sealed class ReferenceModel(ModelFault fault = ModelFault.None)
{
    private const int ListedCallLimit = 20;

    private readonly ModelMock[] _roots = [new(0), new(0)];
    private readonly Dictionary<int, ModelMock?> _slots = [];
    private readonly Dictionary<ModelMock, string> _labels = [];

    public static IReadOnlyList<string> Run(Sequence sequence, ModelFault fault = ModelFault.None)
    {
        var model = new ReferenceModel(fault);
        model._labels[model._roots[0]] = "root0";
        model._labels[model._roots[1]] = "root1";
        var observations = new List<string>();
        foreach (var op in sequence.Ops)
        {
            model.Apply(op, observations);
        }

        return observations;
    }

    private void Apply(Op op, List<string> observations)
    {
        switch (op)
        {
            case ParallelCalls parallel:
                observations.Add(Parallel(parallel.Branches.SelectMany(branch => branch)));
                break;
            case ParallelSetups setups:
                observations.Add(Parallel(setups.Branches));
                break;
            case ChildTask child:
                foreach (var inner in child.Ops)
                {
                    Apply(inner, observations);
                }

                break;
            case FinishInChild finish:
                observations.Add(Statement(finish.Setup));
                break;
            case AbandonInChild:
                observations.Add("abandoned a matcher in a child task");
                break;
            case Yield:
                break;
            case Interleave { At: Point.BeforeCall } interleave:
                var before = Statement(interleave.B);
                observations.Add(Statement(interleave.A));
                observations.Add("other flow: " + before);
                break;
            case Interleave { A: SetupNs setup } interleave:
                var call = SetupCall(setup);
                var other = Statement(interleave.B);
                observations.Add(call.Error is { } error ? "throws " + error : Bind(setup, call.Record!));
                observations.Add("other flow: " + other);
                break;
            default:
                observations.Add(Statement(op));
                break;
        }
    }

    /// <summary>
    /// Runs the statements of parallel flows. Their order is not part of the contract, so the
    /// observation is the sorted calls and the sorted outcomes, and the calls they record sort as one
    /// range in a later snapshot.
    /// </summary>
    private string Parallel(IEnumerable<Op> statements)
    {
        var starts = AllMocks().ToDictionary(mock => mock, mock => mock.Calls.Count);
        var outcomes = new List<string>();
        foreach (var statement in statements)
        {
            outcomes.Add(Statement(statement));
        }

        foreach (var (mock, start) in starts)
        {
            if (mock.Calls.Count > start)
            {
                mock.Ranges.Add((start, mock.Calls.Count));
            }
        }

        return Inputs.Parallel(outcomes);
    }

    private IEnumerable<ModelMock> AllMocks() => _roots.Concat(_slots.Values.OfType<ModelMock>()).Distinct();

    private string Statement(Op op)
    {
        switch (op)
        {
            case Invoke invoke:
                return Receiver(invoke.Site) is { } mock ? Show(invoke.Site, Call(mock, invoke.Site, Values(invoke.Site))) : Empty(invoke.Site);
            case SetupNs setup:
                if (Receiver(setup.Site) is null)
                {
                    return Empty(setup.Site);
                }

                var call = SetupCall(setup);
                return call.Error is { } error ? "throws " + error : Bind(setup, call.Record!);
            case SetupTyped typed:
                if (Receiver(typed.Site) is not { } target)
                {
                    return Empty(typed.Site);
                }

                if (typed.Does is { } does)
                {
                    target.Callbacks.Add((typed.Site, does, false));
                }

                if (typed.Response is { } response)
                {
                    target.Responses.Add(new ModelResponse(typed.Site, response, Captured(response)));
                }

                return "set up";
            case When select:
                if (Receiver(select.Site) is not { } selected)
                {
                    return Empty(select.Site);
                }

                selected.Callbacks.Add((select.Site, select.Callback, select.Throw));
                return "set up";
            case Check check:
                return Receiver(check.Site) is { } checkedMock ? CheckCalls(checkedMock, check) : Empty(check.Site);
            case CountCalls count:
                if (Receiver(count.Site) is not { } counted)
                {
                    return Empty(count.Site);
                }

                var matching = counted.Calls.Count(record => Matches(count.Site, record));
                var toMember = counted.Calls.Count(record => !record.Setter && !record.Event && Inputs.GroupOf(record.Member) == Inputs.GroupOf(count.Site.Member));
                return string.Create(CultureInfo.InvariantCulture, $"count {matching} of {toMember}");
            case Snapshot snapshot:
                if ((snapshot.Slot ? _slots.GetValueOrDefault(snapshot.Target) : _roots[snapshot.Target]) is not { } shown)
                {
                    return $"slot{snapshot.Target} is empty";
                }

                return "calls [" + string.Join(", ", Inputs.Normalized([.. shown.Calls.Select(record => record.Text)], shown.Ranges)) + "]";
            case Fetch fetch:
                if (Receiver(fetch.Site) is not { } source)
                {
                    return Empty(fetch.Site);
                }

                var fetched = Call(source, fetch.Site, Values(fetch.Site));
                if (fetched.Error is { } fetchError)
                {
                    return "throws " + fetchError;
                }

                _slots[fetch.Slot] = (ModelMock?)fetched.Value;
                return $"slot{fetch.Slot} = {Label((ModelMock?)fetched.Value)}";
            case SetProperty set:
                var root = _roots[set.Mock];
                var getter = set.Indexer ? Member.Index : Member.Count;
                object?[] index = set.Indexer ? [set.Index] : [];
                root.Calls.Add(new ModelCall(getter, [.. index, set.Value], Inputs.CallText(set.Indexer ? "set_Item" : "set_Count", [.. index, set.Value], _ => "")) { Setter = true });
                var getterSite = new Site(getter, set.Mock, set.Indexer ? [ArgSpec.Exact(set.Index.ToString(CultureInfo.InvariantCulture))] : []);
                root.Responses.Add(new ModelResponse(getterSite, new Response(ResponseKind.Value, [set.Value.ToString(CultureInfo.InvariantCulture)]), null));
                return "set";
            case Subscribe subscribe:
                _roots[subscribe.Mock].Calls.Add(new ModelCall(Member.Reset, [], $"add_Changed(h{subscribe.Handler})") { Event = true });
                _roots[subscribe.Mock].Handlers.Add(subscribe.Handler);
                return "subscribed";
            case Unsubscribe unsubscribe:
                var handlers = _roots[unsubscribe.Mock].Handlers;
                _roots[unsubscribe.Mock].Calls.Add(new ModelCall(Member.Reset, [], $"remove_Changed(h{unsubscribe.Handler})") { Event = true });
                var last = handlers.LastIndexOf(unsubscribe.Handler);
                if (last >= 0)
                {
                    handlers.RemoveAt(last);
                }

                return "unsubscribed";
            case RaiseEvent raise:
                return "raised [" + string.Join(", ", _roots[raise.Mock].Handlers.Select(handler => string.Create(CultureInfo.InvariantCulture, $"h{handler}({raise.Value})"))) + "]";
            default:
                throw new HarnessException($"The model has no rule for {op}.");
        }
    }

    private ModelMock? Receiver(Site site) => site.OnSlot ? _slots.GetValueOrDefault(site.Target) : _roots[site.Target];

    private static string Empty(Site site) => $"slot{site.Target} is empty";

    /// <summary>The mock call of an NSubstitute-style setup. It is an ordinary call, so earlier setups and callbacks answer it.</summary>
    private (ModelCall? Record, string? Error) SetupCall(SetupNs setup)
    {
        var mock = Receiver(setup.Site)!;
        var outcome = Call(mock, setup.Site, Values(setup.Site));
        return (mock.Calls[^1], outcome.Error);
    }

    /// <summary><c>Returns</c> or <c>Throws</c>: removes the setup call from the recorded calls and adds the response.</summary>
    private string Bind(SetupNs setup, ModelCall record)
    {
        var mock = Receiver(setup.Site)!;
        mock.Calls.RemoveAt(mock.Calls.FindLastIndex(call => ReferenceEquals(call, record)));
        mock.Responses.Add(new ModelResponse(setup.Site, setup.Response, Captured(setup.Response)));
        return "set up";
    }

    private ModelMock? Captured(Response response) => response.Kind == ResponseKind.SameAs ? _slots.GetValueOrDefault(response.Id) : null;

    private static object?[] Values(Site site) => [.. site.Args.Select(arg => Inputs.Passed(site.Member, arg))];

    private static bool Matches(Site site, ModelCall record) =>
        !record.Setter && !record.Event && record.Member == site.Member && site.Args.Select((arg, i) => Inputs.Matches(site.Member, arg, record.Arguments[i])).All(match => match);

    /// <summary>
    /// A call: it is recorded, the callbacks that match it run in the order they were added, and the
    /// last setup that matches it answers. Which callbacks and which setup match is decided when the
    /// call arrives. A callback that throws ends the call. With no setup, an unconfigured member gives
    /// its auto-value, and a recursive member gives the mock it made for equal arguments, or a new one.
    /// </summary>
    private Outcome Call(ModelMock mock, Site site, object?[] arguments)
    {
        var record = new ModelCall(site.Member, [.. arguments], Inputs.CallText(Inputs.NameOf(site.Member), arguments, _ => ""));
        mock.Calls.Add(record);
        var callbacks = mock.Callbacks.Where(callback => Matches(callback.Site, record)).ToList();
        var matching = mock.Responses.Where(response => Matches(response.Site, record)).ToList();
        var response = fault == ModelFault.FirstSetupWins ? matching.FirstOrDefault() : matching.LastOrDefault();

        var ran = new List<string>();
        foreach (var (_, callback, throws) in callbacks)
        {
            if (throws)
            {
                return new Outcome(null, ran, "r" + callback.Id.ToString(CultureInfo.InvariantCulture));
            }

            ran.Add("c" + callback.Id.ToString(CultureInfo.InvariantCulture));
            if (callback.Writes)
            {
                arguments[^1] = 100 + callback.Id;
            }
        }

        if (response is null)
        {
            return new Outcome(Unconfigured(mock, site.Member, record), ran, null, arguments);
        }

        var r = response.Response;
        switch (r.Kind)
        {
            case ResponseKind.Throws:
                return new Outcome(null, ran, "r" + r.Id.ToString(CultureInfo.InvariantCulture));
            case ResponseKind.Faulted:
                return new Outcome(new AsyncResult("faulted r" + r.Id.ToString(CultureInfo.InvariantCulture)), ran, null, arguments);
            case ResponseKind.Cancelled:
                return new Outcome(new AsyncResult("cancelled"), ran, null, arguments);
            case ResponseKind.SameAs:
                return new Outcome(response.Mock, ran, null, arguments);
            case ResponseKind.Factory:
                return new Outcome(Inputs.Factory(site.Member, arguments), ran, null, arguments);
            default:
                var index = response.Next++;
                var value = r.Values[Math.Min(index, r.Values.Count - 1)];
                return new Outcome(Inputs.ParseResult(site.Member, value), ran, null, arguments);
        }
    }

    private static object? Unconfigured(ModelMock mock, Member member, ModelCall record)
    {
        switch (Inputs.ResultOf(member))
        {
            case Inputs.Result.String:
                return "";
            case Inputs.Result.Bool:
                return false;
            case Inputs.Result.Child or Inputs.Result.Leaf when member != Member.LeafNext:
                foreach (var (made, made_member, made_args) in mock.Recursive)
                {
                    if (made_member == member && made_args.SequenceEqual(record.Arguments))
                    {
                        return made;
                    }
                }

                var created = new ModelMock(mock.Level + 1);
                mock.Recursive.Add((created, member, record.Arguments));
                return created;
            case Inputs.Result.Child or Inputs.Result.Leaf:
                return null;
            case Inputs.Result.Void:
                return null;
            default:
                return 0;
        }
    }

    private string CheckCalls(ModelMock mock, Check check)
    {
        if (fault == ModelFault.CheckConsumesSequences)
        {
            foreach (var response in mock.Responses.Where(response => response.Response.Kind == ResponseKind.Sequence && Overlaps(response.Site, check.Site)))
            {
                response.Next++;
            }
        }

        var count = mock.Calls.Count(record => Matches(check.Site, record));
        if (count >= check.Min && count <= check.Max)
        {
            return "check passes";
        }

        var listed = mock.Calls.Take(ListedCallLimit).ToList();
        return Inputs.CheckFailure(count, listed.Count, listed.Count(record => Matches(check.Site, record)), mock.Calls.Count - listed.Count);
    }

    private static bool Overlaps(Site setup, Site check) => setup.Member == check.Member;

    private string Show(Site site, Outcome outcome)
    {
        var callbacks = outcome.Callbacks.Count == 0 ? "" : "[" + string.Join(" ", outcome.Callbacks) + "] ";
        if (outcome.Error is { } error)
        {
            return callbacks + "throws " + error;
        }

        var text = Inputs.ResultOf(site.Member) switch
        {
            Inputs.Result.Task or Inputs.Result.ValueTask => outcome.Value is AsyncResult async ? "awaits " + async.Text : "awaits " + Inputs.Show(outcome.Value),
            Inputs.Result.Bool => Inputs.Show(outcome.Value) + ", out " + Inputs.Show(outcome.Arguments![1]),
            Inputs.Result.Void when site.Member == Member.Swap => "ref " + Inputs.Show(outcome.Arguments![0]),
            Inputs.Result.Void => "done",
            Inputs.Result.Child or Inputs.Result.Leaf => Label((ModelMock?)outcome.Value),
            _ => Inputs.Show(outcome.Value),
        };
        return callbacks + text;
    }

    private string Label(ModelMock? mock)
    {
        if (mock is null)
        {
            return "null";
        }

        if (!_labels.TryGetValue(mock, out var label))
        {
            label = "m" + _labels.Count.ToString(CultureInfo.InvariantCulture);
            _labels[mock] = label;
        }

        return label;
    }

    private sealed record Outcome(object? Value, IReadOnlyList<string> Callbacks, string? Error, object?[]? Arguments = null);

    private sealed record AsyncResult(string Text);

    private sealed record ModelCall(Member Member, object?[] Arguments, string Text)
    {
        public bool Setter { get; init; }

        public bool Event { get; init; }
    }

    private sealed class ModelResponse(Site site, Response response, ModelMock? mock)
    {
        public Site Site => site;

        public Response Response => response;

        public ModelMock? Mock => mock;

        public int Next { get; set; }
    }

    private sealed class ModelMock(int level)
    {
        public int Level => level;

        public List<ModelCall> Calls { get; } = [];

        public List<(int Start, int End)> Ranges { get; } = [];

        public List<ModelResponse> Responses { get; } = [];

        public List<(Site Site, Callback Callback, bool Throws)> Callbacks { get; } = [];

        public List<(ModelMock Mock, Member Member, object?[] Arguments)> Recursive { get; } = [];

        public List<int> Handlers { get; } = [];
    }
}
