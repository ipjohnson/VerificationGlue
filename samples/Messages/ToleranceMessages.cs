using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace Messages;

public sealed record Box(double X, double Y);

/// <summary>Tier 3: a tolerance check, <c>Math.Abs(a - b) &lt;= t</c>, says how far apart the values were.</summary>
public static partial class ToleranceMessages
{
    public static void Tolerance() => VerifyLeft(new Box(120.5625, 40));

    #region message-tier3-tolerance
    [Verify]
    public static void VerifyLeft(Box box)
    {
        Verify(Math.Abs(box.X - 120.5) <= 0.01);
    }
    #endregion
}
