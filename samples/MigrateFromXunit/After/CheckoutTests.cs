using Checkout;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace MigrateFromXunit;

public partial class PricingTests
{
    #region migrate-from-xunit-fact-after
    [VerifyTest]
    public void Total_multiplies_quantity_and_price()
    {
        Verify(Pricing.Total(2, 10) == 20);
    }
    #endregion

    #region migrate-from-xunit-inline-data-after
    [VerifyTest]
    [Row(2, 10, 20)]
    [Row(3, 5, 15)]
    public void Totals(int qty, int price, int expected)
    {
        Verify(Pricing.Total(qty, price) == expected);
    }
    #endregion

    #region migrate-from-xunit-member-data-after
    public static IEnumerable<(string Code, int Expected)> Codes =>
    [
        ("SAVE10", 90),
        ("HALF", 50),
    ];

    [VerifyTest]
    [RowSource(nameof(Codes))]
    public void Discounts(string code, int expected)
    {
        Verify(Pricing.Discounted(100, code) == expected);
    }
    #endregion

    #region migrate-from-xunit-trait-after
    [VerifyTest]
    [Trait("Speed", "Fast")]
    public void Unknown_code_keeps_the_total()
    {
        Verify(Pricing.Discounted(100, "NONE") == 100);
    }
    #endregion
}

#region migrate-from-xunit-context-after
public partial class ReceiptTests
{
    [VerifyTest]
    public async Task Prints_the_total(ITestOutput output, CancellationToken cancellationToken)
    {
        var receipt = await new Receipts().PrintAsync(20, cancellationToken);

        output.WriteLine(receipt);
        Verify(receipt == "Total: 20");
    }
}
#endregion

#region migrate-from-xunit-collection-after
[Resource("stock")]
public partial class TakeTests
{
    [VerifyTest]
    public async Task Taking_two_leaves_eight()
    {
        Stock.Reset(10);
        Verify(await Stock.TakeAsync(2) == 8);
    }
}

[Resource("stock")]
public partial class RestockTests
{
    [VerifyTest]
    public async Task Taking_five_leaves_five()
    {
        Stock.Reset(10);
        Verify(await Stock.TakeAsync(5) == 5);
    }
}
#endregion
