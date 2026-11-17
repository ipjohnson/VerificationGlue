# Testing an ASP.NET Core app from a test parameter

Research for a possible `VerificationGlue.Tests.AspNetCore` package. Date: 2026-11-17. The spike is committed under `spikes/aspnet`. It builds against `src/` at commit 670d373.

## Result

An ASP.NET Core app can be the container of a test. The bootstrap starts the app, the test takes an `HttpClient`, its `[Mock]` parameters and the app's services as parameters, and disposing the container stops the app. This needs no change to `VerificationGlue.Tests`.

```csharp
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
}
```

Starting an app for each test is cheap. 46 tests with 45 app starts run in about 400 ms, and the median start takes 3.1 ms. The same approach publishes with Native AOT with no warnings.

Each claim below is marked **(verified)** when a run of the spike showed it, or **(inference)** when it comes from reading code.

## Decisions

The user took these on 2026-11-17, after the spike.

- App testing ships as a fourth package. The north star lists three packages, and it needs that change when the design is written.
- The first cut covers HTTP with mocks. Database fixtures are left out, because there are many databases and one HTTP client.

## Pinned stack

| Component | Version |
|---|---|
| .NET SDK | 10.0.403, osx-arm64, from the repository's `global.json` |
| ASP.NET Core shared framework | 10.0.14 |
| Microsoft.AspNetCore.TestHost | 10.0.0 |
| Microsoft.Testing.Platform | 3.0.0-preview.26566.9 |
| Machine | Apple M3 Pro, macOS 27.0 |

## The spike

| Path | What it is |
|---|---|
| `ShopApi/Program.cs` | A top-level `Program.cs` with `WebApplication.CreateBuilder`, two minimal API endpoints and an MVC controller. It has no code for tests. |
| `ShopApi.Tests/WebApp.cs` | `WebAppAttribute<TEntry>`, `WebAppBootstrap<TEntry>` and the container. This is what the package would ship. |
| `ShopApi.Tests/OrderEndpointTests.cs` | `ShopApiEntry`, the five lines that name the app's entry point, and the tests for an app per test and an app per class. |
| `ShopApi.Tests/ManyTests.cs` | 40 tests that each start an app, for the timings. |
| `ShopApi.Tests/KestrelTests.cs`, `ControllerTests.cs` | A test on a real port, and a test of a controller that takes the mock. |
| `ShopApi.Tests/FailingTests.cs` | A test that fails on purpose. It is compiled only with `-p:ShowFailure=true`. |
| `ShopApi.Aot`, `ShopApi.Aot.Tests` | The same app with `CreateSlimBuilder` and a JSON source generation context, and its tests, published with Native AOT. |

## How the bootstrap starts the app

`WebAppBootstrap<TEntry>.BuildAsync` does this for each container:

1. It starts a thread and calls the entry point that the compiler writes for `Program.cs`. `ShopApiEntry` reaches that method, named `<Main>$`, through `UnsafeAccessor`, so the call uses no reflection.
2. The hosting library announces each host on a `DiagnosticListener` named `Microsoft.Extensions.Hosting`. On `HostBuilding` the bootstrap adds `TestServer` as the server, the test's overrides, and a logger that writes to the test's output. On `HostBuilt` it takes the host.
3. It waits for `IHostApplicationLifetime.ApplicationStarted` and returns a container over the host. The container supplies `HttpClient`, `IHost` and the services of the app.
4. `DisposeAsync` stops the app and waits for the entry point to return.

The bootstrap does not use `WebApplicationFactory`. `WebApplicationFactory` calls the entry point through reflection and reads the app's `deps.json` file, which does not suit a Native AOT test binary **(inference)**.

A class or assembly container starts one app. Each test then gets a child with its own `HttpClient` and service scope.

## What it showed

All of these are **(verified)**.

| Claim | Evidence |
|---|---|
| An app per test is cheap on the JIT. | 46 tests with 45 app starts take 388 to 510 ms over three runs. The median start is 3.1 to 3.5 ms. The first start takes 61 to 145 ms. |
| Without the MVC controller, a start is faster. | With minimal API endpoints only, the median start was 1.0 ms and 45 tests took 273 ms. |
| Watching the configuration files is what makes a start slow. | With `WEBAPP_WATCH_CONFIG=1` the bootstrap leaves the watcher on. The median start is then 64 to 82 ms and the run takes 3.3 to 3.8 s. The bootstrap passes `--hostBuilder:reloadConfigOnChange=false` by default. |
| A `[Mock]` parameter reaches the app. | `OrderEndpointTests` reads the mocked `IClock` through a minimal API endpoint, and `ControllerTests` through a controller. |
| The app's log reaches the test. | The failing test's output lists the request and the `ShopApi.OrderService` log line. See below. |
| A test can use a real port. | `[WebApp<ShopApiEntry>(Kestrel = true)]` listens on a free loopback port, and `KestrelTests` checks the port and the mock. |
| A controller needs the app's name. | Without `--applicationName` set to the app's assembly name, `GET /clock` returns 404, because MVC looks for controllers in the test assembly. |
| A shared app with a `[Mock]` parameter is a build error. | A class with `[WebApp<ShopApiEntry>(Scope = ContainerScope.Class)]` and a `[Mock]` parameter fails the build with VGT0008. |
| The approach works under Native AOT. | `dotnet publish -c Release` of `ShopApi.Aot.Tests` has no warnings and takes 15 s. The binary is 15.8 MB. It runs 3 tests in 14 to 29 ms, on TestServer and on Kestrel, with the mock. |

The output of the failing test:

```text
failed A_failing_test_shows_the_app_log (19ms)
  Verify failed: response.StatusCode == System.Net.HttpStatusCode.Created
    expected response.StatusCode to equal System.Net.HttpStatusCode.Created
  Standard output
    App started in 11.3 ms
    [Information] Microsoft.AspNetCore.Hosting.Diagnostics: Request starting HTTP/1.1 POST http://localhost/orders - application/json;+charset=utf-8 -
    [Information] Microsoft.AspNetCore.Routing.EndpointMiddleware: Executing endpoint 'HTTP: POST /orders'
    [Information] ShopApi.OrderService: Created an order of 3 at 7
    [Information] Microsoft.AspNetCore.Routing.EndpointMiddleware: Executed endpoint 'HTTP: POST /orders'
    [Information] Microsoft.AspNetCore.Hosting.Diagnostics: Request finished HTTP/1.1 POST http://localhost/orders - 200 - application/json;+charset=utf-8 6.7520ms
```

## Not covered

- **The entry point's shape.** `ShopApiEntry` is written by hand and matches a `Program.cs` with no `await`, whose entry point returns `void`. A `Program.cs` with `await` or a `return` has another signature. A generator should write the entry type, and the spike did not check that a generator can read the signature of a private method in a referenced assembly.
- **Parallel tests.** The bootstrap finds its host through an `AsyncLocal` set on the app's thread, so two apps that start at once should not mix **(inference)**. The runner runs one test at a time until `tests.md` Phase 3.
- **Heavier apps.** The spike's app has three services. An app with EF Core, hosted services or a slow startup was not measured.
- **Per-test configuration.** The spike passes fixed command-line arguments. It has no way for a test to set a configuration value or the environment name.
- **Authentication.** The spike has no endpoint that needs a user.
- **A mock under a shared app.** VGT0008 stays. A built service provider takes no new registrations, so a per-test mock cannot reach an app that other tests share **(inference)**.

## Fixtures that outlive a test

A database is the usual case: start it once for the assembly and give each app its connection string. The first cut leaves it out. The core has no place for it today **(inference)**:

- `[Container<T>]` is single-valued, so a test cannot have an assembly container for the database and a test container for the app.
- A test-scoped container gets a null `TestContainerContext.Parent`.
- `TestScope` holds no state, and the runner creates the attributes again for each scope. An `ITestScopeListener` on the assembly scope therefore cannot hand a value to the listener on a test scope without a static field.

The part every database shares is small: start once, hand a value to each test's app, and clean up between tests. The core needs only a place for a scope to hold a value that narrower scopes can read. The database itself stays outside the package.

## What the design must answer

1. The name and surface of the fourth package, and the change to the north star's package list.
2. How the entry type is written: by a generator, or by hand with an analyzer that checks it.
3. The parameters a test can take: `HttpClient`, the host, the app's services, and whether a type of our own wraps them.
4. How a test sets configuration, the environment name and the choice of TestServer or Kestrel.
5. Whether the bootstrap turns off the configuration watcher by default, as the spike does.
