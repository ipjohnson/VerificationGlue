using Billing;
using NUnit.Framework;
using VerificationGlue.Mocks;

namespace MocksInNUnit;

public class InvoiceTests
{
    #region mocks-in-nunit
    [Test]
    public void Quote_adds_the_prices()
    {
        var prices = Mock.Of<IPriceList>();
        prices.PriceOf("apple").Returns(3);
        prices.PriceOf("pear").Returns(4);

        var total = new Quote(prices).Total("apple", "pear");

        Assert.That(total, Is.EqualTo(7));
        prices.Received(2).PriceOf(Arg.Any<string>());
    }
    #endregion

    #region mocks-parameter-in-nunit
    [Test]
    public void Bill_with_mock_parameters([Mock] IPriceList prices, [Mock] IReceipts receipts)
    {
        prices.Setup().PriceOf(Arg<string>.Any()).Returns(5);

        var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

        Assert.That(total, Is.EqualTo(10));
        Assert.That(receipts.Calls().Send("ana", 10) == 1);
    }
    #endregion
}
