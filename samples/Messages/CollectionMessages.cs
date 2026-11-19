using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary>
/// Tier 3: <c>SequenceEqual</c> and <c>SetEquals</c> say which items differ, a count, <c>Any</c>
/// and <c>Contains</c> list the items, and <c>All</c> names the first item that fails.
/// </summary>
public static partial class CollectionMessages
{
    public static void Sequence() => VerifyLines([10, 20, 25], [10, 20, 30, 40]);

    public static void Set() => VerifyTags(["gift", "fragile", "express"], ["gift", "priority"]);

    public static void Counted() => VerifyLineCount([10, 20]);

    public static void Every() => VerifyQuantities([2, 5, 0, 3]);

    public static void Some() => VerifyBackorder([2, 5, 3]);

    public static void Contained() => VerifyExpress(["gift", "fragile"]);

    #region message-tier3-sequence-equal
    [Verify]
    public static void VerifyLines(int[] lines, int[] expected)
    {
        Verify(lines.SequenceEqual(expected));
    }
    #endregion

    #region message-tier3-set-equals
    [Verify]
    public static void VerifyTags(HashSet<string> tags, string[] expected)
    {
        Verify(tags.SetEquals(expected));
    }
    #endregion

    #region message-tier3-count
    [Verify]
    public static void VerifyLineCount(List<int> lines)
    {
        Verify(lines.Count == 3);
    }
    #endregion

    #region message-tier3-all
    [Verify]
    public static void VerifyQuantities(int[] quantities)
    {
        Verify(quantities.All(quantity => quantity > 0));
    }
    #endregion

    #region message-tier3-any
    [Verify]
    public static void VerifyBackorder(int[] quantities)
    {
        Verify(quantities.Any(quantity => quantity > 10));
    }
    #endregion

    #region message-tier3-contains
    [Verify]
    public static void VerifyExpress(string[] tags)
    {
        Verify(tags.Contains("express"));
    }
    #endregion
}
