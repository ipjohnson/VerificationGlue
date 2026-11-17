using System;
using System.Threading.Tasks;

namespace VerificationGlue.Mocks;

/// <summary>
/// The setup of a member that returns <typeparamref name="TResult"/>, from a typed <c>Setup()</c>
/// member such as <c>calc.Setup().Add(1, Arg&lt;int&gt;.Any())</c>.
/// </summary>
/// <typeparam name="TResult">The member's return type.</typeparam>
public sealed class MockSetup<TResult>
{
    private readonly MockAdapter _adapter;
    private readonly CallSpec _spec;

    internal MockSetup(MockAdapter adapter, CallSpec spec)
    {
        _adapter = adapter;
        _spec = spec;
    }

    /// <summary>Makes each matching call return <paramref name="value"/>.</summary>
    public void Returns(TResult value) => _adapter.AddResponse(_spec, _ => value);

    /// <summary>Makes the matching calls return each value in turn, repeating the last.</summary>
    public void Returns(TResult first, params TResult[] then) => _adapter.AddResponse(_spec, Responses.Sequence(first, then));

    /// <summary>Makes each matching call return the result of <paramref name="factory"/>, which can also set <c>out</c> and <c>ref</c> arguments.</summary>
    public void Returns(Func<CallInfo, TResult> factory) => _adapter.AddResponse(_spec, info => factory(info));

    /// <summary>Makes each matching call throw <paramref name="exception"/>.</summary>
    public void Throws(Exception exception) => _adapter.AddResponse(_spec, _ => throw exception);

    /// <summary>Runs <paramref name="callback"/> on each matching call, before the call returns.</summary>
    /// <returns>This setup, so a response can follow.</returns>
    public MockSetup<TResult> Does(Action<CallInfo> callback)
    {
        _adapter.AddCallback(_spec, callback);
        return this;
    }
}

/// <summary>The setup of a member that returns <c>void</c>, from a typed <c>Setup()</c> member such as <c>calc.Setup().Reset()</c>.</summary>
public sealed class MockSetup
{
    private readonly MockAdapter _adapter;
    private readonly CallSpec _spec;

    internal MockSetup(MockAdapter adapter, CallSpec spec)
    {
        _adapter = adapter;
        _spec = spec;
    }

    /// <summary>Makes each matching call throw <paramref name="exception"/>.</summary>
    public void Throws(Exception exception) => _adapter.AddResponse(_spec, _ => throw exception);

    /// <summary>Runs <paramref name="callback"/> on each matching call, before the call returns.</summary>
    /// <returns>This setup.</returns>
    public MockSetup Does(Action<CallInfo> callback)
    {
        _adapter.AddCallback(_spec, callback);
        return this;
    }
}

/// <summary>Setups that wrap a value in the task an async member returns.</summary>
public static class MockSetupExtensions
{
    /// <summary>Makes each matching call return a completed task with <paramref name="value"/>.</summary>
    public static void Returns<T>(this MockSetup<Task<T>> setup, T value) => setup.Returns(_ => Task.FromResult(value));

    /// <summary>Makes each matching call return a completed value task with <paramref name="value"/>.</summary>
    public static void Returns<T>(this MockSetup<ValueTask<T>> setup, T value) => setup.Returns(_ => new ValueTask<T>(value));
}
