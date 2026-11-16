using System.Collections.Concurrent;

namespace Glue.Mocks;

internal interface IArgMatcher
{
    /// <summary>The argument position a generated interceptor supplied, or -1 when unknown.</summary>
    int Position { get; }

    bool Matches(object? value);

    bool IsDefaultOfMatchedType(object? value);
}

internal sealed class EqualsMatcher(object? expected) : IArgMatcher
{
    public int Position => -1;

    public bool Matches(object? value) => Equals(expected, value);

    public bool IsDefaultOfMatchedType(object? value) => false;

    public override string ToString() => expected?.ToString() ?? "null";
}

internal sealed class PredicateMatcher<T>(Func<T, bool>? predicate, string text, int position = -1) : IArgMatcher
{
    public int Position => position;

    public bool Matches(object? value) =>
        value is T typed ? predicate is null || predicate(typed) : value is null && default(T) is null && (predicate is null || predicate(default!));

    public bool IsDefaultOfMatchedType(object? value) =>
        value is null ? default(T) is null : value is T typed && EqualityComparer<T>.Default.Equals(typed, default!);

    public override string ToString() => text;
}

/// <summary>
/// The ambient state that links <c>mock.Member(args).Returns(value)</c> together. It is per thread:
/// the member call and the <c>Returns</c> call are two halves of one statement.
/// </summary>
internal static class MockContext
{
    [ThreadStatic]
    private static List<IArgMatcher>? t_matchers;

    [ThreadStatic]
    private static (MockAdapter Adapter, CallRecord Record, CallSpec Spec)? t_lastCall;

    [ThreadStatic]
    private static object?[]? t_pendingRaise;

    public static void Enqueue(IArgMatcher matcher) => (t_matchers ??= []).Add(matcher);

    public static List<IArgMatcher>? TakeMatchers()
    {
        var matchers = t_matchers;
        t_matchers = null;
        return matchers;
    }

    public static void SetLastCall(MockAdapter adapter, CallRecord record, CallSpec spec) =>
        t_lastCall = (adapter, record, spec);

    public static void BindLastCall(Func<CallInfo, object?> response)
    {
        var last = t_lastCall ?? throw new NoLastCallException();
        t_lastCall = null;
        last.Adapter.BindResponse(last.Record, last.Spec, response);
    }

    public static void SetPendingRaise(object?[] args) => t_pendingRaise = args;

    public static object?[]? TakePendingRaise()
    {
        var args = t_pendingRaise;
        t_pendingRaise = null;
        return args;
    }
}

public static class Arg
{
    private sealed class Slot<T>
    {
        public T Value = default!;
    }

    // Returning by ref lets a matcher stand in for an out or ref argument: Foo(out Arg.Any<int>()).
    public static ref T Any<T>()
    {
        MockContext.Enqueue(new PredicateMatcher<T>(null, $"any {typeof(T).Name}"));
        return ref new Slot<T>().Value;
    }

    public static ref T Is<T>(Func<T, bool> predicate)
    {
        MockContext.Enqueue(new PredicateMatcher<T>(predicate, $"{typeof(T).Name} matching predicate"));
        return ref new Slot<T>().Value;
    }

    public static ref T Is<T>(T value)
    {
        MockContext.Enqueue(new PredicateMatcher<T>(v => EqualityComparer<T>.Default.Equals(v, value), $"{value}"));
        return ref new Slot<T>().Value;
    }

    // Targets of generated interceptors. The generator reads the argument position from syntax.
    public static ref T AnyAt<T>(int position)
    {
        MockContext.Enqueue(new PredicateMatcher<T>(null, $"any {typeof(T).Name}", position));
        return ref new Slot<T>().Value;
    }

    public static ref T IsAt<T>(int position, Func<T, bool> predicate)
    {
        MockContext.Enqueue(new PredicateMatcher<T>(predicate, $"{typeof(T).Name} matching predicate", position));
        return ref new Slot<T>().Value;
    }

    public static ref T IsAt<T>(int position, T value)
    {
        MockContext.Enqueue(new PredicateMatcher<T>(v => EqualityComparer<T>.Default.Equals(v, value), $"{value}", position));
        return ref new Slot<T>().Value;
    }
}

public readonly struct RaiseEvent<THandler>
    where THandler : Delegate
{
    // The conversion hands the event's add accessor a null handler. The arguments travel in the
    // thread's context, and the generated accessor invokes its typed delegate field with them.
    public static implicit operator THandler(RaiseEvent<THandler> _) => null!;
}

public static class Raise
{
    public static RaiseEvent<THandler> Event<THandler>(params object?[] args)
        where THandler : Delegate
    {
        MockContext.SetPendingRaise(args);
        return default;
    }
}

public sealed class WhenCall
{
    private readonly MockAdapter _adapter;
    private readonly CallSpec _spec;

    internal WhenCall(MockAdapter adapter, CallSpec spec)
    {
        _adapter = adapter;
        _spec = spec;
    }

    public void Do(Action<CallInfo> callback) => _adapter.AddCallback(_spec, callback);

    public void Throw(Exception exception) => _adapter.AddCallback(_spec, _ => throw exception);
}

public static class MockSyntax
{
    public static void Returns<T>(this T _, T value) => MockContext.BindLastCall(_ => value);

    public static void Returns<T>(this T _, T first, params T[] then)
    {
        var index = -1;
        MockContext.BindLastCall(_ =>
        {
            var i = Interlocked.Increment(ref index);
            return i == 0 ? first : then[Math.Min(i - 1, then.Length - 1)];
        });
    }

    public static void Returns<T>(this T _, Func<CallInfo, T> factory) => MockContext.BindLastCall(info => factory(info));

    public static void Returns<T>(this Task<T> _, T value) => MockContext.BindLastCall(_ => Task.FromResult(value));

    public static void Returns<T>(this ValueTask<T> _, T value) => MockContext.BindLastCall(_ => new ValueTask<T>(value));

    public static void Throws<T>(this T _, Exception exception) => MockContext.BindLastCall(_ => throw exception);

    public static T Received<T>(this T mock)
        where T : class => VerifyNext(mock, 1, int.MaxValue);

    public static T Received<T>(this T mock, int count)
        where T : class => VerifyNext(mock, count, count);

    public static T DidNotReceive<T>(this T mock)
        where T : class => VerifyNext(mock, 0, 0);

    public static WhenCall When<T>(this T mock, Action<T> call)
        where T : class
    {
        var adapter = AdapterOf(mock);
        return new WhenCall(adapter, adapter.Capture(mock, call));
    }

    public static IReadOnlyList<CallRecord> ReceivedCalls<T>(this T mock)
        where T : class => AdapterOf(mock).ReceivedCalls();

    private static T VerifyNext<T>(T mock, int min, int max)
        where T : class
    {
        AdapterOf(mock).VerifyNextCall(min, max);
        return mock;
    }

    private static MockAdapter AdapterOf(object mock) =>
        mock is IMock generated
            ? generated.Adapter
            : throw new InvalidOperationException($"{mock.GetType()} is not a generated mock.");
}

/// <summary>Marks a test parameter that should receive a generated mock.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field)]
public class MockAttribute : Attribute;

public static class MockFactory<T>
    where T : class
{
    public static Func<T>? Create;
}

/// <summary>
/// Filled by a generated module initializer. It serves callers that only have a <see cref="Type"/>,
/// such as a test framework's parameter hook.
/// </summary>
public static class MockRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object>> s_factories = new();

    public static void Register<T>(Func<T> factory)
        where T : class
    {
        MockFactory<T>.Create = factory;
        s_factories[typeof(T)] = factory;
    }

    public static object Create(Type type) =>
        s_factories.TryGetValue(type, out var factory)
            ? factory()
            : throw new InvalidOperationException($"No mock was generated for {type}.");
}

public static class Mock
{
    /// <summary>Resolved through the generated registry. Works for a type argument the call site does not name.</summary>
    public static T Of<T>()
        where T : class =>
        MockFactory<T>.Create is { } create
            ? create()
            : throw new InvalidOperationException($"No mock was generated for {typeof(T)}.");
}

public static class Substitute
{
    /// <summary>Every call site is replaced by a generated interceptor. This body never runs.</summary>
    public static T For<T>()
        where T : class =>
        throw new InvalidOperationException($"Substitute.For<{typeof(T).Name}>() was not intercepted.");
}
