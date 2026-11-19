using Shop;
using VerificationGlue;
using Xunit;
using static VerificationGlue.VerifySyntax;

namespace VerifyInXunit;

public class OrderTests
{
    #region verify-in-xunit
    [Fact]
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

    #region failure-in-xunit
    [Fact]
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

    #region lambda-in-xunit
    [Fact]
    public void A_lambda_lists_the_value_of_each_part()
    {
        var order = new Order(Id: 7, Total: 40);

        var failure = Throws<VerifyException>(() => Verify(() => order.Total == 30));

        Verify(() => failure.Message == """
            Verify failed: order.Total == 30
              order.Total → 40
              order       → Order { Id = 7, Total = 40 }
              at OrderTests.cs:51
            """);
    }
    #endregion

    #region keep-verify-bool-in-xunit
    [Fact]
    public async Task Some_checks_keep_Verify_bool()
    {
        var orders = new Order?[] { new Order(Id: 7, Total: 40) };

        // A Func<bool> cannot contain await.
        Verify(await Task.FromResult(orders.Length) == 1);

        // A lambda cannot capture a Span<T>.
        ReadOnlySpan<Order?> span = orders;
        Verify(span.Length == 1);

        // Only Verify(bool) tells the compiler that first is not null on the next line.
        var first = orders[0];
        Verify(first is not null);
        Verify(() => first.Total == 40);
    }
    #endregion
}
