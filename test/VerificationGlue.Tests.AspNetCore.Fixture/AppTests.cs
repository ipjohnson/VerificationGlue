using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using FixtureShop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

[WebAppEntry("FixtureShop")]
public sealed partial class FixtureShopEntry;

[WebAppEntry("ThrowApp")]
public sealed partial class ThrowAppEntry;

[WebAppEntry("ReturnApp")]
public sealed partial class ReturnAppEntry;

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
    public void Takes_a_scoped_service_that_is_disposed_when_the_test_ends(Basket basket)
    {
        Verify(!basket.IsDisposed);
    }

    [VerifyTest]
    public void Does_not_watch_its_configuration_files(IConfiguration configuration)
    {
        var files = ((IConfigurationRoot)configuration).Providers.OfType<FileConfigurationProvider>().ToArray();

        Verify(files.Length > 0);
        Verify(files.All(file => !file.Source.ReloadOnChange));
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

    internal static async Task<Order> PostAsync(HttpClient client, int quantity)
    {
        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(quantity));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Order>())!;
    }
}

[WebApp<ThrowAppEntry>]
public partial class ThrowingStartTests
{
    [VerifyTest]
    public void Fails_with_the_exception_of_the_app(HttpClient client) => Verify(client is not null);
}

[WebApp<ReturnAppEntry>]
public partial class ReturningStartTests
{
    [VerifyTest]
    public void Fails_with_the_exit_code_of_the_app(HttpClient client) => Verify(client is not null);
}

/// <summary>
/// The runner starts one test at a time, so this test starts its apps itself, each with its own clock
/// and its own output.
/// </summary>
public partial class ConcurrentStartTests
{
    private const int Apps = 8;

    [VerifyTest]
    public async Task Apps_that_start_at_once_get_their_own_host_and_overrides(ITestOutput output)
    {
        BuildWindow.Reset();
        var outputs = Enumerable.Range(0, Apps).Select(_ => new Lines()).ToArray();
        var starts = Enumerable.Range(0, Apps).Select(i => WebAppBootstrap<FixtureShopEntry>.StartAsync(
            $"app {i}",
            [new TestOverride(typeof(IClock), new FixedClock(new DateTime(2030, 1, 1).AddDays(i)), null)],
            outputs[i],
            CancellationToken.None)).ToArray();
        try
        {
            var apps = await Task.WhenAll(starts);

            Verify(apps.Select(app => app.Host).Distinct().Count() == Apps);
            for (var i = 0; i < Apps; i++)
            {
                var order = await AppTests.PostAsync(apps[i].Client, i + 1);

                Verify(order.Id == 1);
                Verify(order.Created == new DateTime(2030, 1, 1).AddDays(i));
                Verify(outputs[i].Count(line => line.StartsWith("FixtureShop started in ", StringComparison.Ordinal)) == 1);
                Verify(outputs[i].Count(line => line.StartsWith("[Information] FixtureShop.Orders: Saved order", StringComparison.Ordinal)) == 1);
            }
        }
        finally
        {
            foreach (var start in starts.Where(start => start.IsCompletedSuccessfully))
            {
                await start.Result.DisposeAsync();
            }
        }

        output.WriteLine($"Dynamic code: {RuntimeFeature.IsDynamicCodeSupported}. Most entry points before Build at once: {BuildWindow.Most}.");
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            Verify(BuildWindow.Most == 1);
        }
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime Now => now;
    }

    private sealed class Lines : ITestOutput
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

        public void WriteLine(string message) => _lines.Enqueue(message);

        public int Count(Func<string, bool> match) => _lines.Count(match);
    }
}
