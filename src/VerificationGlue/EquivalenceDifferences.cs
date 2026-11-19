using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace VerificationGlue;

/// <summary>
/// The differences that a <c>VerifyEquivalent</c> comparer finds. The comparers the generator writes call it.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EquivalenceDifferences
{
    /// <summary>The most differences a message lists. The message counts the rest.</summary>
    public const int Limit = 10;

    private readonly List<string> _listed = new();
    private readonly List<Frame> _frames = new();
    private readonly EquivalenceDifferences? _outer;
    private readonly int _depth;
    private HashSet<Pair>? _seen;

    /// <summary>Creates an empty list of differences.</summary>
    public EquivalenceDifferences()
    {
    }

    private EquivalenceDifferences(EquivalenceDifferences outer)
    {
        _outer = outer;
        _depth = outer._depth + outer._frames.Count;
    }

    /// <summary>The number of differences found.</summary>
    public int Count { get; private set; }

    /// <summary>The path of <paramref name="member"/> inside the value at <paramref name="path"/>. An empty path is the value passed to <c>VerifyEquivalent</c>.</summary>
    /// <param name="path">The path of the value that holds the member.</param>
    /// <param name="member">The name of the member, or an empty string for the value itself.</param>
    public static string Path(string path, string member) =>
        path.Length == 0 ? member : member.Length == 0 ? path : path + "." + member;

    /// <summary>The path of the item at <paramref name="index"/> in the array or list at <paramref name="path"/>.</summary>
    /// <param name="path">The path of the array or list.</param>
    /// <param name="index">The index of the item.</param>
    public static string Item(string path, int index) =>
        path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    /// <summary>The path of the value at <paramref name="key"/> in the dictionary at <paramref name="path"/>.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="path">The path of the dictionary.</param>
    /// <param name="key">The key of the entry.</param>
    public static string Entry<TKey>(string path, TKey key) =>
        path + "[" + VerifyFailureBuilder.Format(key) + "]";

    /// <summary>Records a difference when an array or list has a different number of items than expected.</summary>
    /// <param name="path">The path of the array or list.</param>
    /// <param name="actual">The number of items in the actual value.</param>
    /// <param name="expected">The number of items in the expected value.</param>
    public void ItemCount(string path, int actual, int expected)
    {
        if (actual != expected)
        {
            Add(path, "expected " + Items(expected) + ", found " + Items(actual));
        }
    }

    /// <summary>Records a difference when the two values are not equal by <see cref="EqualityComparer{T}.Default"/>.</summary>
    /// <typeparam name="T">The type of the member.</typeparam>
    /// <param name="path">The path of the value that holds the member.</param>
    /// <param name="member">The name of the member, or an empty string for the value itself.</param>
    /// <param name="actual">The value of the member in the actual value.</param>
    /// <param name="expected">The value of the member in the expected value.</param>
    public void Equal<T>(string path, string member, T actual, T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            Add(Path(path, member), actual, expected);
        }
    }

    /// <summary>
    /// Whether the comparison of two references at <paramref name="path"/> ends without reading their
    /// members. It ends when both are the same reference, when either is null, which is a difference,
    /// when either is a value that holds it on the path, which is a cycle, and when this pair was
    /// compared before. A cycle is a difference unless both sides refer back to the same depth of
    /// the path. When it does not end, the comparer reads the members and then calls <see cref="Leave"/>.
    /// </summary>
    /// <param name="path">The path of the two values.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    public bool Settled(string path, object? actual, object? expected)
    {
        if (ReferenceEquals(actual, expected))
        {
            return true;
        }

        if (actual is null || expected is null)
        {
            Add(path, actual, expected);
            return true;
        }

        var actualCycle = Ancestor(actual, expectedSide: false);
        var expectedCycle = Ancestor(expected, expectedSide: true);
        if (actualCycle >= 0 || expectedCycle >= 0)
        {
            if (actualCycle != expectedCycle)
            {
                Add(path, "expected " + Cycle(expectedCycle) + ", found " + Cycle(actualCycle));
            }

            return true;
        }

        if (!(_seen ??= new HashSet<Pair>()).Add(new Pair(actual, expected)))
        {
            return true;
        }

        _frames.Add(new Frame(actual, expected, path));
        return false;
    }

    /// <summary>Ends the comparison of the two references that the last call of <see cref="Settled"/> that returned false started.</summary>
    public void Leave() => _frames.RemoveAt(_frames.Count - 1);

    /// <summary>Whether two items of an unordered array or list are equal by <see cref="EqualityComparer{T}.Default"/>.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">An item of the actual value.</param>
    /// <param name="expected">An item of the expected value.</param>
    public bool Same<T>(T actual, T expected) => EqualityComparer<T>.Default.Equals(actual, expected);

    /// <summary>Whether <paramref name="compare"/> finds no difference between two items of an unordered array or list, or of a set.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">An item of the actual value.</param>
    /// <param name="expected">An item of the expected value.</param>
    /// <param name="compare">The generated comparer of the item type.</param>
    public bool Same<T>(T actual, T expected, Action<T, T, string, EquivalenceDifferences> compare)
    {
        var trial = new EquivalenceDifferences(this);
        compare(actual, expected, "", trial);
        return trial.Count == 0;
    }

    /// <summary>Whether two nullable items of an unordered array or list, or of a set, are both null, or <paramref name="compare"/> finds no difference between their values.</summary>
    /// <typeparam name="T">The value type of the items.</typeparam>
    /// <param name="actual">An item of the actual value.</param>
    /// <param name="expected">An item of the expected value.</param>
    /// <param name="compare">The generated comparer of <typeparamref name="T"/>.</param>
    public bool SameNullable<T>(T? actual, T? expected, Action<T, T, string, EquivalenceDifferences> compare)
        where T : struct =>
        actual is { } actualValue && expected is { } expectedValue ? Same(actualValue, expectedValue, compare) : actual.HasValue == expected.HasValue;

    /// <summary>Records an item of the expected unordered array or list, or set, that no item of the actual one matches.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="path">The path of the array or list.</param>
    /// <param name="item">The expected item.</param>
    public void Missing<T>(string path, T item) => Add(path, "missing " + VerifyFailureBuilder.Format(item));

    /// <summary>Records an item of the actual unordered array or list, or set, that no item of the expected one matches.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="path">The path of the array or list.</param>
    /// <param name="item">The actual item.</param>
    public void Extra<T>(string path, T item) => Add(path, "extra " + VerifyFailureBuilder.Format(item));

    /// <summary>Records an entry of the expected dictionary whose key the actual dictionary does not have.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="path">The path of the dictionary.</param>
    /// <param name="key">The key of the expected entry.</param>
    /// <param name="value">The value of the expected entry.</param>
    public void MissingEntry<TKey, TValue>(string path, TKey key, TValue value) =>
        Add(path, "missing " + VerifyFailureBuilder.Format(key) + ": " + VerifyFailureBuilder.Format(value));

    /// <summary>Records an entry of the actual dictionary whose key the expected dictionary does not have.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="path">The path of the dictionary.</param>
    /// <param name="key">The key of the actual entry.</param>
    /// <param name="value">The value of the actual entry.</param>
    public void ExtraEntry<TKey, TValue>(string path, TKey key, TValue value) =>
        Add(path, "extra " + VerifyFailureBuilder.Format(key) + ": " + VerifyFailureBuilder.Format(value));

    /// <summary>One line per listed difference, then a count of the rest, joined for a failure message.</summary>
    internal string Describe()
    {
        var text = new StringBuilder(string.Join("\n  ", _listed));
        var rest = Count - _listed.Count;
        if (rest > 0)
        {
            text.Append("\n  and ").Append(rest.ToString(CultureInfo.InvariantCulture)).Append(rest == 1 ? " more difference" : " more differences");
        }

        return text.ToString();
    }

    private void Add<T>(string path, T actual, T expected)
    {
        if (Count < Limit)
        {
            Add(path, "expected " + VerifyFailureBuilder.Format(expected) + ", found " + VerifyFailureBuilder.Format(actual));
        }
        else
        {
            Count++;
        }
    }

    private void Add(string path, string line)
    {
        if (Count++ < Limit)
        {
            _listed.Add(path.Length == 0 ? line : path + ": " + line);
        }
    }

    private static string Items(int count) =>
        count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " item" : " items");

    /// <summary>The depth on the path, counted from the outermost comparison, of the frame whose value on one side is <paramref name="value"/>, or -1.</summary>
    private int Ancestor(object value, bool expectedSide)
    {
        for (var index = _frames.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(expectedSide ? _frames[index].Expected : _frames[index].Actual, value))
            {
                return _depth + index;
            }
        }

        return _outer?.Ancestor(value, expectedSide) ?? -1;
    }

    private string Cycle(int depth)
    {
        if (depth < 0)
        {
            return "no cycle";
        }

        var owner = this;
        while (depth < owner._depth)
        {
            owner = owner._outer!;
        }

        var path = owner._frames[depth - owner._depth].Path;
        return "a cycle back to " + (path.Length == 0 ? "the root" : path);
    }

    private sealed class Frame
    {
        public Frame(object actual, object expected, string path)
        {
            Actual = actual;
            Expected = expected;
            Path = path;
        }

        public object Actual { get; }

        public object Expected { get; }

        public string Path { get; }
    }

    private readonly struct Pair : IEquatable<Pair>
    {
        private readonly object _actual;
        private readonly object _expected;

        public Pair(object actual, object expected)
        {
            _actual = actual;
            _expected = expected;
        }

        public bool Equals(Pair other) => ReferenceEquals(_actual, other._actual) && ReferenceEquals(_expected, other._expected);

        public override bool Equals(object? obj) => obj is Pair other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_actual), RuntimeHelpers.GetHashCode(_expected));
    }
}
