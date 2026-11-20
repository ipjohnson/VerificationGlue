using System;
using System.Threading.Tasks;

namespace VerificationGlue.Mocks;

/// <summary>
/// NSubstitute-style setup and verification. A setup applies to the last mock call made in the
/// same async flow: <c>calc.Add(1, 2).Returns(3)</c>.
/// </summary>
public static class MockSyntax
{
    /// <summary>Makes the mock call before this one return <paramref name="value"/>.</summary>
    public static void Returns<T>(this T call, T value) => Bind(_ => value);

    /// <summary>Makes the mock call before this one return each value in turn, repeating the last.</summary>
    public static void Returns<T>(this T call, T first, params T[] then) => Bind(Responses.Sequence(first, then));

    /// <summary>Makes the mock call before this one return the result of <paramref name="factory"/>, which can also set <c>out</c> and <c>ref</c> arguments.</summary>
    public static void Returns<T>(this T call, Func<CallInfo, T> factory) => Bind(info => factory(info));

    /// <summary>Makes the mock call before this one return a completed task with <paramref name="value"/>.</summary>
    public static void Returns<T>(this Task<T> call, T value) => Bind(_ => Task.FromResult(value));

    /// <summary>Makes the mock call before this one return a completed value task with <paramref name="value"/>.</summary>
    public static void Returns<T>(this ValueTask<T> call, T value) => Bind(_ => new ValueTask<T>(value));

    /// <summary>Makes the mock call before this one throw <paramref name="exception"/>.</summary>
    public static void Throws<T>(this T call, Exception exception) => Bind(_ => throw exception);

    /// <summary>Makes the next call on <paramref name="mock"/> in this flow check that it was received at least once.</summary>
    public static T Received<T>(this T mock)
        where T : class => CheckNext(mock, 1, int.MaxValue);

    /// <summary>Makes the next call on <paramref name="mock"/> in this flow check that it was received exactly <paramref name="count"/> times.</summary>
    public static T Received<T>(this T mock, int count)
        where T : class
    {
        if (count < 0)
        {
            MockContext.Clear();
            throw new ArgumentOutOfRangeException(nameof(count), count, "The count cannot be negative.");
        }

        return CheckNext(mock, count, count);
    }

    /// <summary>Makes the next call on <paramref name="mock"/> in this flow check that it was never received.</summary>
    public static T DidNotReceive<T>(this T mock)
        where T : class => CheckNext(mock, 0, 0);

    /// <summary>Selects the call <paramref name="call"/> makes on <paramref name="mock"/>, for a callback added with <see cref="WhenCall.Do"/>.</summary>
    public static WhenCall When<T>(this T mock, Action<T> call)
        where T : class
    {
        var adapter = AdapterOf(mock);
        MockContext.BeginCapture(adapter);
        CallSpec? spec;
        try
        {
            call(mock);
        }
        finally
        {
            spec = MockContext.EndCapture(adapter);
        }

        return spec is null
            ? throw Misuse(new MockSetupException("When(...) did not call a member of the mock it was given."))
            : new WhenCall(adapter, spec);
    }

    private static void Bind(Func<CallInfo, object?> response)
    {
        var last = MockContext.TakeLastCall() ?? throw Misuse(new MockSetupException(
            "Returns or Throws was called, but no mock call came before it in this async flow. " +
            "Call it directly on a mock call, such as calc.Add(1, 2).Returns(3)."));
        last.Adapter.BindResponse(last.Record, last.Spec, response);
    }

    /// <summary>Clears the flow's pending state, so the next mock operation starts from nothing, and returns <paramref name="exception"/> to throw.</summary>
    private static Exception Misuse(Exception exception)
    {
        MockContext.Clear();
        return exception;
    }

    private static T CheckNext<T>(T mock, int min, int max)
        where T : class
    {
        MockContext.SetPendingCheck(AdapterOf(mock), min, max);
        return mock;
    }

    private static MockAdapter AdapterOf(object mock) =>
        mock is IMock generated ? generated.Adapter : throw Misuse(MockAdapter.NotAMock(mock));
}

/// <summary>A call selected with <see cref="MockSyntax.When{T}"/>.</summary>
public sealed class WhenCall
{
    private readonly MockAdapter _adapter;
    private readonly CallSpec _spec;

    internal WhenCall(MockAdapter adapter, CallSpec spec)
    {
        _adapter = adapter;
        _spec = spec;
    }

    /// <summary>Runs <paramref name="callback"/> on each matching call, before the call returns.</summary>
    public void Do(Action<CallInfo> callback) => _adapter.AddCallback(_spec, callback);

    /// <summary>Makes each matching call throw <paramref name="exception"/>.</summary>
    public void Throw(Exception exception) => _adapter.AddCallback(_spec, _ => throw exception);
}
