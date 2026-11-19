using Shop;
using TUnit.Core;
using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace VerifyInTUnit;

public class OrderTests
{
    #region verify-in-tunit
    [Test]
    public async Task Checkout_reports_an_empty_cart()
    {
        var order = new Order(Id: 7, Total: 40);
        Verify(() => order.Total == 40);
        Verify(order.Id > 0, $"order {order.Id} has no id");

        var error = Throws<InvalidOperationException>(() => new Cart().Checkout());
        Verify(() => error.Message == "The cart is empty.");

        var exact = ThrowsExactly<InvalidOperationException>(() => new Cart().Checkout());
        Verify(() => exact.Message == "The cart is empty.");

        var timeout = await ThrowsAsync<TimeoutException>(() => new Client().GetAsync("/orders/7"));
        Verify(() => timeout.Message.Contains("/orders/7"));
    }
    #endregion

    #region failure-in-tunit
    [Test]
    public void A_failure_names_the_shape_file_and_line()
    {
        var order = new Order(Id: 7, Total: 40);

        var failure = Throws<VerifyException>(() => Verify(order.Total == 30));

        Verify(() => failure.Message == """
            Verify failed: order.Total == 30
              expected order.Total to equal 30
              at OrderTests.cs:35
            """);
    }
    #endregion

    #region lambda-in-tunit
    [Test]
    public void A_lambda_lists_the_value_of_each_part()
    {
        var order = new Order(Id: 7, Total: 40);

        var failure = Throws<VerifyException>(() => Verify(() => order.Total == 30 && order.Id > 0));

        Verify(() => failure.Message == """
            Verify failed: order.Total == 30 && order.Id > 0
              order.Total == 30 → false
              order.Total       → 40
              order             → Order { Id = 7, Total = 40 }
              order.Id > 0      → <not evaluated>
              order.Id          → <not evaluated>
              at OrderTests.cs:51
            """);
    }
    #endregion

    [Test]
    public void A_lambda_lists_the_items_of_a_collection()
    {
        var problems = new List<string> { "flowchart/a: box differs", "pie/b: missing label" };

        var failure = Throws<VerifyException>(() => Verify(() => problems.Count == 0));

        Verify(() => failure.Message == """
            Verify failed: problems.Count == 0
              problems.Count → 2
              problems       → 2 items
                                 "flowchart/a: box differs"
                                 "pie/b: missing label"
              at OrderTests.cs:70
            """);
    }
}
