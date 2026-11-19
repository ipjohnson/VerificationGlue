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

    /// <summary>The length a string value is cut to when the message shows where two strings differ.</summary>
    private const int DifferenceStringLength = 40;

    /// <summary>The characters of a line the difference shows, and how many come before the first that differs.</summary>
    private const int DifferenceWidth = 80;
    private const int DifferenceLead = 30;

    /// <summary>The lines shown before and after the line that differs.</summary>
    private const int DifferenceContext = 2;

    private readonly string _kind;
    private readonly string _expression;
    private readonly string _file;
    private readonly int _line;
    private readonly List<(string Label, string Value, string? Text)> _rows = new();
    private readonly List<string> _explanations = new();
    private readonly List<string> _notes = new();
    private List<string>? _difference;

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
        AddRow(label, evaluated ? Format(value) : NotEvaluated, evaluated && value is string text ? text : null);
        return this;
    }

    /// <summary>Adds a part whose value was not captured.</summary>
    /// <param name="label">The source text of the part.</param>
    /// <param name="evaluated">False when the part was skipped.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder NotCaptured(string label, bool evaluated = true)
    {
        AddRow(label, evaluated ? Unavailable : NotEvaluated, null);
        return this;
    }

    /// <summary>
    /// Adds the lines for a failed <c>left == right</c> on two strings that show where they first
    /// differ. When either string has more than one line, they give the line and column and the lines
    /// around it. Otherwise, when either string is long, they give the index and the text around it.
    /// Lines from <paramref name="left"/> start with <c>-</c> and lines from <paramref name="right"/>
    /// with <c>+</c>. Once the lines are shown, the message cuts long string values short.
    /// </summary>
    /// <param name="left">The value of the left operand.</param>
    /// <param name="right">The value of the right operand.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder StringDifference(string? left, string? right)
    {
        if (left is null || right is null || string.Equals(left, right, StringComparison.Ordinal))
        {
            return this;
        }

        if (left.IndexOf('\n') >= 0 || right.IndexOf('\n') >= 0)
        {
            _difference = LineDifference(left, right);
        }
        else if (Math.Max(left.Length, right.Length) > DifferenceStringLength)
        {
            var index = FirstDifference(left, right);
            var start = WindowStart(index, Math.Max(left.Length, right.Length));
            _difference = new List<string>
            {
                "first difference at index " + index.ToString(CultureInfo.InvariantCulture),
                "- " + Window(left, start, quoted: true),
                "+ " + Window(right, start, quoted: true),
            };
        }

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

        foreach (var (label, value, raw) in _rows)
        {
            text.Append("\n  ").Append(label).Append(' ', width - label.Length).Append(" → ")
                .Append(_difference is not null && raw is { Length: > DifferenceStringLength } ? Quote(raw, DifferenceStringLength) : value);
        }

        foreach (var line in _difference ?? new List<string>())
        {
            text.Append("\n  ").Append(line);
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
    private void AddRow(string label, string value, string? text)
    {
        foreach (var row in _rows)
        {
            if (row.Label == label && (row.Value == value || value == NotEvaluated))
            {
                return;
            }
        }

        _rows.Add((label, value, text));
    }

    /// <summary>
    /// The lines for two strings that differ and of which at least one has more than one line. Lines
    /// split at <c>\n</c>, so a <c>\r</c> before it stays in the line and shows as <c>\r</c>.
    /// </summary>
    private static List<string> LineDifference(string left, string right)
    {
        var leftLines = left.Split('\n');
        var rightLines = right.Split('\n');
        var line = 0;
        while (line < leftLines.Length && line < rightLines.Length && leftLines[line] == rightLines[line])
        {
            line++;
        }

        var column = line < leftLines.Length && line < rightLines.Length ? FirstDifference(leftLines[line], rightLines[line]) : 0;
        var first = Math.Max(0, line - DifferenceContext);
        var last = Math.Min(Math.Max(leftLines.Length, rightLines.Length) - 1, line + DifferenceContext);
        var longest = 0;
        for (var index = first; index <= last; index++)
        {
            longest = Math.Max(longest, Math.Max(index < leftLines.Length ? leftLines[index].Length : 0, index < rightLines.Length ? rightLines[index].Length : 0));
        }

        var start = WindowStart(column, longest);
        var width = (last + 1).ToString(CultureInfo.InvariantCulture).Length;
        string Row(char marker, int index, string? text)
        {
            var number = (index + 1).ToString(CultureInfo.InvariantCulture).PadLeft(width);
            var shown = text is null ? "<end of text>" : Window(text, start, quoted: false);
            return marker + " " + number + (shown.Length > 0 ? "   " + shown : "");
        }

        var lines = new List<string>
        {
            "first difference at line " + (line + 1).ToString(CultureInfo.InvariantCulture) + ", column " + (column + 1).ToString(CultureInfo.InvariantCulture),
        };
        for (var index = first; index <= last; index++)
        {
            var inLeft = index < leftLines.Length;
            var inRight = index < rightLines.Length;
            if (index < line || (index > line && inLeft && inRight && leftLines[index] == rightLines[index]))
            {
                lines.Add(Row(' ', index, leftLines[index]));
                continue;
            }

            if (inLeft || index == line)
            {
                lines.Add(Row('-', index, inLeft ? leftLines[index] : null));
            }

            if (inRight || index == line)
            {
                lines.Add(Row('+', index, inRight ? rightLines[index] : null));
            }
        }

        return lines;
    }

    /// <summary>The first index at which the strings differ, or the length of the shorter one.</summary>
    private static int FirstDifference(string left, string right)
    {
        var index = 0;
        while (index < left.Length && index < right.Length && left[index] == right[index])
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Where the shown part of the text starts: at the beginning when the longest text fits, and
    /// otherwise <see cref="DifferenceLead"/> characters before <paramref name="position"/>.
    /// </summary>
    private static int WindowStart(int position, int longest) =>
        longest <= DifferenceWidth ? 0 : Math.Max(0, Math.Min(position - DifferenceLead, longest - DifferenceWidth));

    /// <summary>The text from <paramref name="start"/>, or the whole text when it fits.</summary>
    private static string Window(string text, int start, bool quoted)
    {
        start = text.Length <= DifferenceWidth ? 0 : start;
        var length = Math.Max(0, Math.Min(DifferenceWidth, text.Length - start));
        var segment = length > 0 ? text.Substring(start, length) : "";
        return (start > 0 && text.Length > 0 ? "…" : "") +
               (quoted ? "\"" + Escape(segment) + "\"" : Escape(segment, quoted: false)) +
               (start + length < text.Length ? "…" : "");
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

    private static string Quote(string text, int maxLength = MaxStringLength) =>
        text.Length > maxLength
            ? "\"" + Escape(text.Substring(0, maxLength)) + "\"… (" + text.Length.ToString(CultureInfo.InvariantCulture) + " characters)"
            : "\"" + Escape(text) + "\"";

    /// <summary>Escapes control characters, and also <c>\</c> and <c>"</c> when the text is shown in quotes.</summary>
    private static string Escape(string text, bool quoted = true)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            escaped.Append(character switch
            {
                '\\' when quoted => "\\\\",
                '"' when quoted => "\\\"",
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
