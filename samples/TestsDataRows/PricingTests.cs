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
}

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
