# Share an app across a class

At the end of this page you can start one app for the tests of a class or of the whole test assembly. You also know what a shared app cannot do, so you can choose between a shared app and an app per test.

The examples start the app from [Getting started with VerificationGlue.Tests.AspNetCore](getting-started-aspnetcore.md).

## When to share an app

Keep the default, an app per test, unless the app is slow to start. An app per test gives each test its own services, its own `[Mock]` parameters and its own configuration. The ShopApi app starts in a few milliseconds, so a test pays little for its own copy.

Share an app when its start costs more than the tests that use it. An app that migrates a database, warms a cache or starts hosted services is the usual case. The tests of a shared app must not need a mock, a configuration value of their own or a store that starts empty.

## Share an app across a class

Set `Scope = ContainerScope.Class` on `[WebApp<TEntry>]`:

<!-- snippet: shared-app-class -->
<a id='snippet-shared-app-class'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/SharedAppTests.cs#L10-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-shared-app-class' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The app starts when the first selected test of the class starts, and it stops after the last selected test of the class. Its start line, such as `ShopApi started in 12.0 ms`, is in the output of the test that started it.

Each test gets its own `HttpClient` and its own service scope:

- The client is disposed when the test ends. Under `[Kestrel]` each client has the app's address as its `BaseAddress`.
- A scoped service that a test takes as a parameter comes from the test's scope. It is disposed when the test ends, while the app keeps running.
- A singleton, such as `IOrderStore`, is the same instance in every test of the class. `Each_test_sees_the_orders_of_the_others` reads the orders that the other tests saved, so the tests do not check that the store is empty or count its orders.

The app reads `[AppSetting]`, `[AppEnvironment]`, `[Kestrel]` and `[WatchAppConfiguration]` from the class and the assembly. In the example the class sets `Orders:MaxQuantity` to 3, so `Post_rejects_more_than_the_class_maximum` gets `400 Bad Request` for an order of 4.

What the app logs while it serves a request on TestServer is in the output of the test that sent the request.

## Share an app across the assembly

Set `Scope = ContainerScope.Assembly`. Two classes that name the same entry type with assembly scope reach one app:

<!-- snippet: shared-app-assembly -->
<a id='snippet-shared-app-assembly'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/SharedAppTests.cs#L44-L68' title='Snippet source file'>snippet source</a> | <a href='#snippet-shared-app-assembly' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The app starts when the first test that uses it starts, and it stops after the last test of the run. It reads the configuration attributes of the assembly only. An `[AppSetting]` on one of the classes does not reach it, because the other classes share the same app.

## What a shared app cannot do

A test of a shared app cannot take a `[Mock]` parameter. The app's service provider is built once, and it takes no registrations after that, so the test's mock could not reach the app. The build fails with [VGT0008](../diagnostics/VGT0008.md#under-a-shared-app):

```csharp
[WebApp<ShopApiEntry>(Scope = ContainerScope.Class)]
public partial class SharedClockTests
{
    [VerifyTest]
    public async Task Post_stamps_the_order_with_the_clock(HttpClient client, [Mock] IClock clock) { }
}
```

Move such a test to a class whose `[WebApp<TEntry>]` has the default scope.

A configuration attribute on a test method does not reach a shared app. The app has started before the test, so `[AppSetting]`, `[AppEnvironment]`, `[Kestrel]` and `[WatchAppConfiguration]` go on the class or the assembly. On a test method they fail the build with [VGA0003](../diagnostics/VGA0003.md).

Under `[Kestrel]`, what a shared app logs while it serves a request is not in any test's output. Kestrel runs the request without the test's execution context.

## Next

- [Configure the app for a test](configure-the-app-for-a-test.md)
- [Test an app on a real port](test-an-app-on-a-real-port.md)
- [Containers and scopes](containers-and-scopes.md)
- [The design of VerificationGlue.Tests.AspNetCore](../design/aspnet.md#scopes)
