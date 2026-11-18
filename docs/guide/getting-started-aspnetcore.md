# Getting started with VerificationGlue.Tests.AspNetCore

At the end of this page you have a test project that starts your ASP.NET Core app for each test, calls it with an `HttpClient`, and replaces one of its services with a `[Mock]`.

`VerificationGlue.Tests.AspNetCore` builds on `VerificationGlue.Tests`. Read [Getting started with VerificationGlue.Tests](getting-started-tests.md) first if you have not written a `[VerifyTest]` yet.

## The app

The examples test this app. It has a minimal API endpoint that saves an order with the time from `IClock`, and an MVC controller that returns the time:

<!-- snippet: shop-api-program -->
<a id='snippet-shop-api-program'></a>
```cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IOrderStore, OrderStore>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapPost("/orders", (CreateOrder request, IClock clock, IOrderStore store, ILogger<Program> log) =>
{
    var order = store.Add(request.Quantity, clock.Now);
    log.LogInformation("Saved order {Id} of {Quantity}", order.Id, order.Quantity);
    return order;
});
app.MapGet("/orders", (IOrderStore store) => store.Orders);
app.MapControllers();

app.Run();
```
<sup><a href='/samples/ShopApi/Program.cs#L3-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-shop-api-program' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: shop-api-controller -->
<a id='snippet-shop-api-controller'></a>
```cs
[ApiController]
[Route("clock")]
public sealed class ClockController(IClock clock) : ControllerBase
{
    [HttpGet]
    public DateTime Get() => clock.Now;
}
```
<sup><a href='/samples/ShopApi/Shop.cs#L52-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-shop-api-controller' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The app needs no change for its tests. It does not declare `public partial class Program`, and it does not give the test project access to its internals.

## Install

Add both packages to the test project. They are previews:

```sh
dotnet add package VerificationGlue.Tests --prerelease
dotnet add package VerificationGlue.Tests.AspNetCore --prerelease
```

The two packages must have the same version. `VerificationGlue.Tests` adds its build files only to a project that references it, so the test project needs both references. `VerificationGlue.Tests` needs the MTP 3 preview feed in `NuGet.config` and the test runner in `global.json`. [Install VerificationGlue.Tests](getting-started-tests.md#install) shows both lines.

Then reference the app's project:

<!-- snippet: reference-the-app -->
<a id='snippet-reference-the-app'></a>
```csproj
<ItemGroup>
  <ProjectReference Include="../ShopApi/ShopApi.csproj" />
</ItemGroup>
```
<sup><a href='/samples/TestsAspNetCore/TestsAspNetCore.csproj#L20-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-reference-the-app' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Name the app

Write a partial class with `[WebAppEntry]` and the name of the app's assembly:

<!-- snippet: name-the-app -->
<a id='snippet-name-the-app'></a>
```cs
using VerificationGlue.Tests.AspNetCore;

namespace TestsAspNetCore;

[WebAppEntry("ShopApi")]
public sealed partial class ShopApiEntry;
```
<sup><a href='/samples/TestsAspNetCore/ShopApiEntry.cs#L1-L8' title='Snippet source file'>snippet source</a> | <a href='#snippet-name-the-app' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The generator in the package makes `ShopApiEntry` implement `IWebAppEntry`. The generated code calls the app's entry point with no reflection. [Generated entry types](generated/web-app-entry.md) shows it. If the generator cannot find the entry point, it reports [VGA0001](../diagnostics/VGA0001.md). A class that is not partial, is generic or is nested gets [VGA0002](../diagnostics/VGA0002.md).

## Write a test

Put `[WebApp<ShopApiEntry>]` on the test class. A test can then take an `HttpClient` that calls the app, and `[Mock]` parameters:

<!-- snippet: first-app-test -->
<a id='snippet-first-app-test'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/OrderTests.cs#L10-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-first-app-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each test starts its own copy of the app on TestServer, which needs no port. The `[Mock] IClock` replaces the app's `IClock` registration in that copy, so the endpoint stamps the order with the mocked time. The app stops when the test ends.

A controller receives the mock the same way:

<!-- snippet: app-test-controller -->
<a id='snippet-app-test-controller'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/OrderTests.cs#L28-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-app-test-controller' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Take the app's services

A test can also take `IHost` and any service the app registers. A scoped service comes from a scope that the test owns. Each test gets its own app, so state saved by one test is not seen by another:

<!-- snippet: app-test-services -->
<a id='snippet-app-test-services'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/OrderTests.cs#L44-L64' title='Snippet source file'>snippet source</a> | <a href='#snippet-app-test-services' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Run the tests

Run the tests as for any `VerificationGlue.Tests` project:

```sh
dotnet test
```

The app's log at level `Information` and above goes to the test's output, after a line such as `ShopApi started in 25.0 ms`. A TRX report holds the output of each test. [Write a TRX report](getting-started-tests.md#write-a-trx-report) shows how to get one.

## Next

- [Generated entry types](generated/web-app-entry.md)
- [Containers and scopes](containers-and-scopes.md)
- [Parameters and where they come from](test-parameters.md)
- [The design of VerificationGlue.Tests.AspNetCore](../design/aspnet.md), with the phases that are not built yet
