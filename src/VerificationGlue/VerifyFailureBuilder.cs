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

    /// <summary>The items a list of items shows before it ends with <c>…</c>.</summary>
    private const int MaxListedItems = 10;

    /// <summary>The length of a row past which a collection's items go one per line.</summary>
    private const int RowWidth = 60;

    /// <summary>How deep collections inside collections show their items. Deeper ones show <c>[…]</c>.</summary>
    private const int MaxDepth = 3;

    private const string NotRead = "not read again, because reading it could run its code";

    private readonly string _kind;
    private readonly string _expression;
    private readonly string _file;
    private readonly int _line;
    private readonly List<(string Label, string Value, string? Text, Listing? Items)> _rows = new();

    /// <summary>
    /// The lines before the rows. <c>Listed</c> names the sequence whose items a line lists, so the
    /// line is left out when that sequence's row lists them.
    /// </summary>
    private readonly List<(string? Listed, string Text)> _explanations = new();
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
        if (!evaluated)
        {
            AddRow(label, NotEvaluated, null, null);
            return this;
        }

        Listing? items;
        try
        {
            items = value is null or string ? null : List(value, 0);
        }
        catch (Exception)
        {
            items = null;
        }

        AddRow(label, items is null ? Format(value) : items.Inline(), value as string, items);
        return this;
    }

    /// <summary>Adds a part whose value was not captured.</summary>
    /// <param name="label">The source text of the part.</param>
    /// <param name="evaluated">False when the part was skipped.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder NotCaptured(string label, bool evaluated = true)
    {
        AddRow(label, evaluated ? Unavailable : NotEvaluated, null, null);
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
        Explain((IsNaN(leftValue), IsNaN(rightValue)) switch
        {
            (true, true) => $"{left} and {right} are NaN, so they are not within tolerance {within}",
            (true, false) => $"{left} is NaN, so it is not within tolerance {within} of {right}",
            (false, true) => $"{right} is NaN, so it is not within tolerance {within} of {left}",
            _ => $"{left} differs from {right} by {Format(difference)}, tolerance {within}",
        });
        return this;
    }

    /// <summary>
    /// Adds the lines for a failed <c>left.SequenceEqual(right)</c>: the first index at which the
    /// items differ with the item each side has there, both lengths, and the items the longer side
    /// has past the end of the shorter. Adds nothing when either side is not a collection, because
    /// reading a lazy sequence again would run its code again.
    /// </summary>
    /// <typeparam name="T">The item type <c>SequenceEqual</c> compared.</typeparam>
    /// <param name="left">The source text of the first sequence.</param>
    /// <param name="leftValue">The value of the first sequence.</param>
    /// <param name="right">The source text of the second sequence.</param>
    /// <param name="rightValue">The value of the second sequence.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder SequenceDifference<T>(string left, IEnumerable<T>? leftValue, string right, IEnumerable<T>? rightValue)
    {
        if (Materialize(leftValue) is not { } leftItems || Materialize(rightValue) is not { } rightItems)
        {
            return this;
        }

        var comparer = EqualityComparer<T>.Default;
        var index = 0;
        while (index < leftItems.Count && index < rightItems.Count && comparer.Equals(leftItems[index], rightItems[index]))
        {
            index++;
        }

        if (index == leftItems.Count && index == rightItems.Count)
        {
            return this;
        }

        string At(string name, List<T> items) => index < items.Count ? name + " has " + Format(items[index]) : name + " has no item";
        Explain("first difference at index " + index.ToString(CultureInfo.InvariantCulture) + ": " + At(left, leftItems) + ", " + At(right, rightItems));
        Explain(left + " has " + Items(leftItems.Count) + ", " + right + " has " + rightItems.Count.ToString(CultureInfo.InvariantCulture));
        if (leftItems.Count != rightItems.Count)
        {
            var (longer, longerItems, shorter, end) = leftItems.Count > rightItems.Count
                ? (left, leftItems, right, rightItems.Count)
                : (right, rightItems, left, leftItems.Count);
            Explain(longer + " has " + Items(longerItems.Count - end) + " past the end of " + shorter + ": " + ItemList(longerItems.GetRange(end, longerItems.Count - end)));
        }

        return this;
    }

    /// <summary>
    /// Adds the lines for a failed <c>set.SetEquals(other)</c>: the items of <paramref name="other"/>
    /// that the set lacks, and the items of the set that <paramref name="other"/> lacks. Items match
    /// by the comparer of a <see cref="HashSet{T}"/>, and by the default comparer for any other set.
    /// Adds nothing when <paramref name="otherValue"/> is not a collection, because reading a lazy
    /// sequence again would run its code again.
    /// </summary>
    /// <typeparam name="T">The item type of the set.</typeparam>
    /// <param name="set">The source text of the set.</param>
    /// <param name="setValue">The value of the set.</param>
    /// <param name="other">The source text of the sequence the set was compared with.</param>
    /// <param name="otherValue">The value of that sequence.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder SetDifference<T>(string set, IEnumerable<T>? setValue, string other, IEnumerable<T>? otherValue)
    {
        if (Materialize(setValue) is not { } setItems || Materialize(otherValue) is not { } otherItems)
        {
            return this;
        }

        var comparer = setValue is HashSet<T> hashSet ? hashSet.Comparer : EqualityComparer<T>.Default;
        var inSet = new HashSet<T>(setItems, comparer);
        var inOther = new HashSet<T>(otherItems, comparer);
        var listed = new HashSet<T>(comparer);
        var missing = new List<T>();
        foreach (var item in otherItems)
        {
            if (!inSet.Contains(item) && listed.Add(item))
            {
                missing.Add(item);
            }
        }

        var extra = new List<T>();
        foreach (var item in setItems)
        {
            if (!inOther.Contains(item) && listed.Add(item))
            {
                extra.Add(item);
            }
        }

        if (missing.Count > 0)
        {
            Explain(set + " is missing " + Items(missing.Count) + " that " + other + " has: " + ItemList(missing));
        }

        if (extra.Count > 0)
        {
            Explain(set + " has " + Items(extra.Count) + " that " + other + " does not: " + ItemList(extra));
        }

        return this;
    }

    /// <summary>
    /// Adds the line that lists the items of a sequence, for a failed count, <c>Any</c> or
    /// <c>Contains</c>. Adds nothing when the sequence is not a collection, because reading a lazy
    /// sequence again would run its code again.
    /// </summary>
    /// <typeparam name="T">The item type of the sequence.</typeparam>
    /// <param name="label">The source text of the sequence.</param>
    /// <param name="value">The value of the sequence.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder ListItems<T>(string label, IEnumerable<T>? value)
    {
        if (Materialize(value) is { } items)
        {
            Explain(items.Count == 0 ? label + " is empty" : label + " has " + Items(items.Count) + ": " + ItemList(items), label);
        }

        return this;
    }

    /// <summary>
    /// Adds the line for a failed <c>All</c> that names the index and value of the first item that
    /// fails. It runs <paramref name="predicate"/> again on the items up to that one. Adds nothing
    /// when the sequence is not a collection, when every item passes, or when the predicate throws.
    /// </summary>
    /// <typeparam name="T">The item type of the sequence.</typeparam>
    /// <param name="value">The value of the sequence.</param>
    /// <param name="predicate">The predicate <c>All</c> was given.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder FirstFailure<T>(IEnumerable<T>? value, Func<T, bool> predicate)
    {
        if (Materialize(value) is not { } items)
        {
            return this;
        }

        for (var index = 0; index < items.Count; index++)
        {
            bool passed;
            try
            {
                passed = predicate(items[index]);
            }
            catch (Exception)
            {
                return this;
            }

            if (!passed)
            {
                Explain("first item that fails at index " + index.ToString(CultureInfo.InvariantCulture) + ": " + Format(items[index]));
                return this;
            }
        }

        return this;
    }

    /// <summary>Adds the message of the call. A null or empty message adds nothing.</summary>
    /// <param name="message">The message.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder Message(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _notes.Add(message!);
        }

        return this;
    }

    /// <summary>
    /// Adds the interpolated message of the call. A hole whose formatting throws shows a placeholder
    /// that names the exception's type.
    /// </summary>
    /// <param name="message">The interpolated message.</param>
    /// <returns>This builder.</returns>
    public VerifyFailureBuilder Message(ref VerifyMessageHandler message) => Message(message.ToStringAndClear());

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
        foreach (var (listed, explanation) in _explanations)
        {
            if (listed is null || !_rows.Exists(row => row.Label == listed && row.Items is not null))
            {
                text.Append("\n  ").Append(explanation);
            }
        }

        var width = 0;
        foreach (var row in _rows)
        {
            width = Math.Max(width, row.Label.Length);
        }

        foreach (var (label, value, raw, items) in _rows)
        {
            text.Append("\n  ").Append(label).Append(' ', width - label.Length).Append(" → ");
            if (items is not null && width + 5 + value.Length > RowWidth)
            {
                var indent = new string(' ', width + 7);
                text.Append(Items(items.Count));
                foreach (var item in items.Shown)
                {
                    text.Append('\n').Append(indent).Append(item);
                }

                if (items.Count > items.Shown.Count)
                {
                    text.Append('\n').Append(indent).Append('…');
                }

                continue;
            }

            text.Append(_difference is not null && raw is { Length: > DifferenceStringLength } ? Quote(raw, DifferenceStringLength) : value);
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
    private void AddRow(string label, string value, string? text, Listing? items)
    {
        foreach (var row in _rows)
        {
            if (row.Label == label && (row.Value == value || value == NotEvaluated))
            {
                return;
            }
        }

        _rows.Add((label, value, text, items));
    }

    private void Explain(string text, string? listed = null) => _explanations.Add((listed, text));

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

    internal static string Format<T>(T value) => Format(value, 0);

    private static string Format<T>(T value, int depth)
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
                System.Collections.IEnumerable => List(value, depth) is { } items ? items.Bracketed() : Unread(value),
                _ => value.ToString() ?? value.GetType().Name,
            };
        }
        catch (Exception exception)
        {
            return FormatThrew(exception);
        }
    }

    /// <summary>The placeholder for a value whose formatting threw <paramref name="exception"/>.</summary>
    internal static string FormatThrew(Exception exception) => $"<ToString threw {exception.GetType().Name}>";

    /// <summary>
    /// The count and the first items of a collection, or null when the value is not a collection.
    /// A collection holds its items, so reading it runs no code of the condition. Covariance lets
    /// <see cref="IReadOnlyCollection{T}"/> of <see cref="object"/> match a collection of any
    /// reference type without reflection, so the check is the same under Native AOT. A
    /// <see cref="HashSet{T}"/> of a value type has neither interface, so it is matched by its type.
    /// </summary>
    private static Listing? List(object value, int depth)
    {
        int count;
        switch (value)
        {
            case System.Collections.ICollection collection:
                count = collection.Count;
                break;
            case IReadOnlyCollection<object> collection:
                count = collection.Count;
                break;
            case System.Collections.IEnumerable set when value.GetType() is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(HashSet<>):
                count = 0;
                foreach (var _ in set)
                {
                    count++;
                }

                break;
            default:
                return null;
        }

        var shown = new List<string>();
        if (depth < MaxDepth)
        {
            if (value is System.Collections.IDictionary dictionary)
            {
                var entries = dictionary.GetEnumerator();
                while (shown.Count < MaxListedItems && entries.MoveNext())
                {
                    shown.Add(Format(entries.Key, depth + 1) + ": " + Format(entries.Value, depth + 1));
                }

                (entries as IDisposable)?.Dispose();
            }
            else
            {
                foreach (var item in (System.Collections.IEnumerable)value)
                {
                    if (shown.Count == MaxListedItems)
                    {
                        break;
                    }

                    shown.Add(Format(item, depth + 1));
                }
            }
        }

        return new Listing(count, shown);
    }

    /// <summary>
    /// A sequence that is not a collection, such as the result of <c>Select</c>. It is shown by its
    /// <c>ToString</c> when the type has one, and otherwise by its type name with a note.
    /// </summary>
    private static string Unread(object value)
    {
        var text = value.ToString();
        var type = value.GetType();
        return text is null || text == type.ToString() ? VerifyFailure.TypeName(type) + " (" + NotRead + ")" : text;
    }

    /// <summary>
    /// A copy of the items when the sequence is a collection, and null otherwise. A collection holds
    /// its items, so reading it again runs no code of the condition.
    /// </summary>
    private static List<T>? Materialize<T>(IEnumerable<T>? items) =>
        items is ICollection<T> or IReadOnlyCollection<T> or System.Collections.ICollection ? new List<T>(items) : null;

    private static string Items(int count) =>
        count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " item" : " items");

    /// <summary>The items, formatted and joined, with <c>…</c> after the first <see cref="MaxListedItems"/>.</summary>
    private static string ItemList<T>(List<T> items)
    {
        var text = new StringBuilder();
        for (var index = 0; index < items.Count && index < MaxListedItems; index++)
        {
            text.Append(index > 0 ? ", " : "").Append(Format(items[index]));
        }

        return items.Count > MaxListedItems ? text.Append(", …").ToString() : text.ToString();
    }

    /// <summary>
    /// The count of a collection and its first <see cref="MaxListedItems"/> items, formatted. A
    /// collection deeper than <see cref="MaxDepth"/> has no items shown.
    /// </summary>
    private sealed class Listing
    {
        public Listing(int count, List<string> shown)
        {
            Count = count;
            Shown = shown;
        }

        public int Count { get; }

        public List<string> Shown { get; }

        /// <summary>The form for a row: <c>2 items: "a", "b"</c>.</summary>
        public string Inline() => Count == 0 ? Items(0) : Items(Count) + ": " + Joined();

        /// <summary>The form for an item or a line: <c>["a", "b"]</c>.</summary>
        public string Bracketed() => "[" + Joined() + "]";

        private string Joined()
        {
            if (Shown.Count == 0)
            {
                return Count == 0 ? "" : "…";
            }

            return string.Join(", ", Shown) + (Count > Shown.Count ? ", …" : "");
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
