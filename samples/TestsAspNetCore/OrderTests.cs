using System.Net.Http.Json;
using ShopApi;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

namespace TestsAspNetCore;

#region first-app-test
[WebApp<ShopApiEntry>]
public partial class OrderTests
{
    [VerifyTest]
    public async Task Post_stamps_the_order_with_the_clock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2));
        var order = await response.Content.ReadFromJsonAsync<Order>();

        Verify(response.IsSuccessStatusCode);
        Verify(order!.Created == new DateTime(2026, 11, 17));
    }
}
#endregion

#region app-test-controller
[WebApp<ShopApiEntry>]
public partial class ClockTests
{
    [VerifyTest]
    public async Task A_controller_receives_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2030, 1, 2));

        var now = await client.GetFromJsonAsync<DateTime>("/clock");

        Verify(now == new DateTime(2030, 1, 2));
    }
}
#endregion

#region app-test-services
[WebApp<ShopApiEntry>]
public partial class StoreTests
{
    [VerifyTest]
    public async Task Post_saves_the_order_in_the_store(HttpClient client, IOrderStore store)
    {
        await client.PostAsJsonAsync("/orders", new CreateOrder(3));

        Verify(store.Orders is [{ Id: 1, Quantity: 3 }]);
    }

    [VerifyTest]
    public async Task Each_test_gets_its_own_app(HttpClient client)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders is { Length: 0 });
    }
}
#endregion
