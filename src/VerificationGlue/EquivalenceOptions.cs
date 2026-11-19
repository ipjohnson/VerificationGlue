using System;
using System.Collections.Generic;

namespace VerificationGlue;

/// <summary>
/// The options of a <c>VerifyEquivalent</c> or <c>CheckEquivalent</c> call. The generator reads them
/// from the source of the call and writes them into the comparer, so these methods never run.
/// </summary>
/// <remarks>
/// Each lambda names a member by a chain of member accesses on its parameter, such as
/// <c>x =&gt; x.Customer.Address</c>. Any other lambda is build error VG0010.
/// </remarks>
/// <typeparam name="T">The type of the two values that the call compares.</typeparam>
public sealed class EquivalenceOptions<T>
{
    private EquivalenceOptions()
    {
    }

    /// <summary>
    /// Compares the items of the array or list that <paramref name="member"/> names without order.
    /// The message lists each expected item that is missing and each actual item that is extra.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="member">The member, such as <c>x =&gt; x.Lines</c>, or <c>x =&gt; x</c> for the value itself.</param>
    public EquivalenceOptions<T> Unordered<TItem>(Func<T, IEnumerable<TItem>?> member) => this;

    /// <summary>Skips the member that <paramref name="member"/> names.</summary>
    /// <typeparam name="TMember">The type of the member.</typeparam>
    /// <param name="member">The member, such as <c>x =&gt; x.Customer.Id</c>.</param>
    public EquivalenceOptions<T> Ignore<TMember>(Func<T, TMember> member) => this;
}
