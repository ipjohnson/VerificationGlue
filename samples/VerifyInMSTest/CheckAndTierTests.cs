using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shop;
using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace VerifyInMSTest;

[TestClass]
public partial class CheckAndTierTests
{
    [TestMethod]
    public async Task Failed_checks_are_reported_when_the_scope_ends()
    {
        var order = new Order(Id: 7, Total: 40);

        var failure = await ThrowsAsync<VerifyException>(async () =>
        {
            using var scope = VerifyScope.Begin();
            Check(order.Total == 30);
            await Task.Yield();
            Check(order.Id == 8);
        });

        Verify(() => failure.Message == """
            2 checks failed

            Check failed: order.Total == 30
              expected order.Total to equal 30
              at CheckAndTierTests.cs:19

            Check failed: order.Id == 8
              expected order.Id to equal 8
              at CheckAndTierTests.cs:21
            """);
    }

    // MSTest calls the test through reflection, so the original runs and the Check gets tier 2.
    [TestMethod]
    [Verify]
    public void A_test_MSTest_calls_runs_the_original()
    {
        var order = new Order(Id: 7, Total: 40);
        var scope = VerifyScope.Begin();

        Check(order.Total == 30);

        var failure = Throws<VerifyException>(scope.Dispose);
        Verify(() => failure.Message == """
            1 check failed

            Check failed: order.Total == 30
              expected order.Total to equal 30
              at CheckAndTierTests.cs:45
            """);
    }

    [TestMethod]
    public void A_direct_call_to_a_Verify_helper_runs_the_copy()
    {
        var order = new Order(Id: 7, Total: 40);

        var failure = Throws<VerifyException>(() => VerifyTotal(order, 30));

        Verify(() => failure.Message == """
            Verify failed: order.Total == expected
              order.Total → 40
              order       → Order { Id = 7, Total = 40 }
              expected    → 30
              at CheckAndTierTests.cs:76
            """);
    }

    [Verify]
    private static void VerifyTotal(Order order, int expected)
    {
        Verify(order.Total == expected);
    }
}
