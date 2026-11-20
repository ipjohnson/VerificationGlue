using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using VerificationGlue.Mocks;

namespace Hardening.Specs.Mocks;

/// <summary>
/// Runs a <see cref="Sequence"/> on mocks the public generator built for <see cref="IService"/>, and
/// gives the observations in the form <see cref="ReferenceModel"/> gives them.
/// </summary>
/// <remarks>
/// The ops of one flow run in one async method, so state a statement leaves in the mock context
/// reaches the next statement as it would in a test. Each NSubstitute-style matcher is written
/// directly as an argument of a mock call, so the generator's interceptor gives it its position.
/// </remarks>
public sealed partial class Interpreter
{
    private static readonly AsyncLocal<Hook?> s_hook = new();
    private static readonly AsyncLocal<List<string>?> s_log = new();

    private readonly IService[] _roots = [Mock.Of<IService>(), Mock.Of<IService>()];
    private readonly ConcurrentDictionary<int, object?> _slots = new();
    private readonly Dictionary<object, string> _labels = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, List<(int Start, int End)>> _ranges = new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentDictionary<int, EventHandler<int>> _handlers = new();

    private Interpreter()
    {
        _labels[_roots[0]] = "root0";
        _labels[_roots[1]] = "root1";
    }

    public static async Task<IReadOnlyList<string>> RunAsync(Sequence sequence)
    {
        var interpreter = new Interpreter();
        var observations = new List<string>();
        await interpreter.RunOpsAsync(sequence.Ops, observations);
        return observations;
    }

    private async Task RunOpsAsync(IReadOnlyList<Op> ops, List<string> observations)
    {
        foreach (var op in ops)
        {
            switch (op)
            {
                case SetupNs { Await: true } setup when Receiver(setup.Site) is { } receiver:
                    var awaited = TryCall(receiver, setup.Site);
                    await Task.Yield();
                    observations.Add(awaited.Error ?? Bind(setup.Site, setup.Response));
                    break;
                case ChildTask child:
                    var inner = new List<string>();
                    await Task.Run(() => RunOpsAsync(child.Ops, inner));
                    observations.AddRange(inner);
                    break;
                case FinishInChild { Setup: var finish } when Receiver(finish.Site) is { } parent:
                    var started = TryCall(parent, finish.Site);
                    observations.Add(started.Error ?? await Task.Run(() => Bind(finish.Site, finish.Response)));
                    break;
                case AbandonInChild:
#pragma warning disable VGM0002 // The child abandons the matcher on purpose.
                    await Task.Run(() => Arg.Any<int>());
#pragma warning restore VGM0002
                    observations.Add("abandoned a matcher in a child task");
                    break;
                case Yield:
                    await Task.Yield();
                    break;
                case Interleave interleave:
                    observations.AddRange(await InterleaveAsync(interleave));
                    break;
                case ParallelCalls parallel:
                    observations.Add(await ParallelAsync(parallel.Branches));
                    break;
                case ParallelSetups setups:
                    observations.Add(await ParallelAsync([.. setups.Branches.Select(branch => (IReadOnlyList<Op>)[branch])]));
                    break;
                default:
                    var text = Statement(op, out var rest);
                    observations.Add(rest is null ? text : await rest);
                    break;
            }
        }
    }

    private async Task<string> StatementAsync(Op op)
    {
        var text = Statement(op, out var rest);
        return rest is null ? text : await rest;
    }

    /// <summary>
    /// Runs statement A in this flow and B in another flow at the point A asks for. Before the mock
    /// call, a hook on A's last exact argument hands over to B and blocks until B is done. Before
    /// <c>Returns</c>, A awaits B or blocks on it.
    /// </summary>
    private async Task<string[]> InterleaveAsync(Interleave interleave)
    {
        using var go = new SemaphoreSlim(0);
        using var done = new SemaphoreSlim(0);
        var other = Task.Run(async () =>
        {
            await go.WaitAsync();
            try
            {
                return await StatementAsync(interleave.B);
            }
            finally
            {
                done.Release();
            }
        });

        string first;
        if (interleave.At == Point.BeforeCall)
        {
            s_hook.Value = new Hook(Exacts(interleave.A), () =>
            {
                go.Release();
                done.Wait();
            });
            first = Statement(interleave.A, out _);
            s_hook.Value = null;
        }
        else
        {
            var setup = (SetupNs)interleave.A;
            var call = TryCall(Receiver(setup.Site)!, setup.Site);
            go.Release();
            if (interleave.Await)
            {
                await done.WaitAsync();
            }
            else
            {
                done.Wait();
            }

            first = call.Error ?? Bind(setup.Site, setup.Response);
        }

        return [first, "other flow: " + await other];
    }

    private static int Exacts(Op op) => op switch
    {
        SetupNs setup => setup.Site.Exacts,
        Check check => check.Site.Exacts,
        _ => 0,
    };

    /// <summary>Starts a flow for each branch behind one barrier and joins them.</summary>
    private async Task<string> ParallelAsync(IReadOnlyList<IReadOnlyList<Op>> branches)
    {
        var mocks = _roots.Concat(_slots.Values.OfType<object>()).Distinct(ReferenceEqualityComparer.Instance).ToList();
        var starts = mocks.ToDictionary(mock => mock, mock => Adapter(mock).ReceivedCalls().Count, ReferenceEqualityComparer.Instance);
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcomes = new ConcurrentBag<string>();
        var flows = branches.Select(branch => Task.Run(async () =>
        {
            await barrier.Task;
            foreach (var op in branch)
            {
                outcomes.Add(await StatementAsync(op));
            }
        })).ToList();
        barrier.SetResult();
        await Task.WhenAll(flows);

        foreach (var (mock, start) in starts)
        {
            var end = Adapter(mock).ReceivedCalls().Count;
            if (end > start)
            {
                Ranges(mock).Add((start, end));
            }
        }

        return Inputs.Parallel(outcomes);
    }

    private string Statement(Op op, out Task<string>? rest)
    {
        rest = null;
        var log = new List<string>();
        s_log.Value = log;
        try
        {
            switch (op)
            {
                case Invoke invoke:
                    if (Receiver(invoke.Site) is not { } receiver)
                    {
                        return Empty(invoke.Site);
                    }

                    var result = Call(receiver, invoke.Site);
                    var callbacks = log.Count == 0 ? "" : "[" + string.Join(" ", log) + "] ";
                    if (result is Task<int> task)
                    {
                        rest = Awaited(callbacks, task);
                        return "";
                    }

                    if (result is ValueTask<int> valueTask)
                    {
                        rest = Awaited(callbacks, valueTask.AsTask());
                        return "";
                    }

                    return callbacks + Show(invoke.Site, result);
                case SetupNs setup:
                    if (Receiver(setup.Site) is not { } setUp)
                    {
                        return Empty(setup.Site);
                    }

                    Call(setUp, setup.Site);
                    return Bind(setup.Site, setup.Response);
                case SetupTyped typed:
                    return Receiver(typed.Site) is { } target ? SetUpTyped(target, typed) : Empty(typed.Site);
                case When select:
                    return Receiver(select.Site) is { } selected ? SetUpWhen(selected, select) : Empty(select.Site);
                case Check check:
                    return Receiver(check.Site) is { } checkedMock ? CheckCalls(checkedMock, check) : Empty(check.Site);
                case CountCalls count:
                    if (Receiver(count.Site) is not { } counted)
                    {
                        return Empty(count.Site);
                    }

                    var callCount = Count(counted, count.Site);
                    return string.Create(CultureInfo.InvariantCulture, $"count {callCount.Count} of {callCount.CallsToMember.Count}");
                case Snapshot snapshot:
                    var shown = snapshot.Slot ? _slots.GetValueOrDefault(snapshot.Target) : _roots[snapshot.Target];
                    if (shown is null)
                    {
                        return $"slot{snapshot.Target} is empty";
                    }

                    var calls = Adapter(shown).ReceivedCalls().Select(Text).ToList();
                    return "calls [" + string.Join(", ", Inputs.Normalized(calls, Ranges(shown))) + "]";
                case Fetch fetch:
                    if (Receiver(fetch.Site) is not { } source)
                    {
                        return Empty(fetch.Site);
                    }

                    var fetched = Call(source, fetch.Site);
                    _slots[fetch.Slot] = fetched;
                    return $"slot{fetch.Slot} = {Label(fetched)}";
                case SetProperty set:
                    if (set.Indexer)
                    {
                        _roots[set.Mock][set.Index] = set.Value;
                    }
                    else
                    {
                        _roots[set.Mock].Count = set.Value;
                    }

                    return "set";
                case Subscribe subscribe:
                    _roots[subscribe.Mock].Changed += Handler(subscribe.Handler);
                    return "subscribed";
                case Unsubscribe unsubscribe:
                    _roots[unsubscribe.Mock].Changed -= Handler(unsubscribe.Handler);
                    return "unsubscribed";
                case RaiseEvent raise:
                    _roots[raise.Mock].Changed += Raise.Event<EventHandler<int>>(_roots[raise.Mock], raise.Value);
                    return "raised [" + string.Join(", ", log) + "]";
                default:
                    throw new HarnessException($"The interpreter cannot run {op} as one statement.");
            }
        }
        catch (Exception exception) when (exception is not HarnessException)
        {
            var callbacks = log.Count == 0 || op is not Invoke ? "" : "[" + string.Join(" ", log) + "] ";
            return callbacks + "throws " + Inputs.Show(exception);
        }
    }

    private static async Task<string> Awaited(string callbacks, Task<int> task)
    {
        try
        {
            return callbacks + "awaits " + Inputs.Show(await task);
        }
        catch (OperationCanceledException)
        {
            return callbacks + "awaits cancelled";
        }
        catch (Exception exception)
        {
            return callbacks + "awaits faulted " + Inputs.Show(exception);
        }
    }

    private (object? Value, string? Error) TryCall(object receiver, Site site)
    {
        try
        {
            return (Call(receiver, site), null);
        }
        catch (Exception exception)
        {
            return (null, "throws " + Inputs.Show(exception));
        }
    }

    private string CheckCalls(object mock, Check check)
    {
        var receiver = mock switch
        {
            IService service => (object)((check.Min, check.Max) switch
            {
                (0, 0) => service.DidNotReceive(),
                (1, int.MaxValue) => service.Received(),
                _ => service.Received(check.Min),
            }),
            IChild child => (check.Min, check.Max) switch
            {
                (0, 0) => child.DidNotReceive(),
                (1, int.MaxValue) => child.Received(),
                _ => child.Received(check.Min),
            },
            ILeaf leaf => (check.Min, check.Max) switch
            {
                (0, 0) => leaf.DidNotReceive(),
                (1, int.MaxValue) => leaf.Received(),
                _ => leaf.Received(check.Min),
            },
            _ => throw new HarnessException($"The interpreter has no checks for {mock}."),
        };

        try
        {
            Call(receiver, check.Site);
            return "check passes";
        }
        catch (ReceivedCallsException exception)
        {
            var lines = exception.Message.Split('\n');
            var received = int.Parse(ReceivedCount().Match(exception.Message).Groups[1].Value, CultureInfo.InvariantCulture);
            var listed = lines.Count(line => line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("  and ", StringComparison.Ordinal));
            var marks = lines.Count(line => line.EndsWith("  (matches)", StringComparison.Ordinal));
            var more = MoreCount().Match(exception.Message) is { Success: true } match ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            return Inputs.CheckFailure(received, listed, marks, more);
        }
    }

    private string SetUpWhen(object mock, When when)
    {
        var selected = mock switch
        {
            IService service => service.When(target => Call(target, when.Site)),
            IChild child => child.When(target => Call(target, when.Site)),
            ILeaf leaf => leaf.When(target => Call(target, when.Site)),
            _ => throw new HarnessException($"The interpreter has no When for {mock}."),
        };

        if (when.Throw)
        {
            selected.Throw(Inputs.Error(when.Callback.Id));
        }
        else
        {
            selected.Do(info => Run(when.Callback, info));
        }

        return "set up";
    }

    private static void Run(Callback callback, CallInfo info)
    {
        s_log.Value?.Add("c" + callback.Id.ToString(CultureInfo.InvariantCulture));
        if (callback.Writes)
        {
            info[info.Count - 1] = 100 + callback.Id;
        }
    }

    private EventHandler<int> Handler(int id) =>
        _handlers.GetOrAdd(id, _ => (_, value) => s_log.Value?.Add(string.Create(CultureInfo.InvariantCulture, $"h{id}({value})")));

    private object? Receiver(Site site) => site.OnSlot ? _slots.GetValueOrDefault(site.Target) : _roots[site.Target];

    private static string Empty(Site site) => $"slot{site.Target} is empty";

    private static MockAdapter Adapter(object mock) => MockAdapter.Of(mock);

    private List<(int Start, int End)> Ranges(object mock)
    {
        lock (_ranges)
        {
            if (!_ranges.TryGetValue(mock, out var ranges))
            {
                ranges = [];
                _ranges[mock] = ranges;
            }

            return ranges;
        }
    }

    private static string NameOf(CallRecord call) =>
        call.MemberName + (call.TypeArguments.Count == 0 ? "" : "<" + string.Join(", ", call.TypeArguments.Select(type => type.Name)) + ">");

    private string Text(CallRecord call) => Inputs.CallText(NameOf(call), call.Arguments, HandlerText);

    private string HandlerText(object handler) =>
        _handlers.FirstOrDefault(pair => ReferenceEquals(pair.Value, handler)) is { Value: not null } pair ? "h" + pair.Key.ToString(CultureInfo.InvariantCulture) : "handler";

    private string Show(Site site, object? result) => Inputs.ResultOf(site.Member) switch
    {
        Inputs.Result.Bool => result is ValueTuple<bool, int> (var found, var value) ? Inputs.Show(found) + ", out " + Inputs.Show(value) : Inputs.Show(result),
        Inputs.Result.Void when site.Member == Member.Swap => "ref " + Inputs.Show(result),
        Inputs.Result.Void => "done",
        Inputs.Result.Child or Inputs.Result.Leaf => Label(result),
        _ => Inputs.Show(result),
    };

    private string Label(object? mock)
    {
        if (mock is null)
        {
            return "null";
        }

        lock (_labels)
        {
            if (!_labels.TryGetValue(mock, out var label))
            {
                label = "m" + _labels.Count.ToString(CultureInfo.InvariantCulture);
                _labels[mock] = label;
            }

            return label;
        }
    }

    [GeneratedRegex(@"but received (\d+)\.")]
    private static partial Regex ReceivedCount();

    [GeneratedRegex(@"\n  and (\d+) more")]
    private static partial Regex MoreCount();

    /// <summary>Runs the scheduler's action when the flow that set it evaluates its last exact argument.</summary>
    private sealed class Hook(int at, Action action)
    {
        private int _seen;

        public void Tick()
        {
            if (++_seen == at)
            {
                action();
            }
        }
    }
}
