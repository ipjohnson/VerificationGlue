using System;

namespace VerificationGlue.Mocks;

/// <summary>Raises an event of a mock: <c>calc.Changed += Raise.Event&lt;EventHandler&lt;int&gt;&gt;(calc, 5)</c>.</summary>
public static class Raise
{
    /// <summary>
    /// Raises the event the result is added to, passing <paramref name="arguments"/> to its
    /// handlers. Give one argument for each parameter of <typeparamref name="THandler"/>.
    /// </summary>
    public static RaiseEvent<THandler> Event<THandler>(params object?[] arguments)
        where THandler : Delegate
    {
        MockContext.SetPendingRaise(arguments);
        return default;
    }
}

/// <summary>The value <see cref="Raise.Event{THandler}"/> returns. Adding it to a mock's event raises the event.</summary>
public readonly struct RaiseEvent<THandler>
    where THandler : Delegate
{
    /// <summary>
    /// Hands the event's add accessor a null handler. The arguments travel in the mock context, and
    /// the generated accessor invokes the event's handlers with them.
    /// </summary>
    public static implicit operator THandler(RaiseEvent<THandler> raise) => null!;
}
