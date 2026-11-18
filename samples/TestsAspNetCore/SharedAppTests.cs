using System.Net;
using System.Net.Http.Json;
using ShopApi;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

namespace TestsAspNetCore;

#region shared-app-class
[WebApp<ShopApiEntry>(Scope = ContainerScope.Class)]
[AppSetting("Orders:MaxQuantity", "3")]
public partial class SharedOrderTests
{
    [VerifyTest]
    public async Task Post_rejects_more_than_the_class_maximum(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(4));

        Verify(response.StatusCode == HttpStatusCode.BadRequest);
    }

    [VerifyTest]
    public async Task Post_saves_the_order_in_the_shared_store(HttpClient client, IOrderStore store)
    {
        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2));
        var order = await response.Content.ReadFromJsonAsync<Order>();

        Verify(store.Orders.Contains(order!));
    }

    [VerifyTest]
    public async Task Each_test_sees_the_orders_of_the_others(HttpClient client)
    {
        await client.PostAsJsonAsync("/orders", new CreateOrder(1));

        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Any(order => order.Quantity == 1));
    }
}
#endregion

#region shared-app-assembly
[WebApp<ShopApiEntry>(Scope = ContainerScope.Assembly)]
public partial class ReportTests
{
    [VerifyTest]
    public async Task Lists_the_orders_of_the_assembly_app(HttpClient client)
    {
        var response = await client.GetAsync("/orders");

        Verify(response.IsSuccessStatusCode);
    }
}

[WebApp<ShopApiEntry>(Scope = ContainerScope.Assembly)]
public partial class ClockReportTests
{
    [VerifyTest]
    public async Task Reads_the_clock_of_the_same_app(HttpClient client)
    {
        var now = await client.GetFromJsonAsync<DateTime>("/clock");

        Verify(now.Year >= 2026);
    }
}
#endregion
