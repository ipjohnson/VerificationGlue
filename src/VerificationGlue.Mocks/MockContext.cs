using System.Threading;

namespace VerificationGlue.Mocks;

/// <summary>
/// The state that links one setup or verification statement together, such as
/// <c>calc.Add(1, 2).Returns(3)</c>. It is an immutable value in an <see cref="AsyncLocal{T}"/>,
/// replaced on every change, so it flows across <c>await</c>, a child task starts with its parent's
/// state, and a child's changes do not reach its parent.
/// </summary>
internal static class MockContext
{
    private static readonly AsyncLocal<State?> s_state = new();

    private static State Current => s_state.Value ?? State.Empty;

    /// <summary>Queues a matcher for the next mock call in this flow.</summary>
    public static void Enqueue(IArgMatcher matcher)
    {
        var current = Current;
        var matchers = new IArgMatcher[current.Matchers.Length + 1];
        current.Matchers.CopyTo(matchers, 0);
        matchers[^1] = matcher;
        s_state.Value = current with { Matchers = matchers };
    }

    public static IArgMatcher[] TakeMatchers()
    {
        var current = Current;
        if (current.Matchers.Length == 0)
        {
            return current.Matchers;
        }

        s_state.Value = current with { Matchers = [] };
        return current.Matchers;
    }

    public static void SetLastCall(MockAdapter adapter, CallRecord record, CallSpec spec) =>
        s_state.Value = Current with { Last = new LastCall(adapter, record, spec) };

    public static LastCall? TakeLastCall()
    {
        var current = Current;
        if (current.Last is null)
        {
            return null;
        }

        s_state.Value = current with { Last = null };
        return current.Last;
    }

    /// <summary>Makes the next call on <paramref name="adapter"/> in this flow a check against its recorded calls.</summary>
    public static void SetPendingCheck(MockAdapter adapter, int min, int max) =>
        s_state.Value = Current with { Pending = new Pending(adapter, PendingKind.Check, min, max) };

    /// <summary>Makes the next call on <paramref name="adapter"/> in this flow a capture for <c>When</c>.</summary>
    public static void BeginCapture(MockAdapter adapter) =>
        s_state.Value = Current with { Pending = new Pending(adapter, PendingKind.Capture, 0, 0), Captured = null };

    /// <summary>The pending check or capture, when it belongs to <paramref name="adapter"/>.</summary>
    public static Pending? TakePending(MockAdapter adapter)
    {
        var current = Current;
        if (current.Pending is not { } pending || !ReferenceEquals(pending.Adapter, adapter))
        {
            return null;
        }

        s_state.Value = current with { Pending = null };
        return pending;
    }

    public static void SetCaptured(CallSpec spec) => s_state.Value = Current with { Captured = spec };

    /// <summary>Ends a capture and returns the call it saw, or null when the action made no call on the mock.</summary>
    public static CallSpec? EndCapture(MockAdapter adapter)
    {
        var current = Current;
        var captured = current.Captured;
        var pending = current.Pending is { Kind: PendingKind.Capture } open && ReferenceEquals(open.Adapter, adapter) ? null : current.Pending;
        s_state.Value = current with { Pending = pending, Captured = null };
        return captured;
    }

    public static void SetPendingRaise(object?[] arguments) => s_state.Value = Current with { Raise = arguments };

    public static object?[]? TakePendingRaise()
    {
        var current = Current;
        if (current.Raise is null)
        {
            return null;
        }

        s_state.Value = current with { Raise = null };
        return current.Raise;
    }

    internal enum PendingKind
    {
        Check,
        Capture,
    }

    internal sealed record LastCall(MockAdapter Adapter, CallRecord Record, CallSpec Spec);

    internal sealed record Pending(MockAdapter Adapter, PendingKind Kind, int Min, int Max);

    private sealed record State(IArgMatcher[] Matchers, LastCall? Last, Pending? Pending, CallSpec? Captured, object?[]? Raise)
    {
        public static readonly State Empty = new([], null, null, null, null);
    }
}
