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
    private HashSet<Pair>? _seen;

    /// <summary>The number of differences found.</summary>
    public int Count { get; private set; }

    /// <summary>The path of <paramref name="member"/> inside the value at <paramref name="path"/>. An empty path is the value passed to <c>VerifyEquivalent</c>.</summary>
    /// <param name="path">The path of the value that holds the member.</param>
    /// <param name="member">The name of the member, or an empty string for the value itself.</param>
    public static string Path(string path, string member) =>
        path.Length == 0 ? member : member.Length == 0 ? path : path + "." + member;

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

        return !(_seen ??= new HashSet<Pair>()).Add(new Pair(actual, expected));
    }

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
        if (Count++ < Limit)
        {
            var line = "expected " + VerifyFailureBuilder.Format(expected) + ", found " + VerifyFailureBuilder.Format(actual);
            _listed.Add(path.Length == 0 ? line : path + ": " + line);
        }
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
