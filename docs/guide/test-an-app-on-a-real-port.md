# Test an app on a real port

At the end of this page a test starts the app on Kestrel, on a free port of `127.0.0.1`, and calls it over a socket. You ask for this with `[Kestrel]` on the test, its class or its assembly.

Without `[Kestrel]` the app runs on TestServer, which serves requests in memory and opens no socket. Use `[Kestrel]` when the test needs a real address, such as for a client that you build yourself or a tool that calls a URL.

The examples start the app from [Getting started with VerificationGlue.Tests.AspNetCore](getting-started-aspnetcore.md).

## Ask for Kestrel on a test

Put `[Kestrel]` on the test. The test's `HttpClient` has the app's address as its `BaseAddress`:

<!-- snippet: kestrel-test -->
<a id='snippet-kestrel-test'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/KestrelTests.cs#L10-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-kestrel-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The address is `http://127.0.0.1` with the port that Kestrel bound, such as `http://127.0.0.1:52814`. Each test that runs on Kestrel gets its own port, so tests that run at once do not collide.

A `[Mock]` parameter reaches the app on Kestrel as it does on TestServer. In the test above the app stamps the order with the mock's date.

## Make your own client

The app listens on a socket, so any client can call it. Build it with the `BaseAddress` of the test's `HttpClient`:

<!-- snippet: kestrel-own-client -->
<a id='snippet-kestrel-own-client'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/KestrelTests.cs#L29-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-kestrel-own-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`[Kestrel]` on the class applies to each test in it. On the assembly it applies to every test in the project. A test with no `[Kestrel]` at any level stays on TestServer.

## How the app gets its port

The package passes `urls=http://127.0.0.1:0` to the app as a configuration value, in the same way as [`[AppSetting]`](configure-the-app-for-a-test.md). It leaves the app's own server in place, so the app runs on Kestrel as it does in production. Port 0 asks the operating system for a free port. After the app starts, the package reads the bound address from `IServerAddressesFeature` and gives it to the test's `HttpClient`.

An app that calls `WebApplication.CreateBuilder()` without `args` also gets the address. The server reads `urls` when it starts, which is after `Build()`.

An app that sets its own endpoints, such as with `Kestrel:Endpoints` in `appsettings.json` or with `ListenLocalhost` in code, listens where it says instead. The test's `HttpClient` then has the first address that the app bound.

## Next

- [Configure the app for a test](configure-the-app-for-a-test.md)
- [Getting started with VerificationGlue.Tests.AspNetCore](getting-started-aspnetcore.md)
- [The design of VerificationGlue.Tests.AspNetCore](../design/aspnet.md)
