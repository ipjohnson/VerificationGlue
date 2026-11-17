using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace VerificationGlue;

/// <summary>Marks a method whose <c>Verify</c> calls tier 3 rewrites into a <c>__Verified</c> copy.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class VerifyAttribute : Attribute
{
}

/// <summary>Marks an attribute whose methods the runner always calls through their copy.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class VerifyRewriteAttribute : Attribute
{
}

/// <summary>Stands in for <c>[VerifyTest]</c> from <c>VerificationGlue.Tests</c>.</summary>
[VerifyRewrite]
[AttributeUsage(AttributeTargets.Method)]
public sealed class VerifyTestAttribute : Attribute
{
}

/// <summary>The marker the generator puts on each copy, with the contract version.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class VerifiedCopyAttribute(int contractVersion) : Attribute
{
    public int ContractVersion { get; } = contractVersion;
}

public sealed class VerifyException(string message) : Exception(message);

public static class VerifySyntax
{
    /// <summary>Tier 1: the message is the source text of the condition.</summary>
    public static void Verify(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
        {
            throw new VerifyException("Verify failed: " + expression);
        }
    }
}

public sealed class VerifyFailure
{
    private readonly string expression;
    private readonly string file;
    private readonly int line;
    private readonly List<(string Label, string Value)> rows = [];

    private VerifyFailure(string expression, string file, int line)
    {
        this.expression = expression;
        this.file = file;
        this.line = line;
    }

    /// <summary>Tier 2: a message built at compile time.</summary>
    public static VerifyException Static(string expression, string message, string file, int line) =>
        new($"Verify failed: {expression}\n  {message}\n  at {file}:{line}");

    /// <summary>Tier 3: starts a message that lists captured values.</summary>
    public static VerifyFailure Start(string expression, string file, int line) => new(expression, file, line);

    public VerifyFailure Value<T>(string label, T value, bool evaluated = true)
    {
        rows.Add((label, evaluated ? Format(value) : "<not evaluated>"));
        return this;
    }

    public VerifyException Build()
    {
        var text = new StringBuilder("Verify failed: ").Append(expression).Append('\n');
        foreach (var (label, value) in rows)
        {
            text.Append("  ").Append(label).Append(" = ").Append(value).Append('\n');
        }

        return new VerifyException(text.Append("  at ").Append(file).Append(':').Append(line).ToString());
    }

    private static string Format<T>(T value) => value switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
