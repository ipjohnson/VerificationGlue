using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace ShopApi.Tests;

/// <summary>Calls the entry point that the compiler writes for Program.cs. A generator would write this type.</summary>
public sealed class ShopApiEntry : IWebAppEntry
{
    public static string ApplicationName => typeof(Program).Assembly.GetName().Name!;

    public static void Run(string[] args) => EntryPoint(null, args);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern void EntryPoint(Program? program, string[] args);
}

[WebApp<ShopApiEntry>]
public partial class OrderEndpointTests
{
    [VerifyTest]
    public async Task Post_stamps_the_order_with_the_mocked_clock(HttpClient client, [Mock] IClock clock, IOrderStore store)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2, 10));
        var order = await response.Content.ReadFromJsonAsync<Order>();

        Verify(response.StatusCode == HttpStatusCode.OK);
        Verify(order!.Created == new DateTime(2026, 11, 17));
        Verify(store.Orders.Count == 1);
    }

    [VerifyTest]
    public async Task Each_test_gets_its_own_app(HttpClient client)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }
}

[WebApp<ShopApiEntry>(Scope = ContainerScope.Class)]
public partial class SharedAppTests
{
    [VerifyTest]
    public async Task First_test_saves_an_order(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(1, 5));

        Verify(response.StatusCode == HttpStatusCode.OK);
    }

    [VerifyTest]
    public async Task Second_test_sees_the_shared_store(HttpClient client, IOrderStore store)
    {
        await client.PostAsJsonAsync("/orders", new CreateOrder(1, 5));

        Verify(store.Orders.Count >= 1);
    }
}
