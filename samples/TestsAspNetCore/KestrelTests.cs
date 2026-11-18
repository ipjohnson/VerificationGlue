using System.Net.Http.Json;
using ShopApi;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

namespace TestsAspNetCore;

#region kestrel-test
[WebApp<ShopApiEntry>]
public partial class RealPortTests
{
    [VerifyTest]
    [Kestrel]
    public async Task Post_on_a_real_port_stamps_the_order_with_the_clock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2026, 11, 18));

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2));
        var order = await response.Content.ReadFromJsonAsync<Order>();

        Verify(client.BaseAddress!.Host == "127.0.0.1");
        Verify(order!.Created == new DateTime(2026, 11, 18));
    }
}
#endregion

#region kestrel-own-client
[WebApp<ShopApiEntry>]
[Kestrel]
public partial class OwnClientTests
{
    [VerifyTest]
    public async Task A_client_the_test_makes_reaches_the_app(HttpClient client)
    {
        using var own = new HttpClient { BaseAddress = client.BaseAddress };

        var orders = await own.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders is { Length: 0 });
    }
}
#endregion
