using Checkout;
using Xunit;

namespace MigrateFromXunit;

public class PricingTests
{
    #region migrate-from-xunit-fact-before
    [Fact]
    public void Total_multiplies_quantity_and_price()
    {
        Assert.Equal(20, Pricing.Total(2, 10));
    }
    #endregion

    #region migrate-from-xunit-inline-data-before
    [Theory]
    [InlineData(2, 10, 20)]
    [InlineData(3, 5, 15)]
    public void Totals(int qty, int price, int expected)
    {
        Assert.Equal(expected, Pricing.Total(qty, price));
    }
    #endregion

    #region migrate-from-xunit-member-data-before
    public static TheoryData<string, int> Codes => new()
    {
        { "SAVE10", 90 },
        { "HALF", 50 },
    };

    [Theory]
    [MemberData(nameof(Codes))]
    public void Discounts(string code, int expected)
    {
        Assert.Equal(expected, Pricing.Discounted(100, code));
    }
    #endregion

    #region migrate-from-xunit-trait-before
    [Fact]
    [Trait("Speed", "Fast")]
    public void Unknown_code_keeps_the_total()
    {
        Assert.Equal(100, Pricing.Discounted(100, "NONE"));
    }
    #endregion
}

#region migrate-from-xunit-context-before
public class ReceiptTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Prints_the_total()
    {
        var receipt = await new Receipts().PrintAsync(20, TestContext.Current.CancellationToken);

        output.WriteLine(receipt);
        Assert.Equal("Total: 20", receipt);
    }
}
#endregion

#region migrate-from-xunit-collection-before
[Collection("stock")]
public class TakeTests
{
    [Fact]
    public async Task Taking_two_leaves_eight()
    {
        Stock.Reset(10);
        Assert.Equal(8, await Stock.TakeAsync(2));
    }
}

[Collection("stock")]
public class RestockTests
{
    [Fact]
    public async Task Taking_five_leaves_five()
    {
        Stock.Reset(10);
        Assert.Equal(5, await Stock.TakeAsync(5));
    }
}
#endregion
