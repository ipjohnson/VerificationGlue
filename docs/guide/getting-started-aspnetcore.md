# Getting started with VerificationGlue.Tests.AspNetCore

At the end of this page you can write a test that calls an ASP.NET Core app through an `HttpClient`, with the test's mocks in the app.

`VerificationGlue.Tests.AspNetCore` starts the app for each test on TestServer and stops it when the test ends. It builds on `VerificationGlue.Tests`, so set up that package first with [Getting started with VerificationGlue.Tests](getting-started-tests.md).

## Install

Add the package and a reference to the app's project to the test project:

```xml
<ItemGroup>
  <PackageReference Include="VerificationGlue.Tests" Version="$(VerificationGlueVersion)" />
  <PackageReference Include="VerificationGlue.Tests.AspNetCore" Version="$(VerificationGlueVersion)" />
  <ProjectReference Include="../ShopApi/ShopApi.csproj" />
</ItemGroup>
```

The test project keeps its direct reference to `VerificationGlue.Tests`, because that package's `build` files make the project a test application. Both packages must have the same version. `VerificationGlue.Tests.AspNetCore` brings `Microsoft.AspNetCore.TestHost` and the ASP.NET Core shared framework. Nothing in the app changes for tests.

## Name the app's entry point

The package calls the app's entry point through a class that implements `IWebAppEntry`. For an app with top-level statements, write it with an `UnsafeAccessor` to the method the compiler generates:

<!-- snippet: shopapi-entry -->
<a id='snippet-shopapi-entry'></a>
```cs
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
```
<sup><a href='/samples/ShopApi.Tests/OrderTests.cs#L12-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-shopapi-entry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ApplicationName` is the name of the app's assembly. `UnsafeAccessorType` names the app's `Program` class as a string, so an internal `Program` needs no `InternalsVisibleTo`. When the app's top-level statements return an `int`, declare `EntryPoint` to return `int` and return its result from `Run`.

A later version generates this class from one attribute.

## Write a test

Mark the test class with `[WebApp<TEntry>]`. A test takes an `HttpClient`, the app's services and `[Mock]` parameters:

<!-- snippet: first-app-test -->
<a id='snippet-first-app-test'></a>
```cs
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
```
<sup><a href='/samples/ShopApi.Tests/OrderTests.cs#L28-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-first-app-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The first test sets up the mock clock, posts an order, and reads the order back from the response and from the app's store. The second test shows that each test gets a new app, so an order saved by the first test is not there.

## What a test gets

| Parameter type | Value |
|---|---|
| `HttpClient` | A client for the test's app on TestServer. |
| `IHost` | The app's host. |
| A `[Mock]` parameter | A mock that the app also receives for that service type, in place of its own registration. |
| Any other type | A service from a service scope created for the test, or no value when the app does not register the type. |

The package calls the app's entry point on a new thread with `--applicationName`, `--environment=Development` and `--hostBuilder:reloadConfigOnChange=false`. While the host builds, it adds TestServer, the test's mocks and a logger that writes to the test's output. It removes the app's other logger providers.

## The app's log

What the app logs while it serves a test is in that test's output, with the app's start time. A failed test shows it under "Standard output":

```text
ShopApi started in 12.0 ms
[Information] Microsoft.AspNetCore.Hosting.Diagnostics: Request starting HTTP/1.1 POST http://localhost/orders - application/json;+charset=utf-8 -
[Information] Microsoft.AspNetCore.Routing.EndpointMiddleware: Executing endpoint 'HTTP: POST /orders'
```

## When the test ends

The package stops the app and waits for its entry point to return before it reports the test's result. An exception that the entry point throws while the app stops fails the test.

## An app that does not start

When the entry point throws before the app starts, the test fails with the app's exception and its stack trace. When the entry point returns before it builds a host, the test fails with the exit code:

```text
The entry point of ShopApi returned 2 before it built a host. What the app wrote to standard error went to the console of the test process.
```

The other tests still run.

## Not yet supported

Each test starts its own app. An app shared by a class or an assembly, configuration values, the environment name, Kestrel, and Native AOT are designed in [the package design](../design/aspnet.md) and are not built yet.

## Next

- [Containers and scopes](containers-and-scopes.md)
- [Parameters and where they come from](test-parameters.md)
- [Getting started with VerificationGlue.Mocks](getting-started-mocks.md)
