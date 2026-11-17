using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using ShopApi;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

namespace ShopApi.Tests;

#region shopapi-entry
public sealed class ShopApiEntry : IWebAppEntry
{
    public static string ApplicationName => "ShopApi";

    public static int Run(string[] args)
    {
        EntryPoint(null, args);
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern void EntryPoint([UnsafeAccessorType("Program, ShopApi")] object? program, string[] args);
}
#endregion

#region first-app-test
[WebApp<ShopApiEntry>]
public partial class OrderTests
{
    [VerifyTest]
    public async Task Post_stamps_the_order_with_the_clock(HttpClient client, [Mock] IClock clock, IOrderStore store)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2));
        var order = await response.Content.ReadFromJsonAsync<Order>();

        Verify(response.StatusCode == HttpStatusCode.OK);
        Verify(order!.Created == new DateTime(2026, 11, 17));
        Verify(store.Orders.Count == 1);
    }

    [VerifyTest]
    public async Task Each_test_starts_with_no_orders(HttpClient client)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }
}
#endregion
