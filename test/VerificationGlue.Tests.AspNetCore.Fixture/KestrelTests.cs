using System.Net.Http.Json;
using FixtureShop;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

[WebApp<FixtureShopEntry>]
public partial class KestrelTests
{
    [VerifyTest]
    [Kestrel]
    public void Listens_on_a_free_loopback_port_that_the_client_names(HttpClient client, IHost host)
    {
        var server = host.Services.GetRequiredService<IServer>();
        var address = new Uri(server.Features.Get<IServerAddressesFeature>()!.Addresses.Single());

        Verify(server is not TestServer);
        Verify(address.Host == "127.0.0.1");
        Verify(address.Port > 0);
        Verify(client.BaseAddress == address);
    }

    [VerifyTest]
    [Kestrel]
    public async Task A_client_the_test_makes_reaches_the_app(HttpClient client)
    {
        using var other = new HttpClient { BaseAddress = client.BaseAddress };

        var orders = await other.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders is { Length: 0 });
    }

    [VerifyTest]
    [Kestrel]
    public async Task The_mock_reaches_the_app_on_Kestrel(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2026, 11, 18));

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2));
        var order = await response.Content.ReadFromJsonAsync<Order>();

        Verify(order!.Created == new DateTime(2026, 11, 18));
    }

    [VerifyTest]
    public void A_test_without_Kestrel_stays_on_TestServer(HttpClient client, IHost host)
    {
        Verify(host.Services.GetRequiredService<IServer>() is TestServer);
        Verify(client.BaseAddress == new Uri("http://localhost/"));
    }

    /// <summary>
    /// An assembly attribute would move every test in this fixture to Kestrel, so this test passes the
    /// attributes of each scope to the bootstrap itself, widest first, as the runner does.
    /// </summary>
    [VerifyTest]
    public async Task Kestrel_on_the_assembly_applies_to_the_tests_under_it()
    {
        Verify(await IsOnKestrelAsync([new KestrelAttribute()]));
        Verify(await IsOnKestrelAsync([new KestrelAttribute(), new AppEnvironmentAttribute("Class")]));
        Verify(await IsOnKestrelAsync([new KestrelAttribute(), new AppEnvironmentAttribute("Class"), new AppEnvironmentAttribute("Method")]));
        Verify(!await IsOnKestrelAsync([new AppEnvironmentAttribute("Class")]));
    }

    private static async Task<bool> IsOnKestrelAsync(Attribute[] attributes)
    {
        await using var app = await WebAppBootstrap<FixtureShopEntry>.StartAsync("kestrel", attributes, [], new NoOutput(), CancellationToken.None);
        if (app.Client.BaseAddress is not { Host: "127.0.0.1", Port: > 0 })
        {
            return false;
        }

        var orders = await app.Client.GetFromJsonAsync<Order[]>("/orders");
        return orders is { Length: 0 };
    }

    private sealed class NoOutput : ITestOutput
    {
        public void WriteLine(string message)
        {
        }
    }
}

[WebApp<FixtureShopEntry>]
[Kestrel]
public partial class ClassKestrelTests
{
    [VerifyTest]
    public async Task Kestrel_on_the_class_applies_to_each_test(HttpClient client, IHost host)
    {
        Verify(host.Services.GetRequiredService<IServer>() is not TestServer);
        Verify(client.BaseAddress is { Host: "127.0.0.1", Port: > 0 });
        Verify(await client.GetFromJsonAsync<Order[]>("/orders") is { Length: 0 });
    }

    [VerifyTest]
    public void Kestrel_on_the_class_applies_to_another_test(HttpClient client)
    {
        Verify(client.BaseAddress is { Host: "127.0.0.1", Port: > 0 });
    }
}

[WebApp<NoArgsAppEntry>]
public partial class NoArgsKestrelTests
{
    [VerifyTest]
    [Kestrel]
    public async Task An_app_that_does_not_pass_args_listens_on_the_loopback_port(HttpClient client)
    {
        Verify(client.BaseAddress is { Host: "127.0.0.1", Port: > 0 });
        Verify(await client.GetStringAsync("/environment") == "Production");
    }
}
