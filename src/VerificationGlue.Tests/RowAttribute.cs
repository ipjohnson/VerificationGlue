using System;
using System.Collections.Generic;

namespace VerificationGlue.Tests;

/// <summary>
/// One data row of a <c>[VerifyTest]</c> method. Each row is its own test with its own result and
/// container. The values go to the method's first parameters, in order. The other parameters are
/// resolved as usual. A value that does not match its parameter type fails the build with VGT0002.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class RowAttribute : Attribute
{
    /// <summary>Creates a row.</summary>
    /// <param name="values">The values of the method's first parameters, in order.</param>
    public RowAttribute(params object?[]? values) => Values = values ?? [null];

    /// <summary>The values of the method's first parameters, in order.</summary>
    public IReadOnlyList<object?> Values { get; }
}
