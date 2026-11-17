using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace VerificationGlue.Mocks;

/// <summary>
/// Argument matchers for setup and verification, such as <c>calc.Add(Arg.Any&lt;int&gt;(), 2)</c>.
/// Pass a matcher directly as an argument of the mock call. The generator then tells the matcher
/// which parameter it belongs to.
/// </summary>
/// <remarks>
/// Each matcher returns a reference to a new default value, so it can also stand in for an
/// <c>out</c> or <c>ref</c> argument: <c>calc.TryParse("7", out Arg.Any&lt;int&gt;())</c>.
/// </remarks>
public static class Arg
{
    /// <summary>Matches any value of type <typeparamref name="T"/>, including null.</summary>
    public static ref T Any<T>() => ref AnyAt<T>(-1);

    /// <summary>Matches a value for which <paramref name="predicate"/> returns true.</summary>
    public static ref T Is<T>(Func<T, bool> predicate) => ref IsAt(-1, predicate);

    /// <summary>Matches a value equal to <paramref name="value"/>.</summary>
    public static ref T Is<T>(T value) => ref IsAt(-1, value);

    /// <summary>The target of a generated interceptor for <see cref="Any{T}"/>, with the parameter ordinal read from the call site.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ref T AnyAt<T>(int position)
    {
        MockContext.Enqueue(new PredicateMatcher<T>(null, "Arg.Any<" + typeof(T).Name + ">()", position));
        return ref new Slot<T>().Value;
    }

    /// <summary>The target of a generated interceptor for <see cref="Is{T}(Func{T, bool})"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ref T IsAt<T>(int position, Func<T, bool> predicate)
    {
        MockContext.Enqueue(new PredicateMatcher<T>(predicate, "Arg.Is<" + typeof(T).Name + ">(predicate)", position));
        return ref new Slot<T>().Value;
    }

    /// <summary>The target of a generated interceptor for <see cref="Is{T}(T)"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ref T IsAt<T>(int position, T value)
    {
        MockContext.Enqueue(new PredicateMatcher<T>(
            candidate => EqualityComparer<T>.Default.Equals(candidate, value),
            "Arg.Is(" + (value?.ToString() ?? "null") + ")",
            position));
        return ref new Slot<T>().Value;
    }

    private sealed class Slot<T>
    {
        public T Value = default!;
    }
}
