using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using FixtureShop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

public sealed class FixtureShopEntry : IWebAppEntry
{
    public static string ApplicationName => "FixtureShop";

    public static int Run(string[] args) => EntryPoint(null, args);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern int EntryPoint([UnsafeAccessorType("Program, FixtureShop")] object? program, string[] args);
}

public sealed class ThrowingStartEntry : IWebAppEntry
{
    public static string ApplicationName => FixtureShopEntry.ApplicationName;

    public static int Run(string[] args) => FixtureShopEntry.Run([.. args, "--Fixture:Start=throw"]);
}

public sealed class ReturningStartEntry : IWebAppEntry
{
    public static string ApplicationName => FixtureShopEntry.ApplicationName;

    public static int Run(string[] args) => FixtureShopEntry.Run([.. args, "--Fixture:Start=return"]);
}

[WebApp<FixtureShopEntry>]
public partial class AppTests
{
    [VerifyTest]
    public async Task Reads_the_response_of_an_endpoint(HttpClient client)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders is { Length: 0 });
    }

    [VerifyTest]
    public async Task First_app_lists_only_its_own_order(HttpClient client) => await SaveOneOrderAndListAsync(client);

    [VerifyTest]
    public async Task Second_app_lists_only_its_own_order(HttpClient client) => await SaveOneOrderAndListAsync(client);

    [VerifyTest]
    public async Task Minimal_api_endpoint_receives_the_mock(HttpClient client, [Mock] IClock clock, IHost host)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var order = await PostAsync(client, 2);

        Verify(order.Created == new DateTime(2026, 11, 17));
        Verify(ReferenceEquals(host.Services.GetRequiredService<IClock>(), clock));
    }

    [VerifyTest]
    public async Task Controller_receives_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2030, 1, 2));

        var now = await client.GetFromJsonAsync<DateTime>("/clock");

        Verify(now == new DateTime(2030, 1, 2));
    }

    [VerifyTest]
    public async Task Takes_a_service_of_the_app(HttpClient client, IOrderStore store)
    {
        await PostAsync(client, 2);

        Verify(store.Orders.Count == 1);
    }

    [VerifyTest]
    public async Task Fails_after_saving_an_order(HttpClient client)
    {
        var order = await PostAsync(client, 7);

        Verify(order.Quantity == 8);
    }

    private static async Task SaveOneOrderAndListAsync(HttpClient client)
    {
        await PostAsync(client, 2);

        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders is { Length: 1 });
    }

    private static async Task<Order> PostAsync(HttpClient client, int quantity)
    {
        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(quantity));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Order>())!;
    }
}

[WebApp<ThrowingStartEntry>]
public partial class ThrowingStartTests
{
    [VerifyTest]
    public void Fails_with_the_exception_of_the_app(HttpClient client) => Verify(client is not null);
}

[WebApp<ReturningStartEntry>]
public partial class ReturningStartTests
{
    [VerifyTest]
    public void Fails_with_the_exit_code_of_the_app(HttpClient client) => Verify(client is not null);
}
