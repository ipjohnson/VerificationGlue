using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace TestsCi;

#region tests-ci-traits
[Trait("Category", "Checkout")]
public partial class CheckoutTests
{
    [VerifyTest]
    public void Adds_tax() => Verify(Prices.WithTax(100m) == 120m);

    [VerifyTest]
    [Trait("Category", "Manual")]
    public void Prints_a_receipt() => Verify(ReceiptPrinter.Print("order 1"));
}
#endregion

#region tests-ci-rows
public partial class CurrencyTests
{
    [VerifyTest]
    [Row("EUR")]
    [Row("GBP")]
    public async Task Converts(string code) => Verify(await RateFeed.RateAsync(code) > 0m);
}
#endregion

public static class Prices
{
    public static decimal WithTax(decimal net) => net * 1.2m;
}

/// <summary>Stands in for a printer on the desk. It logs each receipt, so the specs can tell whether the test ran.</summary>
public static class ReceiptPrinter
{
    public static bool Print(string text)
    {
        RateFeed.Log("printed " + text);
        return true;
    }
}

/// <summary>
/// Stands in for a rate service that drops a request now and then. When <c>TESTSCI_LOG</c> names a file,
/// each call is logged there, and the first call for GBP fails, so the specs can watch a retry.
/// </summary>
public static class RateFeed
{
    private static readonly string? s_log = Environment.GetEnvironmentVariable("TESTSCI_LOG");
    private static readonly Lock s_lock = new();

    public static async Task<decimal> RateAsync(string code)
    {
        await Task.Yield();
        bool dropped;
        lock (s_lock)
        {
            dropped = s_log is not null && code == "GBP" && !(File.Exists(s_log) && File.ReadAllText(s_log).Contains("rate GBP", StringComparison.Ordinal));
            Log("rate " + code);
        }

        return dropped ? 0m : 1.1m;
    }

    public static void Log(string line)
    {
        if (s_log is not null)
        {
            lock (s_lock)
            {
                File.AppendAllText(s_log, line + "\n");
            }
        }
    }
}
