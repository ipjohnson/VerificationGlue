using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace TestsGettingStarted;

public interface IPrices
{
    int PriceOf(string sku);
}

public sealed class Cart(IPrices prices)
{
    private readonly List<(string Sku, int Quantity)> _lines = [];

    public int Total => _lines.Sum(line => line.Quantity * prices.PriceOf(line.Sku));

    public void Add(string sku, int quantity) => _lines.Add((sku, quantity));

    public async Task CheckoutAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        _lines.Clear();
    }
}

#region first-verify-tests
public partial class CartTests
{
    [VerifyTest]
    public void Total_adds_the_line_prices()
    {
        var prices = Mock.Of<IPrices>();
        prices.PriceOf("apple").Returns(3);
        prices.PriceOf("pear").Returns(4);
        var cart = new Cart(prices);

        cart.Add("apple", 2);
        cart.Add("pear", 1);

        Verify(cart.Total == 10);
    }

    [VerifyTest]
    public async Task Checkout_empties_the_cart(CancellationToken cancellationToken)
    {
        var cart = new Cart(Mock.Of<IPrices>());
        cart.Add("apple", 1);

        await cart.CheckoutAsync(cancellationToken);

        Verify(cart.Total == 0);
    }
}
#endregion
