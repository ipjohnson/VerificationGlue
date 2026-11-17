#region nsubstitute-usings-after
using VerificationGlue.Mocks;
#endregion
using Billing;
using Xunit;

namespace MigrateFromNSubstitute.After;

public class InvoiceTests
{
    [Fact]
    public void Bill_sends_the_total()
    {
        #region nsubstitute-create-after
        var prices = Mock.Of<IPriceList>();
        var receipts = Mock.Of<IReceipts>();
        #endregion

        #region nsubstitute-returns-after
        prices.PriceOf("apple").Returns(3);
        prices.PriceOf(Arg.Is<string>(sku => sku.StartsWith('p'))).Returns(4);
        #endregion

        var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

        Assert.Equal(7, total);
        #region nsubstitute-received-after
        receipts.Received(1).Send("ana", 7);
        receipts.DidNotReceive().Send("bob", Arg.Any<int>());
        #endregion
    }

    [Fact]
    public async Task BillAsync_awaits_each_price()
    {
        var prices = Mock.Of<IPriceList>();
        var receipts = Mock.Of<IReceipts>();

        #region nsubstitute-async-after
        prices.PriceOfAsync("apple").Returns(3);
        prices.PriceOfAsync("pear").Returns(Task.FromResult(4));
        #endregion

        Assert.Equal(7, await new Invoice(prices, receipts).BillAsync("ana", "apple", "pear"));
    }

    [Fact]
    public void Bill_passes_on_a_missing_price()
    {
        var prices = Mock.Of<IPriceList>();
        var receipts = Mock.Of<IReceipts>();

        #region nsubstitute-throws-after
        prices.PriceOf("plum").Throws(new KeyNotFoundException());
        #endregion

        Assert.Throws<KeyNotFoundException>(() => new Invoice(prices, receipts).Bill("ana", "plum"));
        receipts.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public void Bill_sends_one_receipt()
    {
        var prices = Mock.Of<IPriceList>();
        var receipts = Mock.Of<IReceipts>();
        prices.PriceOf(Arg.Any<string>()).Returns(2);

        #region nsubstitute-when-do-after
        var sent = new List<int>();
        receipts.When(r => r.Send("ana", Arg.Any<int>())).Do(call => sent.Add(call.ArgAt<int>(1)));
        #endregion

        new Invoice(prices, receipts).Bill("ana", "apple", "pear");

        Assert.Equal([4], sent);
    }

    [Fact]
    public void Bill_with_the_typed_surface()
    {
        #region nsubstitute-typed-after
        var prices = Mock.Of<IPriceList>();
        var receipts = Mock.Of<IReceipts>();
        prices.Setup().PriceOf(Arg<string>.Any()).Returns(2);

        new Invoice(prices, receipts).Bill("ana", "apple", "pear");

        Assert.True(receipts.Calls().Send("ana", 4) == 1);
        #endregion
    }
}
