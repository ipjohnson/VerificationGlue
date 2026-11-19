using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary>Tier 3: <c>SequenceEqual</c> and <c>SetEquals</c> say which items differ.</summary>
public static partial class CollectionMessages
{
    public static void Sequence() => VerifyLines([10, 20, 25], [10, 20, 30, 40]);

    public static void Set() => VerifyTags(["gift", "fragile", "express"], ["gift", "priority"]);

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
}
