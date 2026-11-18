using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shop;
using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace VerifyInMSTest;

[TestClass]
public class OrderTests
{
    #region verify-in-mstest
    [TestMethod]
    public async Task Checkout_reports_an_empty_cart()
    {
        var order = new Order(Id: 7, Total: 40);
        Verify(order.Total == 40);
        Verify(order.Id > 0, $"order {order.Id} has no id");

        var error = Throws<InvalidOperationException>(() => new Cart().Checkout());
        Verify(error.Message == "The cart is empty.");

        var exact = ThrowsExactly<InvalidOperationException>(() => new Cart().Checkout());
        Verify(exact.Message == "The cart is empty.");

        var timeout = await ThrowsAsync<TimeoutException>(() => new Client().GetAsync("/orders/7"));
        Verify(timeout.Message.Contains("/orders/7"));
    }
    #endregion

    #region failure-in-mstest
    [TestMethod]
    public void A_failure_names_the_shape_file_and_line()
    {
        var order = new Order(Id: 7, Total: 40);

        var failure = Throws<VerifyException>(() => Verify(order.Total == 30));

        Verify(failure.Message == """
            Verify failed: order.Total == 30
              expected order.Total to equal 30
              at OrderTests.cs:36
            """);
    }
    #endregion
}
