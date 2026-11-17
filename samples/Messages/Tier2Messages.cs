using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary>
/// Tier 2: methods without <c>[Verify]</c>. Each call gets an interceptor with a message for the
/// shape of its condition.
/// </summary>
public static class Tier2Messages
{
    public static void Equal()
    {
        var order = new Order(7, 30, "standard");
        #region message-tier2-equal
        Verify(order.Total == 40);
        #endregion
    }

    public static void NotNull()
    {
        Order? order = null;
        #region message-tier2-not-null
        Verify(order is not null);
        #endregion
    }

    public static void Relational()
    {
        var order = new Order(7, 30, "standard");
        #region message-tier2-relational
        Verify(order.Total >= 50);
        #endregion
    }

    public static void BoolCall()
    {
        var order = new Order(7, 30, "standard");
        #region message-tier2-bool-call
        Verify(order.Shipping.StartsWith("express"));
        #endregion
    }

    public static void Pattern()
    {
        object order = new Order(7, 30, "standard");
        #region message-tier2-pattern
        Verify(order is Order { Total: > 50 });
        #endregion
    }

    public static void WithMessage()
    {
        var order = new Order(7, 30, "standard");
        #region message-tier2-with-message
        Verify(order.Total == 40, $"order {order.Id} has the wrong total");
        #endregion
    }

    public static void Throws()
    {
        #region message-tier2-throws
        Throws<InvalidOperationException>(() => new Cart().Add(1));
        #endregion
    }
}
