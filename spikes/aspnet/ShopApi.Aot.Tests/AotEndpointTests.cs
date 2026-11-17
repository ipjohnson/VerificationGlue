using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace ShopApi.Tests;

public sealed class ShopApiEntry : IWebAppEntry
{
    public static string ApplicationName => typeof(Program).Assembly.GetName().Name!;

    public static void Run(string[] args) => EntryPoint(null, args);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern void EntryPoint(Program? program, string[] args);
}

[WebApp<ShopApiEntry>]
public partial class AotEndpointTests
{
    [VerifyTest]
    public async Task Post_stamps_the_order_with_the_mocked_clock(HttpClient client, [Mock] IClock clock, IOrderStore store)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2, 10), ShopJson.Default.CreateOrder);
        var order = await response.Content.ReadFromJsonAsync(ShopJson.Default.Order);

        Verify(response.StatusCode == HttpStatusCode.OK);
        Verify(order!.Created == new DateTime(2026, 11, 17));
        Verify(store.Orders.Count == 1);
    }

    [VerifyTest]
    public async Task Each_test_gets_its_own_app(HttpClient client)
    {
        var orders = await client.GetFromJsonAsync("/orders", ShopJson.Default.OrderArray);

        Verify(orders!.Length == 0);
    }
}

[WebApp<ShopApiEntry>(Kestrel = true)]
public partial class AotKestrelTests
{
    [VerifyTest]
    public async Task Listens_on_a_real_port(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2, 10), ShopJson.Default.CreateOrder);
        var order = await response.Content.ReadFromJsonAsync(ShopJson.Default.Order);

        Verify(client.BaseAddress!.Port > 1024);
        Verify(order!.Created == new DateTime(2026, 11, 17));
    }
}
