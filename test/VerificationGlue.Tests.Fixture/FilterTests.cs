using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

/// <summary>
/// Tests with traits for the --vg-filter specs. Each logs "filter ran {test} in attempt {n}", and with
/// <c>VG_FIXTURE_FILTER_FAIL_FIRST</c> set, <see cref="CheckoutTests.Checkout_charges_the_card"/> fails its first attempt.
/// </summary>
[Trait("Category", "Checkout")]
public partial class CheckoutTests
{
    private static readonly bool s_failFirst = Environment.GetEnvironmentVariable("VG_FIXTURE_FILTER_FAIL_FIRST") is not null;

    [VerifyTest]
    public void Checkout_adds_tax() => Ran(nameof(Checkout_adds_tax));

    [VerifyTest]
    [Trait("Speed", "Slow")]
    public void Checkout_charges_the_card() => Verify(!(Ran(nameof(Checkout_charges_the_card)) == 1 && s_failFirst));

    internal static int Ran(string test)
    {
        var line = $"filter ran {test} in attempt ";
        var attempt = Log.Lines.Count(logged => logged.StartsWith(line, StringComparison.Ordinal)) + 1;
        Log.Write(line + attempt);
        return attempt;
    }
}

[Trait("Category", "Refunds")]
public partial class RefundFilterTests
{
    [VerifyTest]
    [Trait("Speed", "Slow")]
    public void Refund_reverses_the_charge() => CheckoutTests.Ran(nameof(Refund_reverses_the_charge));
}
