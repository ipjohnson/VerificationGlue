using System;
using System.Collections.Generic;

namespace VerificationGlue.Mocks;

/// <summary>One call a mock received.</summary>
/// <param name="MemberId">The generated member ID. A property setter and an event remove accessor have the ID after their getter or add accessor.</param>
/// <param name="MemberName">The member name, such as <c>Add</c>, <c>get_Mode</c> or <c>add_Changed</c>.</param>
/// <param name="TypeArguments">The type arguments of a generic method call, or an empty array.</param>
/// <param name="Arguments">The arguments, with a setter's value last.</param>
public sealed record CallRecord(int MemberId, string MemberName, IReadOnlyList<Type> TypeArguments, IReadOnlyList<object?> Arguments)
{
    /// <inheritdoc />
    public override string ToString()
    {
        var typeArguments = TypeArguments.Count == 0 ? "" : "<" + string.Join(", ", Names(TypeArguments)) + ">";
        return MemberName + typeArguments + "(" + string.Join(", ", Values(Arguments)) + ")";
    }

    private static IEnumerable<string> Names(IReadOnlyList<Type> types)
    {
        foreach (var type in types)
        {
            yield return type.Name;
        }
    }

    private static IEnumerable<string> Values(IReadOnlyList<object?> values)
    {
        foreach (var value in values)
        {
            yield return value?.ToString() ?? "null";
        }
    }
}
