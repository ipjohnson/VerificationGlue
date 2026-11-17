using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary>
/// Tier 3: <c>[Verify]</c> methods in a <c>partial</c> class, in a project that sets
/// <c>VerificationGlueRewrite</c>. A direct call runs the method's <c>__Verified</c> copy, whose
/// message lists the captured values.
/// </summary>
public static partial class Tier3Messages
{
    public static void Values() => VerifyShipping(new Order(7, 30, "standard"));

    public static void Skipped() => VerifyDiscount(new Order(7, 30, "standard"), code: null);

    public static void Because() => VerifyTotal(new Order(7, 30, "standard"));

    public static void Pattern() => VerifyShape(new Order(7, 30, "standard"));

    #region message-tier3-values
    [Verify]
    public static void VerifyShipping(Order order)
    {
        Verify(order.Shipping.StartsWith("express") && order.Lines.Count < 3);
    }
    #endregion

    #region message-tier3-skipped
    [Verify]
    public static void VerifyDiscount(Order order, string? code)
    {
        Verify(code != null && code.Length == 6 || order.Total > 100);
    }
    #endregion

    #region message-tier3-because
    [Verify]
    public static void VerifyTotal(Order order)
    {
        Verify(order.Total - Discount(order) == 25).Because("orders over 20 get 10 off");
    }
    #endregion

    #region message-tier3-pattern
    [Verify]
    public static void VerifyShape(Order order)
    {
        Verify(order is { Total: > 50, Shipping: "express" });
    }
    #endregion

    private static int Discount(Order order) => order.Total > 20 ? 0 : 10;
}
