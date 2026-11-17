using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VerificationGlue.Mocks;

/// <summary>
/// The number of recorded calls that match a typed <c>Calls()</c> member, such as
/// <c>calc.Calls().Add(1, 2)</c>. It compares with an <see cref="int"/>, so a check is a plain
/// condition: <c>Verify(calc.Calls().Add(1, 2) == 1)</c>.
/// </summary>
/// <remarks>
/// <see cref="ToString"/> gives the count and up to 10 of the calls the member received, so a
/// failure message that shows the value also shows what the mock received.
/// </remarks>
public readonly struct CallCount : IEquatable<CallCount>
{
    private const int ListedCallLimit = 10;

    private readonly string? _member;
    private readonly IReadOnlyList<CallRecord>? _callsToMember;

    internal CallCount(int count, string member, IReadOnlyList<CallRecord> callsToMember)
    {
        Count = count;
        _member = member;
        _callsToMember = callsToMember;
    }

    /// <summary>The number of matching calls.</summary>
    public int Count { get; }

    /// <summary>The calls the member received when the count was taken, matching or not.</summary>
    public IReadOnlyList<CallRecord> CallsToMember => _callsToMember ?? [];

    /// <summary>True when the count equals <paramref name="right"/>.</summary>
    public static bool operator ==(CallCount left, int right) => left.Count == right;

    /// <summary>True when the count differs from <paramref name="right"/>.</summary>
    public static bool operator !=(CallCount left, int right) => left.Count != right;

    /// <summary>True when the count is less than <paramref name="right"/>.</summary>
    public static bool operator <(CallCount left, int right) => left.Count < right;

    /// <summary>True when the count is at most <paramref name="right"/>.</summary>
    public static bool operator <=(CallCount left, int right) => left.Count <= right;

    /// <summary>True when the count is greater than <paramref name="right"/>.</summary>
    public static bool operator >(CallCount left, int right) => left.Count > right;

    /// <summary>True when the count is at least <paramref name="right"/>.</summary>
    public static bool operator >=(CallCount left, int right) => left.Count >= right;

    /// <summary>True when <paramref name="left"/> equals the count.</summary>
    public static bool operator ==(int left, CallCount right) => left == right.Count;

    /// <summary>True when <paramref name="left"/> differs from the count.</summary>
    public static bool operator !=(int left, CallCount right) => left != right.Count;

    /// <summary>True when <paramref name="left"/> is less than the count.</summary>
    public static bool operator <(int left, CallCount right) => left < right.Count;

    /// <summary>True when <paramref name="left"/> is at most the count.</summary>
    public static bool operator <=(int left, CallCount right) => left <= right.Count;

    /// <summary>True when <paramref name="left"/> is greater than the count.</summary>
    public static bool operator >(int left, CallCount right) => left > right.Count;

    /// <summary>True when <paramref name="left"/> is at least the count.</summary>
    public static bool operator >=(int left, CallCount right) => left >= right.Count;

    /// <inheritdoc />
    public bool Equals(CallCount other) => Count == other.Count;

    /// <summary>True for a <see cref="CallCount"/> or an <see cref="int"/> with the same count.</summary>
    public override bool Equals(object? obj) => obj switch
    {
        CallCount other => Equals(other),
        int value => Count == value,
        _ => false,
    };

    /// <inheritdoc />
    public override int GetHashCode() => Count;

    /// <summary>The count and up to 10 of the calls the member received, such as <c>0 (Add received 1 call: Add(3, 4))</c>.</summary>
    public override string ToString()
    {
        var text = new StringBuilder(Count.ToString(CultureInfo.InvariantCulture));
        if (_member is null)
        {
            return text.ToString();
        }

        var calls = CallsToMember;
        text.Append(" (").Append(_member).Append(" received ");
        if (calls.Count == 0)
        {
            return text.Append("no calls)").ToString();
        }

        text.Append(calls.Count.ToString(CultureInfo.InvariantCulture)).Append(calls.Count == 1 ? " call: " : " calls: ");
        for (var i = 0; i < calls.Count && i < ListedCallLimit; i++)
        {
            text.Append(i == 0 ? "" : ", ").Append(calls[i]);
        }

        if (calls.Count > ListedCallLimit)
        {
            text.Append(", and ").Append((calls.Count - ListedCallLimit).ToString(CultureInfo.InvariantCulture)).Append(" more");
        }

        return text.Append(')').ToString();
    }
}
