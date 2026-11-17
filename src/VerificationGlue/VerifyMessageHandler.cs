using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace VerificationGlue;

/// <summary>
/// Builds the message of <c>Verify(condition, $"...")</c>. The holes run only when the condition is
/// false. Values are formatted with the invariant culture.
/// </summary>
[InterpolatedStringHandler]
public ref struct VerifyMessageHandler
{
    private readonly bool _enabled;
    private DefaultInterpolatedStringHandler _builder;

    /// <summary>Starts the message. The compiler calls this constructor.</summary>
    /// <param name="literalLength">The number of literal characters in the message.</param>
    /// <param name="formattedCount">The number of holes in the message.</param>
    /// <param name="condition">The condition of the <c>Verify</c> call.</param>
    /// <param name="shouldAppend">Set to false when the condition holds, so no hole runs.</param>
    public VerifyMessageHandler(int literalLength, int formattedCount, bool condition, out bool shouldAppend)
    {
        _enabled = !condition;
        shouldAppend = _enabled;
        _builder = _enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount, CultureInfo.InvariantCulture) : default;
    }

    /// <summary>Appends literal text.</summary>
    /// <param name="value">The text.</param>
    public void AppendLiteral(string value) => _builder.AppendLiteral(value);

    /// <summary>Appends a value.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    public void AppendFormatted<T>(T value) => _builder.AppendFormatted(value);

    /// <summary>Appends a value with a format string.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, string? format) => _builder.AppendFormatted(value, format);

    /// <summary>Appends a value with an alignment.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="alignment">The minimum width. A negative width aligns to the left.</param>
    public void AppendFormatted<T>(T value, int alignment) => _builder.AppendFormatted(value, alignment);

    /// <summary>Appends a value with an alignment and a format string.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="alignment">The minimum width. A negative width aligns to the left.</param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, int alignment, string? format) => _builder.AppendFormatted(value, alignment, format);

    /// <summary>Appends characters.</summary>
    /// <param name="value">The characters.</param>
    public void AppendFormatted(scoped ReadOnlySpan<char> value) => _builder.AppendFormatted(value);

    /// <summary>Appends a string.</summary>
    /// <param name="value">The string.</param>
    public void AppendFormatted(string? value) => _builder.AppendFormatted(value);

    internal string ToStringAndClear() => _enabled ? _builder.ToStringAndClear() : "";
}
