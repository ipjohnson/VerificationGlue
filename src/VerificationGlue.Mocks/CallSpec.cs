using System;
using System.Collections.Generic;

namespace VerificationGlue.Mocks;

internal interface IArgMatcher
{
    /// <summary>The parameter ordinal a generated interceptor read from the syntax, or -1 when it is not known.</summary>
    int Position { get; }

    bool Matches(object? value);

    /// <summary>True when <paramref name="value"/> is the default of the matched type, which is what the matcher call returned.</summary>
    bool IsDefaultOfMatchedType(object? value);
}

internal sealed class EqualsMatcher(object? expected) : IArgMatcher
{
    public int Position => -1;

    public bool Matches(object? value) => Equals(expected, value);

    public bool IsDefaultOfMatchedType(object? value) => false;

    public override string ToString() => expected?.ToString() ?? "null";
}

internal sealed class PredicateMatcher<T>(Func<T, bool>? predicate, string text, int position) : IArgMatcher
{
    public int Position => position;

    public bool Matches(object? value) => value switch
    {
        T typed => predicate is null || predicate(typed),
        null => default(T) is null && (predicate is null || predicate(default!)),
        _ => false,
    };

    public bool IsDefaultOfMatchedType(object? value) =>
        value is null ? default(T) is null : value is T typed && EqualityComparer<T>.Default.Equals(typed, default!);

    public override string ToString() => text;
}

/// <summary>Which calls a setup, a callback or a check applies to: a member, its type arguments and one matcher per argument.</summary>
internal sealed class CallSpec
{
    private readonly Type[] _typeArguments;
    private readonly IArgMatcher[] _matchers;

    private CallSpec(int memberId, string memberName, Type[] typeArguments, IArgMatcher[] matchers)
    {
        MemberId = memberId;
        MemberName = memberName;
        _typeArguments = typeArguments;
        _matchers = matchers;
    }

    public int MemberId { get; }

    public string MemberName { get; }

    public bool Matches(CallRecord call)
    {
        if (call.MemberId != MemberId || call.Arguments.Count != _matchers.Length || call.TypeArguments.Count != _typeArguments.Length)
        {
            return false;
        }

        for (var i = 0; i < _typeArguments.Length; i++)
        {
            if (call.TypeArguments[i] != _typeArguments[i])
            {
                return false;
            }
        }

        for (var i = 0; i < _matchers.Length; i++)
        {
            if (!_matchers[i].Matches(call.Arguments[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString()
    {
        var typeArguments = _typeArguments.Length == 0 ? "" : "<" + string.Join(", ", Array.ConvertAll(_typeArguments, type => type.Name)) + ">";
        return MemberName + typeArguments + "(" + string.Join(", ", (object[])_matchers) + ")";
    }

    /// <summary>A spec with one matcher per argument, each already in its place, as the typed surface gives them.</summary>
    public static CallSpec FromMatchers(int memberId, string memberName, Type[]? typeArguments, IArgMatcher[] matchers) =>
        new(memberId, memberName, typeArguments ?? Type.EmptyTypes, matchers);

    /// <summary>
    /// Gives each queued matcher an argument. A matcher with a position from a generated
    /// interceptor takes that argument. The others take the free arguments in order when there is
    /// one for each, and otherwise follow NSubstitute's rule: each claims the next free argument that
    /// holds the default value the matcher call returned.
    /// </summary>
    public static CallSpec Create(int memberId, string memberName, Type[]? typeArguments, object?[] arguments, IArgMatcher[] queued)
    {
        var matchers = new IArgMatcher?[arguments.Length];
        var unplaced = new List<IArgMatcher>();
        foreach (var matcher in queued)
        {
            if (matcher.Position >= 0 && matcher.Position < arguments.Length && matchers[matcher.Position] is null)
            {
                matchers[matcher.Position] = matcher;
            }
            else
            {
                unplaced.Add(matcher);
            }
        }

        var free = new List<int>();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (matchers[i] is null)
            {
                free.Add(i);
            }
        }

        if (unplaced.Count > 0 && unplaced.Count == free.Count)
        {
            for (var i = 0; i < free.Count; i++)
            {
                matchers[free[i]] = unplaced[i];
            }
        }
        else if (unplaced.Count > 0)
        {
            var next = 0;
            foreach (var index in free)
            {
                if (next < unplaced.Count && unplaced[next].IsDefaultOfMatchedType(arguments[index]))
                {
                    matchers[index] = unplaced[next++];
                }
            }

            if (next != unplaced.Count)
            {
                throw new AmbiguousArgumentsException(
                    $"Cannot tell which arguments of {memberName} the Arg matchers {string.Join(", ", unplaced)} belong to. " +
                    "Pass each Arg matcher directly as an argument of the mock call, or use a matcher for every argument.");
            }
        }

        var result = new IArgMatcher[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            result[i] = matchers[i] ?? new EqualsMatcher(arguments[i]);
        }

        return new CallSpec(memberId, memberName, typeArguments ?? Type.EmptyTypes, result);
    }
}
