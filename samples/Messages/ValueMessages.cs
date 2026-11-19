using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary>
/// Tier 2: <c>Single</c>, <c>Is</c> and <c>NotNull</c>, which return the value they checked.
/// </summary>
public static class ValueMessages
{
    public static void SingleItem()
    {
        var order = new Order(7, 30, "standard");
        #region message-tier2-single
        var line = Single(order.Lines);
        #endregion
    }

    public static void SingleMatch()
    {
        Order[] orders = [new(7, 30, "standard"), new(8, 45, "express")];
        #region message-tier2-single-match
        var order = Single(orders, o => o.Total > 50);
        #endregion
    }

    public static void IsType()
    {
        object item = new Cart();
        #region message-tier2-is
        var order = Is<Order>(item);
        #endregion
    }

    public static void NotNullValue()
    {
        Order? order = null;
        #region message-tier2-not-null-value
        var found = NotNull(order);
        #endregion
    }
}
