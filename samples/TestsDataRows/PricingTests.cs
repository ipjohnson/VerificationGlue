using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace TestsDataRows;

public static class Pricing
{
    public static int Total(int quantity, int price) => quantity * price;

    public static int Discounted(int total, string code) => code switch
    {
        "SAVE10" => total * 90 / 100,
        "HALF" => total / 2,
        _ => total,
    };
}

public partial class PricingTests
{
    #region tests-literal-rows
    [VerifyTest]
    [Row(2, 10, 20)]
    [Row(3, 5, 15)]
    public void Totals(int qty, int price, int expected) => Verify(Pricing.Total(qty, price) == expected);
    #endregion

    #region tests-row-and-other-parameters
    [VerifyTest]
    [Row("SAVE10", 90)]
    [Row("HALF", 50)]
    public void Discounts(string code, int expected, ITestOutput output)
    {
        output.WriteLine($"Applying {code} to 100");
        Verify(Pricing.Discounted(100, code) == expected);
    }
    #endregion

    #region tests-row-source
    public static IEnumerable<(int Qty, int Price, int Expected)> Orders() =>
    [
        (1, 4, 4),
        (6, 2, 12),
    ];

    [VerifyTest]
    [RowSource(nameof(Orders))]
    public void Order_totals(int qty, int price, int expected) => Verify(Pricing.Total(qty, price) == expected);
    #endregion

    #region tests-data-source-usage
    [VerifyTest]
    [Customers]
    public void Earns_points(Customer customer) => Verify(customer.Points >= 0);
    #endregion
}

public sealed record Customer(string Name, int Points);

public static class CustomerStore
{
    public static IEnumerable<Customer> All() => [new("ann", 10), new("bob", 0)];
}

#region tests-data-source
/// <summary>Gives one row per stored customer, keyed by the customer's name.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CustomersAttribute : Attribute, ITestDataSource
{
    public async IAsyncEnumerable<TestRow> GetRowsAsync(TestDataContext context)
    {
        await Task.Yield();
        foreach (var customer in CustomerStore.All())
        {
            yield return new TestRow(customer) { Key = customer.Name };
        }
    }
}
#endregion

#region tests-name-formatter
/// <summary>Writes the method name and the row values, without the parameter names.</summary>
public sealed class ValuesOnlyNameFormatter : ITestNameFormatter
{
    public static string Format(in TestNameContext context) =>
        context.Arguments.Count == 0
            ? context.MethodName
            : context.MethodName + " " + string.Join(" / ", context.Arguments.Select(DefaultNameFormatter.FormatValue));
}
#endregion

#region tests-name-formatter-usage
[NameFormatter<ValuesOnlyNameFormatter>]
public partial class ShippingTests
{
    [VerifyTest]
    [Row(1, "standard")]
    [Row(5, "express")]
    public void Ships(int days, string speed) => Verify(days > 0 && speed.Length > 0);
}
#endregion
