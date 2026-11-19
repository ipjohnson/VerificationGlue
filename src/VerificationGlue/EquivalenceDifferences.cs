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
    private readonly EquivalenceDifferences? _outer;
    private HashSet<Pair>? _seen;

    /// <summary>Creates an empty list of differences.</summary>
    public EquivalenceDifferences()
    {
    }

    private EquivalenceDifferences(EquivalenceDifferences outer) => _outer = outer;

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
    /// and when this pair was compared before, which stops a cycle.
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

        var pair = new Pair(actual, expected);
        for (var outer = _outer; outer is not null; outer = outer._outer)
        {
            if (outer._seen?.Contains(pair) == true)
            {
                return true;
            }
        }

        return !(_seen ??= new HashSet<Pair>()).Add(pair);
    }

    /// <summary>Whether two items of an unordered array or list are equal by <see cref="EqualityComparer{T}.Default"/>.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">An item of the actual value.</param>
    /// <param name="expected">An item of the expected value.</param>
    public bool Same<T>(T actual, T expected) => EqualityComparer<T>.Default.Equals(actual, expected);

    /// <summary>Whether <paramref name="compare"/> finds no difference between two items of an unordered array or list.</summary>
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

    /// <summary>Whether two nullable items of an unordered array or list are both null, or <paramref name="compare"/> finds no difference between their values.</summary>
    /// <typeparam name="T">The value type of the items.</typeparam>
    /// <param name="actual">An item of the actual value.</param>
    /// <param name="expected">An item of the expected value.</param>
    /// <param name="compare">The generated comparer of <typeparamref name="T"/>.</param>
    public bool SameNullable<T>(T? actual, T? expected, Action<T, T, string, EquivalenceDifferences> compare)
        where T : struct =>
        actual is { } actualValue && expected is { } expectedValue ? Same(actualValue, expectedValue, compare) : actual.HasValue == expected.HasValue;

    /// <summary>Records an item of the expected unordered array or list that no item of the actual one matches.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="path">The path of the array or list.</param>
    /// <param name="item">The expected item.</param>
    public void Missing<T>(string path, T item) => Add(path, "missing " + VerifyFailureBuilder.Format(item));

    /// <summary>Records an item of the actual unordered array or list that no item of the expected one matches.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="path">The path of the array or list.</param>
    /// <param name="item">The actual item.</param>
    public void Extra<T>(string path, T item) => Add(path, "extra " + VerifyFailureBuilder.Format(item));

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
