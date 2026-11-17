using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary><c>Check</c> records each failure, and the scope reports them all when it ends.</summary>
public static partial class CheckMessages
{
    public static void Tier2()
    {
        #region check-scope
        var order = new Order(7, 30, "standard");

        using (VerifyScope.Begin())
        {
            Check(order.Total == 40);
            Check(order.Shipping == "express");
            Check(order.Lines.Count == 2);
        }
        #endregion
    }

    public static void Tier3()
    {
        using (VerifyScope.Begin())
        {
            CheckOrder(new Order(7, 30, "standard"));
        }
    }

    #region check-tier3
    [Verify]
    public static void CheckOrder(Order order)
    {
        Check(order.Total == 40);
        Check(order.Shipping.Length > 8);
    }
    #endregion
}
