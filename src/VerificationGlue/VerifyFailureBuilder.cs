using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace VerificationGlue;

/// <summary>
/// Builds a tier 3 failure message from the values a rewritten copy captured while it evaluated
/// the condition. Only the failure path creates it, so a passing check allocates nothing.
/// </summary>
/// <remarks>
/// Each value is formatted from the copy's captured local. Nothing in the condition runs again.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class VerifyFailureBuilder
{
    /// <summary>Shown for a part on the skipped side of <c>&amp;&amp;</c>, <c>||</c>, <c>?:</c> or <c>??</c>.</summary>
    public const string NotEvaluated = "<not evaluated>";

    /// <summary>Shown for a part whose value the copy could not capture without changing behaviour.</summary>
    public const string Unavailable = "<unavailable>";

    private const int MaxStringLength = 200;

    private readonly string _kind;
    private readonly string _expression;
    private readonly string _file;
    private readonly int _line;
    private readonly List<(string Label, string Value)> _rows = new();
    private readonly List<string> _explanations = new();
    private readonly List<string> _notes = new();

    internal VerifyFailureBuilder(string kind, string expression, string file, int line)
    {
        _kind = kind;
        _expression = expression;
        _file = file;
        _line = line;
    }

    /// <summary>Adds a captured value.</summary>
    /// <typeparam name="T">The static type of the part.</typeparam>
    /// <param name="label">The source text of the part.</param>
    /// <param name="value">The value the part had when the condition ran.</param>
    /// <param name="evaluated">False when the part was skipped, so <paramref name="value"/> is not its value.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder Value<T>(string label, T value, bool evaluated = true)
    {
        AddRow(label, evaluated ? Format(value) : NotEvaluated);
        return this;
    }

    /// <summary>Adds a part whose value was not captured.</summary>
    /// <param name="label">The source text of the part.</param>
    /// <param name="evaluated">False when the part was skipped.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder NotCaptured(string label, bool evaluated = true)
    {
        AddRow(label, evaluated ? Unavailable : NotEvaluated);
        return this;
    }

    /// <summary>
    /// Adds the line for a condition of the form <c>Math.Abs(left - right) &lt;= tolerance</c>: how far
    /// apart the values were and what the tolerance was, or which value was NaN.
    /// </summary>
    /// <typeparam name="T">The type <c>Math.Abs</c> took: <c>double</c>, <c>float</c> or <c>decimal</c>.</typeparam>
    /// <typeparam name="TTolerance">The static type of the tolerance.</typeparam>
    /// <param name="left">The source text of the side the line names first.</param>
    /// <param name="leftValue">The value of that side.</param>
    /// <param name="right">The source text of the other side.</param>
    /// <param name="rightValue">The value of the other side.</param>
    /// <param name="difference">The value <c>Math.Abs</c> returned.</param>
    /// <param name="tolerance">The value of the tolerance.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder Tolerance<T, TTolerance>(string left, T leftValue, string right, T rightValue, T difference, TTolerance tolerance)
    {
        var within = Format(tolerance);
        _explanations.Add((IsNaN(leftValue), IsNaN(rightValue)) switch
        {
            (true, true) => $"{left} and {right} are NaN, so they are not within tolerance {within}",
            (true, false) => $"{left} is NaN, so it is not within tolerance {within} of {right}",
            (false, true) => $"{right} is NaN, so it is not within tolerance {within} of {left}",
            _ => $"{left} differs from {right} by {Format(difference)}, tolerance {within}",
        });
        return this;
    }

    /// <summary>Adds the interpolated message of the call.</summary>
    /// <param name="message">The formatted message.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder Message(string message)
    {
        _notes.Add(message);
        return this;
    }

    /// <summary>Adds the reason from <c>.Because(...)</c>.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder Because(string reason)
    {
        _notes.Add("because " + reason);
        return this;
    }

    /// <summary>Adds the text from <c>.WithMessage(...)</c>.</summary>
    /// <param name="message">The text.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder WithMessage(string message) => Message(message);

    /// <summary>The exception for a failed <c>Verify</c>, which the copy throws.</summary>
    /// <returns>The exception.</returns>
    public VerifyException Build() => new(ToString());

    /// <summary>
    /// Records a failed <c>Check</c> in the current <see cref="VerifyScope"/>, or throws it when no
    /// scope is open.
    /// </summary>
    public void Report() => VerifyScope.Report(Build());

    /// <summary>The failure message.</summary>
    /// <returns>The message.</returns>
    public override string ToString()
    {
        var text = new StringBuilder(_kind).Append(" failed: ").Append(_expression);
        foreach (var explanation in _explanations)
        {
            text.Append("\n  ").Append(explanation);
        }

        var width = 0;
        foreach (var row in _rows)
        {
            width = Math.Max(width, row.Label.Length);
        }

        foreach (var (label, value) in _rows)
        {
            text.Append("\n  ").Append(label).Append(' ', width - label.Length).Append(" → ").Append(value);
        }

        foreach (var note in _notes)
        {
            if (note.Length > 0)
            {
                text.Append("\n  ").Append(note);
            }
        }

        return text.Append("\n  at ").Append(_file).Append(':').Append(_line.ToString(CultureInfo.InvariantCulture)).ToString();
    }

    /// <summary>
    /// Adds a row unless an earlier row has the same text and value. A later read of the same text
    /// with a different value keeps its row, and so does a skipped one whose text was not shown.
    /// </summary>
    private void AddRow(string label, string value)
    {
        foreach (var row in _rows)
        {
            if (row.Label == label && (row.Value == value || value == NotEvaluated))
            {
                return;
            }
        }

        _rows.Add((label, value));
    }

    internal static string Format<T>(T value)
    {
        try
        {
            return value switch
            {
                null => "null",
                string text => Quote(text),
                char character => "'" + Escape(character.ToString()) + "'",
                bool flag => flag ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? value.GetType().Name,
            };
        }
        catch (Exception exception)
        {
            return $"<ToString threw {exception.GetType().Name}>";
        }
    }

    private static bool IsNaN<T>(T value) => value switch
    {
        double number => double.IsNaN(number),
        float number => float.IsNaN(number),
        _ => false,
    };

    private static string Quote(string text) =>
        text.Length > MaxStringLength
            ? "\"" + Escape(text.Substring(0, MaxStringLength)) + "\"… (" + text.Length.ToString(CultureInfo.InvariantCulture) + " characters)"
            : "\"" + Escape(text) + "\"";

    private static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            escaped.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\0' => "\\0",
                _ when char.IsControl(character) => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString(),
            });
        }

        return escaped.ToString();
    }
}
