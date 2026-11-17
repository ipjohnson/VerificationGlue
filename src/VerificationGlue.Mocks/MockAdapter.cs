using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VerificationGlue.Mocks;

/// <summary>
/// Records the calls of one mock and answers them. Every generated member calls its mock's adapter.
/// </summary>
/// <remarks>
/// Each <c>Call</c> method takes the generated member ID, the member name, the type arguments of a
/// generic method or null, and the arguments. Generated code writes <c>ref</c> and <c>out</c>
/// values back from the argument array after the call. An unconfigured call returns the auto-value
/// for its return type: <c>default</c>, a completed <see cref="Task"/> or <see cref="ValueTask"/>,
/// or a recursive mock from <see cref="CallMock{TResult}"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MockAdapter
{
    private const int ListedCallLimit = 20;

    private readonly object _gate = new();
    private readonly List<CallRecord> _calls = [];
    private readonly List<(CallSpec Spec, Func<CallInfo, object?> Response)> _responses = [];
    private readonly List<(CallSpec Spec, Action<CallInfo> Callback)> _callbacks = [];
    private readonly List<(CallSpec Spec, object Mock)> _recursiveMocks = [];

    /// <summary>Creates the adapter for one mock.</summary>
    /// <param name="mockName">The generated mock class name, used in messages.</param>
    public MockAdapter(string mockName)
    {
        MockName = mockName;
    }

    /// <summary>The generated mock class name.</summary>
    public string MockName { get; }

    /// <summary>The calls this mock received, in order. Calls that set up a response or made a check are not included.</summary>
    public IReadOnlyList<CallRecord> ReceivedCalls()
    {
        lock (_gate)
        {
            return _calls.ToArray();
        }
    }

    /// <summary>A call to a member that returns <c>void</c>.</summary>
    public void CallVoid(int memberId, string name, Type[]? typeArguments, object?[] arguments) =>
        Dispatch(memberId, name, typeArguments, arguments, out _);

    /// <summary>A call to a member that returns a value.</summary>
    public TResult Call<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments) =>
        Dispatch(memberId, name, typeArguments, arguments, out var result) && result is TResult typed ? typed : default!;

    /// <summary>
    /// A call to a member whose return type has a generated mock. An unconfigured call returns a
    /// mock of <typeparamref name="TResult"/>, created on the first call and returned again for the
    /// same arguments. A call that <c>Received()</c> checks returns <c>default</c>.
    /// </summary>
    public TResult CallMock<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments)
        where TResult : class
    {
        if (Dispatch(memberId, name, typeArguments, arguments, out var result, out var recorded))
        {
            return result is TResult typed ? typed : default!;
        }

        if (!recorded || MockFactory<TResult>.Create is not { } create)
        {
            return default!;
        }

        var call = new CallRecord(memberId, name, typeArguments ?? Type.EmptyTypes, arguments);
        lock (_gate)
        {
            foreach (var (spec, mock) in _recursiveMocks)
            {
                if (spec.Matches(call) && mock is TResult existing)
                {
                    return existing;
                }
            }

            var created = create();
            _recursiveMocks.Add((CallSpec.Create(memberId, name, typeArguments, (object?[])arguments.Clone(), []), created));
            return created;
        }
    }

    /// <summary>A call to a member that returns by reference. The reference points at a new location holding the response or the auto-value.</summary>
    public ref TResult CallRef<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments)
    {
        var value = Dispatch(memberId, name, typeArguments, arguments, out var result) && result is TResult typed ? typed : default!;
        return ref new StrongBox<TResult>(value).Value!;
    }

    /// <summary>A call to a member that returns <see cref="Task"/>.</summary>
    public Task CallTask(int memberId, string name, Type[]? typeArguments, object?[] arguments) =>
        Dispatch(memberId, name, typeArguments, arguments, out var result) && result is Task task ? task : Task.CompletedTask;

    /// <summary>A call to a member that returns <see cref="Task{TResult}"/>.</summary>
    public Task<TResult> CallTask<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments) =>
        Dispatch(memberId, name, typeArguments, arguments, out var result) && result is Task<TResult> task
            ? task
            : Task.FromResult<TResult>(default!);

    /// <summary>A call to a member that returns <see cref="ValueTask"/>.</summary>
    public ValueTask CallValueTask(int memberId, string name, Type[]? typeArguments, object?[] arguments) =>
        Dispatch(memberId, name, typeArguments, arguments, out var result) && result is ValueTask task ? task : default;

    /// <summary>A call to a member that returns <see cref="ValueTask{TResult}"/>.</summary>
    public ValueTask<TResult> CallValueTask<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments) =>
        Dispatch(memberId, name, typeArguments, arguments, out var result) && result is ValueTask<TResult> task
            ? task
            : new ValueTask<TResult>(default(TResult)!);

    /// <summary>
    /// A property or indexer setter. The recorded arguments are the index arguments followed by the
    /// value. A recorded set makes the getter return the value for the same index arguments.
    /// </summary>
    /// <param name="getterId">The getter's member ID, or -1 when the property has no getter.</param>
    /// <param name="setterId">The setter's member ID.</param>
    /// <param name="name">The setter name, such as <c>set_Mode</c>.</param>
    /// <param name="indexArguments">The indexer arguments, or an empty array for a property.</param>
    /// <param name="value">The assigned value.</param>
    public void SetProperty(int getterId, int setterId, string name, object?[] indexArguments, object? value)
    {
        var arguments = new object?[indexArguments.Length + 1];
        indexArguments.CopyTo(arguments, 0);
        arguments[^1] = value;

        Dispatch(setterId, name, null, arguments, out _, out var recorded);
        if (recorded && getterId >= 0)
        {
            var getter = CallSpec.Create(getterId, "get_" + name.Substring(4), null, indexArguments, []);
            lock (_gate)
            {
                _responses.Add((getter, _ => value));
            }
        }
    }

    /// <summary>
    /// An event add accessor. Returns true when the assignment was <c>Raise.Event</c>. The generated
    /// accessor then invokes its handlers with <paramref name="raiseArguments"/> instead of adding one.
    /// </summary>
    public bool EventAdd(int memberId, string name, Delegate? handler, out object?[] raiseArguments)
    {
        if (MockContext.TakePendingRaise() is { } pending)
        {
            raiseArguments = pending;
            return true;
        }

        raiseArguments = [];
        Dispatch(memberId, name, null, [handler], out _);
        return false;
    }

    /// <summary>An event remove accessor.</summary>
    public void EventRemove(int memberId, string name, Delegate? handler) =>
        Dispatch(memberId, name, null, [handler], out _);

    /// <summary>The adapter of a generated mock.</summary>
    /// <exception cref="ArgumentException"><paramref name="mock"/> is not a generated mock.</exception>
    public static MockAdapter Of(object mock) =>
        mock is IMock generated
            ? generated.Adapter
            : throw new ArgumentException($"{mock?.GetType().ToString() ?? "null"} is not a generated mock. Create one with Mock.Of<T>().", nameof(mock));

    /// <summary>The target of a generated <c>Setup()</c> member that returns a value. Each argument is an <see cref="Arg{T}"/>.</summary>
    public MockSetup<TResult> SetupCall<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments) =>
        new(this, CallSpec.FromMatchers(memberId, name, typeArguments, Responses.Matchers(arguments)));

    /// <summary>The target of a generated <c>Setup()</c> member that returns <c>void</c>. Each argument is an <see cref="Arg{T}"/>.</summary>
    public MockSetup SetupVoidCall(int memberId, string name, Type[]? typeArguments, object?[] arguments) =>
        new(this, CallSpec.FromMatchers(memberId, name, typeArguments, Responses.Matchers(arguments)));

    /// <summary>The target of a generated <c>Calls()</c> member: the number of recorded calls that match. Each argument is an <see cref="Arg{T}"/>.</summary>
    public CallCount CountCalls(int memberId, string name, Type[]? typeArguments, object?[] arguments)
    {
        var spec = CallSpec.FromMatchers(memberId, name, typeArguments, Responses.Matchers(arguments));
        var toMember = new List<CallRecord>();
        var count = 0;
        lock (_gate)
        {
            foreach (var call in _calls)
            {
                if (call.MemberId != memberId)
                {
                    continue;
                }

                toMember.Add(call);
                if (spec.Matches(call))
                {
                    count++;
                }
            }
        }

        return new CallCount(count, name, toMember);
    }

    internal void AddResponse(CallSpec spec, Func<CallInfo, object?> response)
    {
        lock (_gate)
        {
            _responses.Add((spec, response));
        }
    }

    internal void BindResponse(CallRecord setupCall, CallSpec spec, Func<CallInfo, object?> response)
    {
        lock (_gate)
        {
            _calls.Remove(setupCall);
            _responses.Add((spec, response));
        }
    }

    internal void AddCallback(CallSpec spec, Action<CallInfo> callback)
    {
        lock (_gate)
        {
            _callbacks.Add((spec, callback));
        }
    }

    private bool Dispatch(int memberId, string name, Type[]? typeArguments, object?[] arguments, out object? result) =>
        Dispatch(memberId, name, typeArguments, arguments, out result, out _);

    private bool Dispatch(int memberId, string name, Type[]? typeArguments, object?[] arguments, out object? result, out bool recorded)
    {
        result = null;
        recorded = false;

        var queued = MockContext.TakeMatchers();
        var pending = MockContext.TakePending(this);
        var spec = CallSpec.Create(memberId, name, typeArguments, arguments, queued);

        if (pending is { Kind: MockContext.PendingKind.Capture })
        {
            MockContext.SetCaptured(spec);
            return false;
        }

        if (pending is { Kind: MockContext.PendingKind.Check })
        {
            Check(spec, pending.Min, pending.Max);
            return false;
        }

        var record = new CallRecord(memberId, name, typeArguments ?? Type.EmptyTypes, (object?[])arguments.Clone());
        Func<CallInfo, object?>? response = null;
        List<Action<CallInfo>>? callbacks = null;
        lock (_gate)
        {
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

        recorded = true;
        MockContext.SetLastCall(this, record, spec);

        if (callbacks is null && response is null)
        {
            return false;
        }

        var info = new CallInfo(arguments, typeArguments);
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

    private void Check(CallSpec spec, int min, int max)
    {
        CallRecord[] calls;
        lock (_gate)
        {
            calls = _calls.ToArray();
        }

        var count = 0;
        foreach (var call in calls)
        {
            if (spec.Matches(call))
            {
                count++;
            }
        }

        if (count >= min && count <= max)
        {
            return;
        }

        var expected = (min, max) switch
        {
            (0, 0) => "no calls",
            (1, int.MaxValue) => "at least one call",
            (1, 1) => "exactly 1 call",
            _ => $"exactly {min} calls",
        };

        var message = new StringBuilder()
            .Append("Expected ").Append(expected).Append(" to ").Append(MockName).Append('.').Append(spec)
            .Append(", but received ").Append(count).Append('.');
        if (calls.Length == 0)
        {
            message.Append("\nThe mock received no calls.");
        }
        else
        {
            message.Append("\nCalls received:");
            for (var i = 0; i < calls.Length && i < ListedCallLimit; i++)
            {
                message.Append("\n  ").Append(calls[i]).Append(spec.Matches(calls[i]) ? "  (matches)" : "");
            }

            if (calls.Length > ListedCallLimit)
            {
                message.Append("\n  and ").Append(calls.Length - ListedCallLimit).Append(" more");
            }
        }

        throw new ReceivedCallsException(message.ToString());
    }
}
