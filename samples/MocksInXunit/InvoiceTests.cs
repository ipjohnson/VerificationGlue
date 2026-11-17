using Billing;
using VerificationGlue.Mocks;
using Xunit;

namespace MocksInXunit;

public class InvoiceTests
{
    #region mocks-in-xunit
    [Fact]
    public void Quote_adds_the_prices()
    {
        var prices = Mock.Of<IPriceList>();
        prices.PriceOf("apple").Returns(3);
        prices.PriceOf("pear").Returns(4);

        var total = new Quote(prices).Total("apple", "pear");

        Assert.Equal(7, total);
        prices.Received(2).PriceOf(Arg.Any<string>());
    }
    #endregion

    #region mocks-parameter-in-xunit
    [Theory, MockData]
    public void Bill_with_mock_parameters([Mock] IPriceList prices, [Mock] IReceipts receipts)
    {
        prices.Setup().PriceOf(Arg<string>.Any()).Returns(5);

        var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

        Assert.Equal(10, total);
        Assert.True(receipts.Calls().Send("ana", 10) == 1);
    }
    #endregion
}
