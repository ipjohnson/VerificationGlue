using System.Text;

namespace Glue.Mocks;

/// <summary>Implemented by every generated mock. Gives the setup API its adapter instance.</summary>
public interface IMock
{
    MockAdapter Adapter { get; }
}

public sealed class CallInfo
{
    private readonly object?[] _args;

    internal CallInfo(object?[] args, Type[]? typeArguments)
    {
        _args = args;
        TypeArguments = typeArguments ?? Type.EmptyTypes;
    }

    public Type[] TypeArguments { get; }

    public object? this[int index]
    {
        get => _args[index];
        set => _args[index] = value;
    }

    public T ArgAt<T>(int index) => (T)_args[index]!;
}

public sealed record CallRecord(int MemberId, string MemberName, Type[]? TypeArguments, object?[] Arguments)
{
    public override string ToString() =>
        $"{MemberName}{(TypeArguments is { Length: > 0 } ? "<" + string.Join(", ", TypeArguments.Select(t => t.Name)) + ">" : "")}({string.Join(", ", Arguments.Select(a => a ?? "null"))})";
}

public sealed class ReceivedCallsException(string message) : Exception(message);

public sealed class AmbiguousArgumentsException(string message) : Exception(message);

public sealed class NoLastCallException()
    : Exception("Returns/Throws was called, but the receiver was not a call on a mock made on this thread.");

internal sealed class CallSpec(int memberId, string memberName, Type[]? typeArguments, IArgMatcher[] matchers)
{
    public int MemberId => memberId;

    public string MemberName => memberName;

    public bool Matches(CallRecord call)
    {
        if (call.MemberId != memberId)
        {
            return false;
        }

        if (!(typeArguments ?? Type.EmptyTypes).AsSpan().SequenceEqual(call.TypeArguments ?? Type.EmptyTypes))
        {
            return false;
        }

        for (var i = 0; i < matchers.Length; i++)
        {
            if (!matchers[i].Matches(call.Arguments[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString() => $"{memberName}({string.Join(", ", matchers.Select(m => m.ToString()))})";

    public static CallSpec Create(int memberId, string memberName, Type[]? typeArguments, object?[] args, List<IArgMatcher>? queued)
    {
        var matchers = new IArgMatcher[args.Length];

        if (queued is null || queued.Count == 0)
        {
            for (var i = 0; i < args.Length; i++)
            {
                matchers[i] = new EqualsMatcher(args[i]);
            }
        }
        else if (queued.TrueForAll(m => m.Position >= 0 && m.Position < args.Length))
        {
            // Positions came from generated interceptors, so there is nothing to guess.
            foreach (var matcher in queued)
            {
                matchers[matcher.Position] = matcher;
            }

            for (var i = 0; i < args.Length; i++)
            {
                matchers[i] ??= new EqualsMatcher(args[i]);
            }
        }
        else if (queued.Count == args.Length)
        {
            queued.CopyTo(matchers);
        }
        else
        {
            // NSubstitute's rule: a queued matcher claims the next argument that holds the default
            // value the matcher call returned. Anything left over is ambiguous.
            var next = 0;

            for (var i = 0; i < args.Length; i++)
            {
                if (next < queued.Count && queued[next].IsDefaultOfMatchedType(args[i]))
                {
                    matchers[i] = queued[next++];
                }
                else
                {
                    matchers[i] = new EqualsMatcher(args[i]);
                }
            }

            if (next != queued.Count)
            {
                throw new AmbiguousArgumentsException(
                    $"Cannot tell which arguments of {memberName} the Arg matchers belong to. Use a matcher for every argument.");
            }
        }

        return new CallSpec(memberId, memberName, typeArguments, matchers);
    }
}

/// <summary>
/// One instance per mock. Holds that mock's recorded calls, configured responses and callbacks.
/// Generated members route every call here.
/// </summary>
public sealed class MockAdapter(string mockName)
{
    private readonly object _gate = new();
    private readonly List<CallRecord> _calls = [];
    private readonly List<(CallSpec Spec, Func<CallInfo, object?> Response)> _responses = [];
    private readonly List<(CallSpec Spec, Action<CallInfo> Callback)> _callbacks = [];
    private (int Min, int Max)? _verifyNext;
    private bool _capturing;
    private CallSpec? _captured;

    public string MockName => mockName;

    public IReadOnlyList<CallRecord> ReceivedCalls()
    {
        lock (_gate)
        {
            return _calls.ToArray();
        }
    }

    public void CallVoid(int memberId, string name, Type[]? typeArgs, object?[] args) =>
        Dispatch(memberId, name, typeArgs, args, out _);

    public TResult Call<TResult>(int memberId, string name, Type[]? typeArgs, object?[] args) =>
        Dispatch(memberId, name, typeArgs, args, out var result) && result is TResult typed ? typed : default!;

    public Task CallTask(int memberId, string name, Type[]? typeArgs, object?[] args) =>
        Dispatch(memberId, name, typeArgs, args, out var result) && result is Task task ? task : Task.CompletedTask;

    public Task<TResult> CallTask<TResult>(int memberId, string name, Type[]? typeArgs, object?[] args) =>
        Dispatch(memberId, name, typeArgs, args, out var result) && result is Task<TResult> task
            ? task
            : Task.FromResult<TResult>(default!);

    public ValueTask CallValueTask(int memberId, string name, Type[]? typeArgs, object?[] args) =>
        Dispatch(memberId, name, typeArgs, args, out var result) && result is ValueTask task ? task : default;

    public ValueTask<TResult> CallValueTask<TResult>(int memberId, string name, Type[]? typeArgs, object?[] args) =>
        Dispatch(memberId, name, typeArgs, args, out var result) && result is ValueTask<TResult> task
            ? task
            : new ValueTask<TResult>(default(TResult)!);

    /// <summary>A property or indexer setter. The assigned value becomes the getter's response.</summary>
    public void SetProperty(int getterId, int setterId, string name, object?[] indexArgs, object? value)
    {
        var all = new object?[indexArgs.Length + 1];
        indexArgs.CopyTo(all, 0);
        all[^1] = value;

        var verifyingOrCapturing = _verifyNext is not null || _capturing;
        Dispatch(setterId, name, null, all, out _);

        if (!verifyingOrCapturing && getterId >= 0)
        {
            var spec = CallSpec.Create(getterId, name, null, indexArgs, null);

            lock (_gate)
            {
                _responses.Add((spec, _ => value));
            }
        }
    }

    /// <summary>
    /// An event add accessor. Returns true when the assignment was <c>Raise.Event</c>, in which case
    /// the generated accessor invokes its handlers with <paramref name="raiseArgs"/>.
    /// </summary>
    public bool EventAdd(int memberId, string name, Delegate? handler, out object?[] raiseArgs)
    {
        var pending = MockContext.TakePendingRaise();

        if (pending is not null)
        {
            raiseArgs = pending;
            return true;
        }

        raiseArgs = [];
        Dispatch(memberId, name, null, [handler], out _);
        return false;
    }

    public void EventRemove(int memberId, string name, Delegate? handler) =>
        Dispatch(memberId, name, null, [handler], out _);

    private bool Dispatch(int memberId, string name, Type[]? typeArgs, object?[] args, out object? result)
    {
        result = null;

        var spec = CallSpec.Create(memberId, name, typeArgs, args, MockContext.TakeMatchers());
        CallRecord record;
        Func<CallInfo, object?>? response = null;
        List<Action<CallInfo>>? callbacks = null;

        lock (_gate)
        {
            if (_verifyNext is { } range)
            {
                _verifyNext = null;
                Verify(spec, range);
                return false;
            }

            if (_capturing)
            {
                _captured = spec;
                return false;
            }

            record = new CallRecord(memberId, name, typeArgs, (object?[])args.Clone());
            _calls.Add(record);

            foreach (var (callbackSpec, callback) in _callbacks)
            {
                if (callbackSpec.Matches(record))
                {
                    (callbacks ??= []).Add(callback);
                }
            }

            for (var i = _responses.Count - 1; i >= 0; i--)
            {
                if (_responses[i].Spec.Matches(record))
                {
                    response = _responses[i].Response;
                    break;
                }
            }
        }

        MockContext.SetLastCall(this, record, spec);

        if (callbacks is null && response is null)
        {
            return false;
        }

        var info = new CallInfo(args, typeArgs);

        if (callbacks is not null)
        {
            foreach (var callback in callbacks)
            {
                callback(info);
            }
        }

        if (response is null)
        {
            return false;
        }

        result = response(info);
        return true;
    }

    internal void BindResponse(CallRecord setupCall, CallSpec spec, Func<CallInfo, object?> response)
    {
        lock (_gate)
        {
            // The call that carried the setup is not a call the system under test made.
            _calls.Remove(setupCall);
            _responses.Add((spec, response));
        }
    }

    internal void VerifyNextCall(int min, int max)
    {
        lock (_gate)
        {
            _verifyNext = (min, max);
        }
    }

    internal CallSpec Capture<T>(T mock, Action<T> call)
    {
        lock (_gate)
        {
            _capturing = true;
            _captured = null;
        }

        try
        {
            call(mock);
        }
        finally
        {
            lock (_gate)
            {
                _capturing = false;
            }
        }

        return _captured ?? throw new InvalidOperationException("When(...) did not call a member of the mock.");
    }

    internal void AddCallback(CallSpec spec, Action<CallInfo> callback)
    {
        lock (_gate)
        {
            _callbacks.Add((spec, callback));
        }
    }

    private void Verify(CallSpec spec, (int Min, int Max) range)
    {
        var count = _calls.Count(spec.Matches);

        if (count >= range.Min && count <= range.Max)
        {
            return;
        }

        var message = new StringBuilder();
        var expected = range switch
        {
            (0, 0) => "no calls",
            (1, int.MaxValue) => "at least one call",
            var (min, max) when min == max => $"exactly {min} call(s)",
            var (min, max) => $"between {min} and {max} calls",
        };
        message.AppendLine($"Expected {expected} to {mockName}.{spec}, but received {count}.");
        message.AppendLine("Calls received:");

        foreach (var call in _calls)
        {
            message.AppendLine($"  {call}");
        }

        throw new ReceivedCallsException(message.ToString());
    }
}
