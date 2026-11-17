using Billing;
using VerificationGlue.Mocks;

namespace MocksInTUnit;

public class InvoiceTests
{
    #region mocks-in-tunit
    [Test]
    public async Task Quote_adds_the_prices()
    {
        var prices = Mock.Of<IPriceList>();
        prices.PriceOf("apple").Returns(3);
        prices.PriceOf("pear").Returns(4);

        var total = new Quote(prices).Total("apple", "pear");

        await Assert.That(total).IsEqualTo(7);
        prices.Received(2).PriceOf(Arg.Any<string>());
    }
    #endregion

    #region mocks-parameter-in-tunit
    [Test, MockData]
    public async Task Bill_with_mock_parameters([Mock] IPriceList prices, [Mock] IReceipts receipts)
    {
        prices.Setup().PriceOf(Arg<string>.Any()).Returns(5);

        var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

        await Assert.That(total).IsEqualTo(10);
        await Assert.That(receipts.Calls().Send("ana", 10) == 1).IsTrue();
    }
    #endregion
}
