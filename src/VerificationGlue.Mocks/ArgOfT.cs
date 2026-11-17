using System;
using System.Threading;

namespace VerificationGlue.Mocks;

/// <summary>Gives the matcher of an <see cref="Arg{T}"/> to the adapter without making it public.</summary>
internal interface IArgSource
{
    IArgMatcher Matcher { get; }
}

/// <summary>
/// An argument of a typed <c>Setup()</c> or <c>Calls()</c> member, such as
/// <c>calc.Setup().Add(1, Arg&lt;int&gt;.Any())</c>. A value converts to an exact match, and
/// <see cref="Any"/> and <see cref="Is(Func{T, bool})"/> give matchers. Nothing is queued, so the
/// position of each argument is known without an interceptor.
/// </summary>
/// <remarks>
/// C# applies no user-defined conversion from an interface type. For a parameter of an interface
/// type, match an exact value with <see cref="Is(T)"/>.
/// </remarks>
/// <typeparam name="T">The parameter type.</typeparam>
public readonly struct Arg<T> : IArgSource
{
    private readonly IArgMatcher? _matcher;

    private Arg(IArgMatcher matcher)
    {
        _matcher = matcher;
    }

    /// <summary>Matches any value of type <typeparamref name="T"/>, including null.</summary>
    public static Arg<T> Any() => new(new PredicateMatcher<T>(null, "Arg<" + typeof(T).Name + ">.Any()", -1));

    /// <summary>Matches a value for which <paramref name="predicate"/> returns true.</summary>
    public static Arg<T> Is(Func<T, bool> predicate) =>
        new(new PredicateMatcher<T>(predicate ?? throw new ArgumentNullException(nameof(predicate)), "Arg<" + typeof(T).Name + ">.Is(predicate)", -1));

    /// <summary>Matches a value equal to <paramref name="value"/>.</summary>
    public static Arg<T> Is(T value) => new(new EqualsMatcher(value));

    /// <summary>Matches a value equal to <paramref name="value"/>.</summary>
    public static implicit operator Arg<T>(T value) => new(new EqualsMatcher(value));

    /// <summary>A <c>default</c> argument matches the default value of <typeparamref name="T"/>.</summary>
    IArgMatcher IArgSource.Matcher => _matcher ?? new EqualsMatcher(default(T));

    /// <inheritdoc />
    public override string ToString() => ((IArgSource)this).Matcher.ToString()!;
}

/// <summary>Builds the responses that both setup styles share.</summary>
internal static class Responses
{
    /// <summary>Returns each value in turn, repeating the last.</summary>
    public static Func<CallInfo, object?> Sequence<T>(T first, T[] then)
    {
        var index = -1;
        return _ =>
        {
            var next = Interlocked.Increment(ref index);
            return next == 0 || then.Length == 0 ? first : then[Math.Min(next - 1, then.Length - 1)];
        };
    }

    public static IArgMatcher[] Matchers(object?[] arguments)
    {
        var matchers = new IArgMatcher[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            matchers[i] = arguments[i] is IArgSource source ? source.Matcher : new EqualsMatcher(arguments[i]);
        }

        return matchers;
    }
}
