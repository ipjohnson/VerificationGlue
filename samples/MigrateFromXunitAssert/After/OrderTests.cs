using Store;
using VerificationGlue;
using Xunit;
using static VerificationGlue.VerifySyntax;

namespace MigrateFromXunitAssert.After;

public class OrderTests
{
    private readonly Order _order = new(7, 40, [10, 30], "express") { IsPaid = true };

    [Fact]
    public void Equality()
    {
        #region xunit-assert-equal-after
        Verify(_order.Total == 40);
        Verify(_order.Id != 0);
        #endregion
    }

    [Fact]
    public void Booleans()
    {
        #region xunit-assert-bool-after
        Verify(_order.IsPaid);
        Verify(_order.Lines.Count != 0);
        #endregion
    }

    [Fact]
    public void Nulls()
    {
        #region xunit-assert-null-after
        Verify(_order.Coupon is null);
        Verify(_order.Shipping is not null);
        #endregion
    }

    [Fact]
    public void Collections()
    {
        var cart = new Cart();

        #region xunit-assert-collections-after
        Verify(_order.Lines.Contains(10));
        Verify(!_order.Lines.Contains(99));
        Verify(cart.Items.Count == 0);
        Verify(_order.Lines.Count == 2);
        #endregion
    }

    [Fact]
    public void Strings()
    {
        #region xunit-assert-strings-after
        Verify(_order.Shipping.StartsWith("exp"));
        Verify(_order.Shipping.Contains("press"));
        #endregion
    }

    [Fact]
    public void Types()
    {
        Order order = new PaidOrder(8, 10, [10], "standard");

        #region xunit-assert-types-after
        Verify(order is Order);
        Verify(order.GetType() == typeof(PaidOrder));
        #endregion
    }

    [Fact]
    public void Ranges()
    {
        #region xunit-assert-range-after
        Verify(_order.Total is >= 1 and <= 100);
        Verify(_order.Total > 10);
        #endregion
    }

    [Fact]
    public void Floating_point()
    {
        #region xunit-assert-precision-after
        Verify(Math.Abs(_order.Tax - 4.0) < 0.000001);
        #endregion
    }

    [Fact]
    public void Messages()
    {
        #region xunit-assert-message-after
        Verify(_order.Total > 10, $"order {_order.Id} is too small");
        #endregion
    }

    [Fact]
    public void Exceptions()
    {
        var cart = new Cart();

        #region xunit-assert-throws-after
        var error = Throws<InvalidOperationException>(() => cart.Checkout());
        Verify(error.Message == "The cart is empty.");
        #endregion
    }

    [Fact]
    public async Task Async_exceptions()
    {
        var client = new Client();

        #region xunit-assert-throws-async-after
        var timeout = await ThrowsAsync<TimeoutException>(() => client.GetAsync("/orders/7"));
        Verify(timeout.Message.Contains("/orders/7"));
        #endregion
    }

    [Fact]
    public void Several_checks()
    {
        #region xunit-assert-multiple-after
        using (VerifyScope.Begin())
        {
            Check(_order.Total == 40);
            Check(_order.Shipping == "express");
            Check(_order.Lines.Count == 2);
        }
        #endregion
    }
}
