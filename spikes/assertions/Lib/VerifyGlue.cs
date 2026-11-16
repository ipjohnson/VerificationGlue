using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace VerifyGlue;

[AttributeUsage(AttributeTargets.Method)]
public sealed class VerifyAttribute : Attribute { }

public sealed class VerifyException(string message) : Exception(message);

public static class VerifySyntax
{
    // Baseline with no generator involved: only the source text is known.
    public static void Verify(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (!condition)
            throw new VerifyException($"Verify failed: {expression}\n  at {file}:{line} (baseline, CallerArgumentExpression only)");
    }
}

public struct VerifyFailure
{
    private string _expression;
    private string _file;
    private int _line;
    private List<(string Label, string Value)> _rows;

    public static VerifyFailure Create(string expression, string file, int line) =>
        new() { _expression = expression, _file = file, _line = line, _rows = new() };

    public VerifyFailure With<T>(string expression, T value, bool evaluated = true)
    {
        _rows.Add((expression, evaluated ? VerifyFormat.Format(value) : "<not evaluated>"));
        return this;
    }

    public VerifyFailure Member(string expression, string valueText, string pattern, bool matched)
    {
        _rows.Add((expression, matched ? valueText : $"{valueText}   (does not match {pattern})"));
        return this;
    }

    public VerifyException Build()
    {
        var sb = new StringBuilder().Append("Verify failed: ").Append(_expression).Append('\n');
        var width = 0;
        foreach (var r in _rows) width = Math.Max(width, r.Label.Length);
        foreach (var r in _rows)
            sb.Append("  ").Append(r.Label.PadRight(width)).Append(" → ").Append(r.Value).Append('\n');
        sb.Append("  at ").Append(_file).Append(':').Append(_line);
        return new VerifyException(sb.ToString());
    }
}

public static class VerifyFormat
{
    // Type tests only. No reflection, so this is trim and AOT safe.
    public static string Format<T>(T value) => value switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        char c => "'" + c + "'",
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable e => FormatSequence(e),
        _ => value.ToString() ?? "",
    };

    private static string FormatSequence(IEnumerable e)
    {
        var sb = new StringBuilder("[");
        var i = 0;
        foreach (var item in e)
        {
            if (i > 0) sb.Append(", ");
            if (i++ == 10) { sb.Append("…"); break; }
            sb.Append(Format(item));
        }
        return sb.Append(']').ToString();
    }
}
