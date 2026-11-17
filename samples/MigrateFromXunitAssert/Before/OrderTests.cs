using Store;
using Xunit;

namespace MigrateFromXunitAssert.Before;

public class OrderTests
{
    private readonly Order _order = new(7, 40, [10, 30], "express") { IsPaid = true };

    [Fact]
    public void Equality()
    {
        #region xunit-assert-equal-before
        Assert.Equal(40, _order.Total);
        Assert.NotEqual(0, _order.Id);
        #endregion
    }

    [Fact]
    public void Booleans()
    {
        #region xunit-assert-bool-before
        Assert.True(_order.IsPaid);
        Assert.False(_order.Lines.Count == 0);
        #endregion
    }

    [Fact]
    public void Nulls()
    {
        #region xunit-assert-null-before
        Assert.Null(_order.Coupon);
        Assert.NotNull(_order.Shipping);
        #endregion
    }

    [Fact]
    public void Collections()
    {
        var cart = new Cart();

        #region xunit-assert-collections-before
        Assert.Contains(10, _order.Lines);
        Assert.DoesNotContain(99, _order.Lines);
        Assert.Empty(cart.Items);
        Assert.Equal(2, _order.Lines.Count);
        #endregion
    }

    [Fact]
    public void Strings()
    {
        #region xunit-assert-strings-before
        Assert.StartsWith("exp", _order.Shipping);
        Assert.Contains("press", _order.Shipping);
        #endregion
    }

    [Fact]
    public void Types()
    {
        Order order = new PaidOrder(8, 10, [10], "standard");

        #region xunit-assert-types-before
        Assert.IsAssignableFrom<Order>(order);
        Assert.IsType<PaidOrder>(order);
        #endregion
    }

    [Fact]
    public void Ranges()
    {
        #region xunit-assert-range-before
        Assert.InRange(_order.Total, 1, 100);
        Assert.True(_order.Total > 10);
        #endregion
    }

    [Fact]
    public void Floating_point()
    {
        #region xunit-assert-precision-before
        Assert.Equal(4.0, _order.Tax, 6);
        #endregion
    }

    [Fact]
    public void Messages()
    {
        #region xunit-assert-message-before
        Assert.True(_order.Total > 10, $"order {_order.Id} is too small");
        #endregion
    }

    [Fact]
    public void Exceptions()
    {
        var cart = new Cart();

        #region xunit-assert-throws-before
        var error = Assert.Throws<InvalidOperationException>(() => cart.Checkout());
        Assert.Equal("The cart is empty.", error.Message);
        #endregion
    }

    [Fact]
    public async Task Async_exceptions()
    {
        var client = new Client();

        #region xunit-assert-throws-async-before
        var timeout = await Assert.ThrowsAsync<TimeoutException>(() => client.GetAsync("/orders/7"));
        Assert.Contains("/orders/7", timeout.Message);
        #endregion
    }

    [Fact]
    public void Several_checks()
    {
        #region xunit-assert-multiple-before
        Assert.Multiple(
            () => Assert.Equal(40, _order.Total),
            () => Assert.Equal("express", _order.Shipping),
            () => Assert.Equal(2, _order.Lines.Count));
        #endregion
    }
}
