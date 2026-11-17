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

## Entry point shapes

The second spike is under `spikes/aspnet/Shapes`. It builds one app for each shape of entry point, and `EntryGenerator` in the test project writes the entry type for each app. Every shape starts on the JIT and under Native AOT, and the test's `[Mock]` reaches the app in each one **(verified)**.

The test marks a partial class with the app's assembly name, and the generator fills in `ApplicationName` and `Run`:

```csharp
[WebAppEntry("TaskIntApp")]
public sealed partial class TaskIntAppEntry;

[WebApp<TaskIntAppEntry>]
public partial class TaskIntAppTests { /* ... */ }
```

### Results

All of these are **(verified)**. Each app maps `GET /now` to its `IClock`, and each test checks that the app answers with the mocked time.

| Shape | App | Method the generated entry type calls in a build | JIT | Native AOT |
|---|---|---|---|---|
| Top-level statements that return `void` | `VoidApp` | `void Program.<Main>$(string[])` | passes | passes |
| Top-level statements that return `int` | `IntApp` | `int Program.<Main>$(string[])` | passes | passes |
| Top-level statements that return `Task` | `TaskApp` | `void Program.<Main>(string[])` | passes | passes |
| Top-level statements that return `Task<int>` | `TaskIntApp` | `int Program.<Main>(string[])` | passes | passes |
| An explicit `private static async Task<int> Main` in the static class `MainApp.Server` | `MainApp` | `int MainApp.Server.<Main>(string[])` | passes | passes |
| Top-level statements with `internal partial class Program` | `InternalApp` | `void Program.<Main>$(string[])` | passes | passes |
| The same, with `InternalsVisibleTo` for the test project | `InternalIvtApp` | `void Program.<Main>$(string[])` | passes | passes |
| `Host.CreateDefaultBuilder` with `ConfigureWebHostDefaults` and a `Startup` class | `StartupApp` | `void Program.<Main>$(string[])` | passes | passes when hosts build one at a time |

The evidence:

- On the JIT, the test project runs 13 of 13 tests in 148 to 227 ms over three runs.
- `dotnet publish -c Release` of `Shapes.Aot.Tests` has no warnings and takes 22 s. The binary is 18.8 MB, and a run takes 29 to 32 ms.
- With `WEBAPP_SERIAL_BUILD=1` the bootstrap builds one host at a time. Then all 13 native tests pass in 20 of 20 runs.
- Without it, 12 of 20 native runs fail the test that starts many apps at once. In each of those runs the `Startup` test fails next with the same `NullReferenceException` in `ActivatorUtilities.CreateInstance`. In the other 8 runs the `Startup` test passes.

The `Startup` test on its own does not fail under Native AOT. The failure comes from hosts that build at the same time. [Apps that start at once](#apps-that-start-at-once) traces it to the framework.

### What a generator in the test project can read

A generator sees the app through one of three kinds of reference. `SymbolProbe` compiles each shape and prints what a referencing compilation sees of the entry type **(verified)**:

| Reference | Default import | With `MetadataImportOptions.All` |
|---|---|---|
| `CompilationReference`, which the IDE uses for a project reference | `<Main>$` or `Main` with its signature | the same |
| Reference assembly, which `dotnet build` passes to the compiler | no private method | the PE entry point only |
| Full assembly | no private method | `<Main>$` or `Main`, and `<Main>` for an async entry point |

The PE entry point is the method the runtime starts. For `void` and `int` it is `<Main>$` itself. For an `async` entry point the compiler adds a private method named `<Main>` that returns `void` or `int` and blocks on the task. A reference assembly keeps that one private method and drops the others.

So a generator can write the entry type for every shape, after it switches the compilation's import to `MetadataImportOptions.All`. `EntryGenerator` does that with `compilation.WithOptions(...)` and looks for a static method named `Main`, `<Main>` or `<Main>$` with an entry point signature **(verified)**.

- For an async shape the IDE and the build give the generator different methods. In the IDE it writes an accessor for `Task <Main>$`, and in the build for `void <Main>`. Both methods exist in the app at run time, so either accessor works **(verified)**.
- The generated accessor names the app's type as a string through `UnsafeAccessorType`, such as `"Program, InternalApp"`. An internal `Program` and a static class need no `InternalsVisibleTo` **(verified)**.
- A hand-written entry type that names an internal `Program` without `InternalsVisibleTo` fails with CS0122. With `InternalsVisibleTo` it builds and passes **(verified)**.
- A static class cannot be the type of an accessor's parameter, so an entry type for `MainApp.Server` has to name it as a string **(inference)**.
- The cost of `MetadataImportOptions.All` on each edit in the IDE was not measured.

### An entry type that does not know the shape

`ProbingTaskIntAppEntry` has one accessor for each shape and calls them in turn. A call to an accessor whose method does not exist throws `MissingMethodException`, on the JIT with `Method not found: 'Program.<Main>$'.` and under Native AOT with `Method not found: '<Main>$'.`. The type catches it and tries the next accessor, and the test passes on both **(verified)**. This fallback is not needed, because the generator can read the shape.

### Recommendation

A generator in the test project should write the entry type. It reads the shape from the reference, it needs no `InternalsVisibleTo`, and the test author writes one attribute. An analyzer that checks a hand-written entry type would need to read the same private method, so it would do the generator's work and still leave the code to the user.

## An app that stops before it builds a host

When the entry point throws or returns before it builds a host, the test fails with the reason. It does not hang **(verified)**.

The bootstrap waits for whichever comes first: the host from `HostBuilt`, or the end of the entry point. When the entry point ends first, `WebApp.StartAsync` does this:

- If the entry point threw, it rethrows that exception.
- If the entry point returned, it throws `InvalidOperationException` with the exit code.

The same check runs again while the bootstrap waits for `ApplicationStarted`. The spike has two apps for this. `ThrowApp` throws when its connection string is missing. `ReturnApp` writes a line to standard error and returns 2 unless `Mode` is `serve`. `EarlyExitTests` checks both messages, and `FailingTests.cs` adds two tests that fail on purpose when built with `-p:ShowFailure=true`.

The output on the JIT, with the paths shortened:

```text
failed The_app_returns_before_it_builds_a_host (1ms)
  System.InvalidOperationException: The entry point of ReturnApp returned 2 before it built a host.
    at Shapes.Tests.WebApp.StartAsync[TEntry](IReadOnlyList`1 overrides, ITestOutput output) in .../Shapes.Tests/WebApp.cs:113
    at Shapes.Tests.WebAppBootstrap`1.BuildAsync(TestContainerContext context) in .../Shapes.Tests/WebApp.cs:36
    at VerificationGlue.Tests.TestRunner.BuildContainerAsync(TestContext context, TestScope classScope) in .../TestRunner.cs:291
    at VerificationGlue.Tests.TestRunner.RunAsync(TestDescriptor test) in .../TestRunner.cs:57
failed The_app_throws_before_it_builds_a_host (0ms)
  System.InvalidOperationException: The connection string 'Shop' is missing.
    at Program.<Main>$(String[] args) in .../Apps/ThrowApp/Program.cs:4
    at Shapes.Tests.ThrowAppEntry.EntryPoint(Object type, String[] args)
    at Shapes.Tests.ThrowAppEntry.Run(String[] args) in .../EntryGenerator/ThrowAppEntry.g.cs:12
    at Shapes.Tests.WebApp.<>c__DisplayClass8_0`1.<StartAsync>b__1() in .../Shapes.Tests/WebApp.cs:81
    --- End of stack trace from previous location ---
    at Shapes.Tests.WebApp.StartAsync[TEntry](IReadOnlyList`1 overrides, ITestOutput output) in .../Shapes.Tests/WebApp.cs:112
    ...
```

All of these are **(verified)**:

- A throw shows the app's own exception and the line of `Program.cs` that threw.
- Under Native AOT the message and the frames are the same, but a frame shows an offset in place of a line, such as `Program.<Main>$(String[] args) + 0x11c`.
- A return shows only the exit code. `ReturnApp`'s line on standard error goes to the console of the test process, not to the test's output. The bootstrap's logger is added on `HostBuilding`, so no log line from the app reaches the test before a host exists.
- Each failure takes 2 ms or less.

The package should keep the exit code in the message. It could also capture what the app writes to standard error while it starts, but `Console.SetError` is global to the process, so it would mix the output of apps that start at once **(inference)**.

## Apps that start at once

Two apps can start at once. Each bootstrap receives its own host and its own overrides, on the JIT and under Native AOT. The one exception is an app with a `Startup` class under Native AOT, where building such hosts at the same time can fail inside ASP.NET Core **(verified)**.

`ConcurrentStartTests` starts 16 apps on 16 threads at once: four each of `VoidApp`, `TaskIntApp`, `MainApp` and `StartupApp`. Each app gets a mocked `IClock` that returns a different day. The test checks each of these:

- Each app answers `GET /now` with its own name and its own day.
- The 16 hosts are distinct.
- Each test output holds one start line, so no app's log went to another app's output.
- More than one entry point was running between its start and its `HostBuilt` event at the same time.

The bootstrap keeps the hosts apart because `HostCapture.Current` is an `AsyncLocal` set on each app's thread. The hosting `DiagnosticListener` is created on the thread that builds the host, so it subscribes the capture of that thread only.

All of these are **(verified)**:

| Run | Result |
|---|---|
| JIT, 3 runs | 13 of 13 pass in each. The test reports that 16 of 16 apps were starting at once. |
| Native AOT with `WEBAPP_SERIAL_BUILD=1`, 20 runs | 13 of 13 pass in each, in 28 to 54 ms. At most 1 app was starting at a time. |
| Native AOT without it, 20 runs | 7 runs fail, and 13 pass. In each failing run, the concurrent test fails, and then the `Startup` test fails. The step before this one saw 12 of 20 runs fail. |

The failure is a `NullReferenceException` in `ActivatorUtilities.CreateInstance`, which `GenericWebHostBuilder.UseStartup` calls while the host builds its services:

```text
System.NullReferenceException: Object reference not set to an instance of an object.
  at Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(IServiceProvider, Type, Object[]) + 0x370
  at Microsoft.AspNetCore.Hosting.GenericWebHostBuilder.UseStartup(Type, HostBuilderContext, IServiceCollection, Object) + 0x118
  at Microsoft.AspNetCore.Hosting.GenericWebHostBuilder.<>c__DisplayClass7_0.<UseStartup>b__0(HostBuilderContext context, IServiceCollection services) + 0xb0
  at Microsoft.Extensions.Hosting.HostBuilder.InitializeServiceProvider() + 0xec
  at Microsoft.Extensions.Hosting.HostBuilder.Build() + 0x70
  at Program.<Main>$(String[] args) + 0x90
  at Shapes.Tests.StartupAppEntry.Run(String[]) + 0xc
```

The `ConcurrentBuild` project builds 16 hosts on 16 threads at once in a new process. It has no test bootstrap and no `DiagnosticListener`. Its results **(verified)**:

| Hosts | JIT, 10 runs | Native AOT, 100 runs |
|---|---|---|
| `WebApplication.CreateSlimBuilder` | 0 fail | 0 fail |
| `Host.CreateDefaultBuilder` with a `Startup` class | 0 fail | 3 fail |
| Half of each | 0 fail | 14 fail |

The same `NullReferenceException` appears without the bootstrap, so the fault is in the framework under Native AOT, not in the host capture. The `Startup` test fails after the concurrent test because the fault leaves the process in a broken state **(inference)**. The spike did not find the cause inside `ActivatorUtilities`.

For the package this means:

- Apps can start at once when the runner runs tests in parallel.
- Under Native AOT, the bootstrap should build one host at a time, as `WEBAPP_SERIAL_BUILD=1` does. With it the hosts still start and serve requests at the same time, and only the build is serial. It can apply this to every app, or only to apps built with `Host.CreateDefaultBuilder`. The bootstrap cannot tell the two apart before the entry point runs **(inference)**.

## Not covered

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
2. Whether the design takes the spike's recommendation that a generator writes the entry type, and what `MetadataImportOptions.All` costs on each edit in the IDE.
3. The parameters a test can take: `HttpClient`, the host, the app's services, and whether a type of our own wraps them.
4. How a test sets configuration, the environment name and the choice of TestServer or Kestrel.
5. Whether the bootstrap turns off the configuration watcher by default, as the spike does.
6. Whether the bootstrap builds one host at a time under Native AOT only, or always.
7. Whether the failure of an app that returns before it builds a host shows what the app wrote to standard error.
