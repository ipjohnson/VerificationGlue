#region nsubstitute-usings-before
using NSubstitute;
using NSubstitute.ExceptionExtensions;
#endregion
using Billing;
using Xunit;

namespace MigrateFromNSubstitute.Before;

public class InvoiceTests
{
    [Fact]
    public void Bill_sends_the_total()
    {
        #region nsubstitute-create-before
        var prices = Substitute.For<IPriceList>();
        var receipts = Substitute.For<IReceipts>();
        #endregion

        #region nsubstitute-returns-before
        prices.PriceOf("apple").Returns(3);
        prices.PriceOf(Arg.Is<string>(sku => sku.StartsWith('p'))).Returns(4);
        #endregion

        var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

        Assert.Equal(7, total);
        #region nsubstitute-received-before
        receipts.Received(1).Send("ana", 7);
        receipts.DidNotReceive().Send("bob", Arg.Any<int>());
        #endregion
    }

    [Fact]
    public async Task BillAsync_awaits_each_price()
    {
        var prices = Substitute.For<IPriceList>();
        var receipts = Substitute.For<IReceipts>();

        #region nsubstitute-async-before
        prices.PriceOfAsync("apple").Returns(3);
        prices.PriceOfAsync("pear").Returns(Task.FromResult(4));
        #endregion

        Assert.Equal(7, await new Invoice(prices, receipts).BillAsync("ana", "apple", "pear"));
    }

    [Fact]
    public void Bill_passes_on_a_missing_price()
    {
        var prices = Substitute.For<IPriceList>();
        var receipts = Substitute.For<IReceipts>();

        #region nsubstitute-throws-before
        prices.PriceOf("plum").Throws(new KeyNotFoundException());
        #endregion

        Assert.Throws<KeyNotFoundException>(() => new Invoice(prices, receipts).Bill("ana", "plum"));
        receipts.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public void Bill_sends_one_receipt()
    {
        var prices = Substitute.For<IPriceList>();
        var receipts = Substitute.For<IReceipts>();
        prices.PriceOf(Arg.Any<string>()).Returns(2);

        #region nsubstitute-when-do-before
        var sent = new List<int>();
        receipts.When(r => r.Send("ana", Arg.Any<int>())).Do(call => sent.Add(call.ArgAt<int>(1)));
        #endregion

        new Invoice(prices, receipts).Bill("ana", "apple", "pear");

        Assert.Equal([4], sent);
    }
}
