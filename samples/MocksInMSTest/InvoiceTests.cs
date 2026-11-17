using Billing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerificationGlue.Mocks;

namespace MocksInMSTest;

[TestClass]
public class InvoiceTests
{
    #region mocks-in-mstest
    [TestMethod]
    public void Quote_adds_the_prices()
    {
        var prices = Mock.Of<IPriceList>();
        prices.PriceOf("apple").Returns(3);
        prices.PriceOf("pear").Returns(4);

        var total = new Quote(prices).Total("apple", "pear");

        Assert.AreEqual(7, total);
        prices.Received(2).PriceOf(Arg.Any<string>());
    }
    #endregion

    #region mocks-parameter-in-mstest
    [TestMethod, MockData]
    public void Bill_with_mock_parameters([Mock] IPriceList prices, [Mock] IReceipts receipts)
    {
        prices.Setup().PriceOf(Arg<string>.Any()).Returns(5);

        var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

        Assert.AreEqual(10, total);
        Assert.IsTrue(receipts.Calls().Send("ana", 10) == 1);
    }
    #endregion
}
