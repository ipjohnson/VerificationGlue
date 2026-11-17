using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace VerificationGlue.Mocks;

/// <summary>
/// Records the calls of one mock and answers them. Every generated member calls its mock's adapter.
/// </summary>
/// <remarks>
/// Each <c>Call</c> method takes the generated member ID, the member name, the type arguments of a
/// generic method or null, and the arguments. Generated code writes <c>ref</c> and <c>out</c>
/// values back from the argument array after the call. An unconfigured call returns the auto-value
/// for its return type: <c>default</c>, or a completed <see cref="Task"/> or <see cref="ValueTask"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MockAdapter
{
    private readonly object _gate = new();
    private readonly List<CallRecord> _calls = [];

    /// <summary>Creates the adapter for one mock.</summary>
    /// <param name="mockName">The generated mock class name, used in messages.</param>
    public MockAdapter(string mockName)
    {
        MockName = mockName;
    }

    /// <summary>The generated mock class name.</summary>
    public string MockName { get; }

    /// <summary>The calls this mock received, in order.</summary>
    public IReadOnlyList<CallRecord> ReceivedCalls()
    {
        lock (_gate)
        {
            return _calls.ToArray();
        }
    }

    /// <summary>A call to a member that returns <c>void</c>.</summary>
    public void CallVoid(int memberId, string name, Type[]? typeArguments, object?[] arguments) =>
        Record(memberId, name, typeArguments, arguments);

    /// <summary>A call to a member that returns a value.</summary>
    public TResult Call<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments)
    {
        Record(memberId, name, typeArguments, arguments);
        return default!;
    }

    /// <summary>A call to a member that returns by reference. The reference points at a new location holding the auto-value.</summary>
    public ref TResult CallRef<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments)
    {
        Record(memberId, name, typeArguments, arguments);
        return ref new StrongBox<TResult>(default!).Value!;
    }

    /// <summary>A call to a member that returns <see cref="Task"/>.</summary>
    public Task CallTask(int memberId, string name, Type[]? typeArguments, object?[] arguments)
    {
        Record(memberId, name, typeArguments, arguments);
        return Task.CompletedTask;
    }

    /// <summary>A call to a member that returns <see cref="Task{TResult}"/>.</summary>
    public Task<TResult> CallTask<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments)
    {
        Record(memberId, name, typeArguments, arguments);
        return Task.FromResult<TResult>(default!);
    }

    /// <summary>A call to a member that returns <see cref="ValueTask"/>.</summary>
    public ValueTask CallValueTask(int memberId, string name, Type[]? typeArguments, object?[] arguments)
    {
        Record(memberId, name, typeArguments, arguments);
        return default;
    }

    /// <summary>A call to a member that returns <see cref="ValueTask{TResult}"/>.</summary>
    public ValueTask<TResult> CallValueTask<TResult>(int memberId, string name, Type[]? typeArguments, object?[] arguments)
    {
        Record(memberId, name, typeArguments, arguments);
        return new ValueTask<TResult>(default(TResult)!);
    }

    /// <summary>A property or indexer setter. The recorded arguments are the index arguments followed by the value.</summary>
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
        Record(setterId, name, null, arguments);
    }

    /// <summary>
    /// An event add accessor. Returns true when the accessor must raise the event with
    /// <paramref name="raiseArguments"/> instead of adding the handler.
    /// </summary>
    public bool EventAdd(int memberId, string name, Delegate? handler, out object?[] raiseArguments)
    {
        raiseArguments = [];
        Record(memberId, name, null, [handler]);
        return false;
    }

    /// <summary>An event remove accessor.</summary>
    public void EventRemove(int memberId, string name, Delegate? handler) =>
        Record(memberId, name, null, [handler]);

    private void Record(int memberId, string name, Type[]? typeArguments, object?[] arguments)
    {
        var record = new CallRecord(memberId, name, typeArguments ?? Type.EmptyTypes, (object?[])arguments.Clone());

        lock (_gate)
        {
            _calls.Add(record);
        }
    }
}
