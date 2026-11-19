using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary>Tier 3: <c>==</c> on two strings shows where they first differ.</summary>
public static partial class StringMessages
{
    public static void Lines() => VerifySvg(Svg("80"), Svg("82"));

    public static void SingleLine() => VerifyTitle(
        "Order 7 for Ada Lovelace, shipped standard on 2026-11-18",
        "Order 7 for Ada Lovelace, shipped express on 2026-11-18");

    #region message-tier3-string-lines
    [Verify]
    public static void VerifySvg(string written, string expected)
    {
        Verify(written == expected);
    }
    #endregion

    #region message-tier3-string-index
    [Verify]
    public static void VerifyTitle(string title, string expected)
    {
        Verify(title == expected);
    }
    #endregion

    private static string Svg(string width) => $"""
        <svg>
          <g class="title">
            <text>Order 7</text>
          </g>
          <g class="node">
            <rect width="{width}">
          </g>
        </svg>
        """;
}
