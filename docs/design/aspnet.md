# VerificationGlue.Tests.AspNetCore: an ASP.NET Core app as the container of a test

Status: design. Parent: `docs/north-star.md`. Research: `docs/research/aspnet-report.md`, from ipjohnson/VerificationGlue#37. The spikes are under `spikes/aspnet`.

## What the user gets

- A test project installs `VerificationGlue.Tests.AspNetCore` and references the app's project. Nothing in the app changes for tests.
- A test takes an `HttpClient` as a parameter. The app starts for that test, and it stops when the test ends.
- The test's `[Mock]` parameters are registered in the app, so the app's endpoints, controllers and services receive the same instances as the test.
- A test can also take `IHost` and any service the app registers.
- What the app logs while it serves the test goes to that test's output.
- Attributes on the test, its class or its assembly set configuration values, the environment name, and whether the app listens on a real port.
- The test project publishes with Native AOT, and the app starts with no reflection.

```csharp
using static VerificationGlue.VerifySyntax;

[WebAppEntry("ShopApi")]
public sealed partial class ShopApiEntry;

[WebApp<ShopApiEntry>]
public partial class OrderEndpointTests
{
    [VerifyTest]
    [AppSetting("Orders:MaxQuantity", "5")]
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

The spike ran the same test without `[AppSetting]`. An app start took a median of 3.1 ms on the JIT, and 46 tests with 45 app starts ran in about 400 ms.

## Decisions

The user took these on 2026-11-17:

- App testing ships as a fourth package, not as part of `VerificationGlue.Tests`.
- The first cut covers HTTP with mocks. Fixtures that outlive a test, such as a database, are not in it. There are many databases and one HTTP client.

## Answers to the report's questions

`docs/research/aspnet-report.md` lists seven questions under "What the design must answer". This design answers each one.

1. **Name, surface and north star.** The package is `VerificationGlue.Tests.AspNetCore`. Its surface is under [Package](#package) and [Attributes](#attributes). The north star lists it as the fourth package, with its dependencies, its place in the layout and its release.
2. **Who writes the entry type.** A generator in the package writes it. The test author writes one attribute on a partial class. See [The entry type](#the-entry-type). The cost of `MetadataImportOptions.All` on each edit in the IDE is not measured. The generator is built so that an edit to the test project does not pay it, and Phase 1 measures it before the package ships a preview.
3. **Parameters.** A test takes `HttpClient`, `IHost` and the app's services directly. No type of ours wraps them. See [Parameters](#parameters).
4. **Configuration, environment and server.** `[AppSetting]`, `[AppEnvironment]` and `[Kestrel]` on the test, the class or the assembly. The default is TestServer in the `Development` environment. See [Configuration](#configuration).
5. **The configuration watcher.** The bootstrap turns it off by default. `[WatchAppConfiguration]` turns it back on. The watcher is the difference between a 3 ms and a 64 ms start in the spike.
6. **Serial host builds.** The bootstrap builds one host at a time under Native AOT only. On the JIT, hosts build at once. See [Apps that start at once](#apps-that-start-at-once).
7. **Standard error of an app that stops early.** The failure does not show it. The message keeps the exit code and says that the app's standard error went to the console of the test process. See [An app that stops before it builds a host](#an-app-that-stops-before-it-builds-a-host).

## Package

`VerificationGlue.Tests.AspNetCore` holds a runtime library and a generator.

- It depends on the exact version of `VerificationGlue.Tests`, and through it on `VerificationGlue` and `VerificationGlue.Mocks`.
- It depends on `Microsoft.AspNetCore.TestHost`.
- It has a `FrameworkReference` to `Microsoft.AspNetCore.App`, which flows to the test project.
- It targets net10, like the other three packages.

The runtime holds `IWebAppEntry`, `WebAppEntryAttribute`, `WebAppAttribute<TEntry>`, `WebAppBootstrap<TEntry>`, the configuration attributes and the container. The generator writes the entry type and reports the VGA diagnostics.

The package does not use `WebApplicationFactory`. `WebApplicationFactory` calls the entry point through reflection and reads the app's `deps.json` file, which does not suit a Native AOT test binary **(inference, from the report)**.

A test project that already references `VerificationGlue.Tests` adds this package and a project reference to the app. It needs no other setting. The `global.json` line that `VerificationGlue.Tests` needs is the same.

## Attributes

| Attribute | Placement | Meaning |
|---|---|---|
| `[WebAppEntry(assemblyName)]` | Partial class | Asks the generator to make this class the entry type of the named app assembly. |
| `[WebApp<TEntry>] where TEntry : IWebAppEntry` | Assembly, class, method | The app is the container of these tests. It derives from `ContainerAttribute<WebAppBootstrap<TEntry>>`, so `Scope = Test` (default), `Class` or `Assembly` works as for any container. |
| `[AppSetting(key, value)]` | Assembly, class, method, many | One configuration value for the app. |
| `[AppEnvironment(name)]` | Assembly, class, method | The environment name. The default is `Development`. |
| `[Kestrel]` | Assembly, class, method | The app listens on a free loopback port instead of TestServer. |
| `[WatchAppConfiguration]` | Assembly, class, method | Leaves the configuration file watcher on. |

The placement rules are those of `docs/design/tests.md`. `[AppEnvironment]`, `[Kestrel]` and `[WatchAppConfiguration]` are single-valued, so the narrowest wins. `[AppSetting]` adds up widest first, and a narrower value for the same key replaces a wider one.

The bootstrap reads all of these from `TestContainerContext.Attributes`, which already holds the materialised attributes of the scope. They need no change to `VerificationGlue.Tests`.

## The entry type

`WebAppBootstrap<TEntry>` calls the app's entry point through `TEntry`:

```csharp
public interface IWebAppEntry
{
    static abstract string ApplicationName { get; }
    static abstract int Run(string[] args);
}
```

A generator writes `ApplicationName` and `Run` for each partial class marked `[WebAppEntry]`. The test author does not write them by hand. This follows the recommendation of ipjohnson/VerificationGlue#37.

The result of #37, in the "Entry point shapes" section of the report, is that a generator in the test project can write the entry type for every shape it tried **(verified)**:

- Top-level statements that return `void`, `int`, `Task` and `Task<int>`, an explicit `Main` in a static class, an internal `Program` with and without `InternalsVisibleTo`, and `Host.CreateDefaultBuilder` with a `Startup` class.
- Each shape starts on the JIT and under Native AOT, and the test's `[Mock]` reaches the app.
- The generator switches the compilation to `MetadataImportOptions.All` and looks for a static method named `Main`, `<Main>` or `<Main>$` with an entry point signature. A reference assembly keeps only the PE entry point, which is `<Main>` for an async shape and `<Main>$` or `Main` otherwise.
- The generated accessor names the app's type as a string through `UnsafeAccessorType`, such as `"Program, InternalApp"`. An internal `Program` and a static class need no `InternalsVisibleTo`.
- In the IDE and in the build the generator can see different methods for an async shape. Both methods exist in the app at run time, so either accessor works.

A hand-written entry type is rejected. It fails with CS0122 on an internal `Program` without `InternalsVisibleTo`, it cannot name a static class as an accessor's parameter type, and an analyzer that checked it would read the same private method the generator reads.

The generated file for the example above:

```csharp
partial class ShopApiEntry : global::VerificationGlue.Tests.AspNetCore.IWebAppEntry
{
    public static string ApplicationName => "ShopApi";

    public static int Run(string[] args)
    {
        EntryPoint(null, args);
        return 0;
    }

    [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern void EntryPoint(
        [global::System.Runtime.CompilerServices.UnsafeAccessorType("Program, ShopApi")] object? type,
        string[] args);
}
```

ShopApi's top-level statements return `void`, so `Run` returns 0. For an `int` shape `Run` returns the exit code. For an async shape it calls the `<Main>` method the compiler writes, or blocks on the task that `<Main>$` returns.

### Cost in the IDE

`compilation.WithOptions(...)` creates a new compilation, and the spike did this on every run of the generator. The package's generator does not:

- It takes the app's reference from `MetadataReferencesProvider`, filtered to the assembly that `[WebAppEntry]` names.
- It builds a compilation that holds only that reference, with `MetadataImportOptions.All`, and reads the entry point from it.
- The model it caches holds the shape as plain values: the type name, the method name and the return type.

An edit to the test project then leaves the reference unchanged, and the stage that reads it does not run again **(inference)**. An edit to the app's project changes the reference in the IDE, and the stage runs once.

Phase 1 measures this with the generator benchmark harness, for an edit to the test project and an edit to the app, before the first preview.

## How the app starts

`WebAppBootstrap<TEntry>.BuildAsync` does what the spike's `WebApp.StartAsync` does:

1. It starts a thread, sets the host capture of that thread, and calls `TEntry.Run(args)`.
2. On the `HostBuilding` event of the `Microsoft.Extensions.Hosting` `DiagnosticListener`, it adds the server, a host lifetime that does nothing, the test's overrides, and a logger provider that writes to the test's output. It removes the app's other logger providers.
3. On `HostBuilt` it takes the host.
4. It waits for `IHostApplicationLifetime.ApplicationStarted` and returns the container.
5. `DisposeAsync` disposes the test's `HttpClient` and service scope, calls `StopApplication`, and waits for the entry point to return.

The arguments it passes are `--applicationName=<ApplicationName>`, `--environment=<name>`, `--hostBuilder:reloadConfigOnChange=false` unless `[WatchAppConfiguration]` applies, and one `--<key>=<value>` for each `[AppSetting]`.

`--applicationName` is needed for controllers. Without it, MVC looks for controllers in the test assembly and `GET /clock` returns 404 **(verified)**.

The host capture is an `AsyncLocal` set on the app's thread. The hosting `DiagnosticListener` is created on the thread that builds the host, so each app's capture receives only its own host **(verified)**.

## Parameters

The resolution order of `docs/design/tests.md` applies. The app's container answers step 4 of it:

| Parameter type | Value |
|---|---|
| `HttpClient` | A client for this test. Under TestServer it comes from `TestServer.CreateClient()`. Under `[Kestrel]` it is a client whose `BaseAddress` is the app's address. |
| `IHost` | The app's host. |
| Any other type | A service from a service scope created for this test, or no value if the app does not register it. |

`[Mock]` parameters are registered as overrides before the app starts, so the parameter and the app's service are the same instance.

No type of ours wraps the client or the host. A wrapper would add a name to learn and hide the framework types a test author already knows. A test that needs the TestServer itself takes `IHost` and resolves `IServer`.

## Configuration

A test sets configuration with attributes, which the bootstrap turns into command-line arguments for the entry point. Command-line arguments are the last configuration source of `WebApplication.CreateBuilder` and `Host.CreateDefaultBuilder`, so a test's value replaces the value from `appsettings.json` or an environment variable.

This works when the app passes `args` to its builder, which the ASP.NET Core templates do. For an app that does not, the bootstrap also adds the values as an in-memory source on `HostBuilding`. Code that reads configuration before `Build()` then does not see them **(inference)**. Phase 2 tests both cases, and the guide says so.

`[Kestrel]` binds `http://127.0.0.1:0`. The bootstrap reads the bound address from `IServerAddressesFeature` after the app starts. The spike ran a test on Kestrel on the JIT and under Native AOT **(verified)**.

## Scopes

The app is a container, so it follows the container scopes of `docs/design/tests.md`:

- **Test scope, the default.** Each test starts its own app with its own overrides and its own configuration.
- **Class or assembly scope.** One app starts for the class or the assembly. Each test gets a child with its own `HttpClient` and service scope. The app reads only the class and assembly attributes.

`WebAppBootstrap<TEntry>` does not implement `ISupportsChildOverrides`. A built service provider takes no new registrations, so a test's mock cannot reach an app that other tests share. A class-scoped or assembly-scoped app with `[Mock]` parameters therefore stays build error VGT0008 **(verified in the spike)**.

A configuration attribute on a test method whose app is shared cannot reach the app either. The generator reports it as VGA0003.

## Apps that start at once

Apps start at once when the runner runs tests in parallel. Each app gets its own host and its own overrides, on the JIT and under Native AOT **(verified)**.

Under Native AOT, hosts that build at the same time can fail with a `NullReferenceException` in `ActivatorUtilities.CreateInstance`, called from `GenericWebHostBuilder.UseStartup`. The `ConcurrentBuild` project shows the same failure with no bootstrap. The fault is in the framework **(verified)**.

The bootstrap therefore builds one host at a time when `RuntimeFeature.IsDynamicCodeSupported` is false. Only the build is serial: an app releases the lock on `HostBuilt`, and the hosts still start and serve requests at the same time. With this, 13 of 13 native tests passed in 20 of 20 runs.

On the JIT the bootstrap does not serialise. The JIT showed no failure in 10 runs of `ConcurrentBuild` or 3 runs of the test project, and a serial build would add the first start's 61 to 145 ms to each app that waits behind it. The bootstrap cannot tell an app with a `Startup` class from one without before the entry point runs, so under Native AOT it serialises every app.

## An app that stops before it builds a host

The bootstrap waits for the host or the end of the entry point, whichever comes first. The test fails with the reason and does not hang **(verified)**:

- If the entry point throws, the bootstrap rethrows its exception. The test shows the app's exception and the line of `Program.cs` that threw.
- If the entry point returns, the bootstrap throws `InvalidOperationException`: "The entry point of ShopApi returned 2 before it built a host. What the app wrote to standard error went to the console of the test process."

The same check runs while the bootstrap waits for `ApplicationStarted`.

The message does not include the app's standard error. `Console.SetError` is global to the process, so capturing it would mix the output of apps that start at once **(inference)**. The test's output holds no log line from the app either, because the bootstrap's logger is added on `HostBuilding`.

## Diagnostics

| ID | Severity | Reports |
|---|---|---|
| VGA0001 | Error | `[WebAppEntry]` names an assembly the test project does not reference, or one with no entry point or entry points in more than one type. The message lists what the generator found. |
| VGA0002 | Error | `[WebAppEntry]` is on a class that is not `partial`, is generic or is nested. |
| VGA0003 | Error | `[AppSetting]`, `[AppEnvironment]`, `[Kestrel]` or `[WatchAppConfiguration]` on a test method whose `[WebApp<TEntry>]` has class or assembly scope. |

VGT0008 from `VerificationGlue.Tests` covers a `[Mock]` parameter under a shared app.

The app's log reaches the test's output through the logger provider. The spike's failing test showed the request, the endpoint and the app's own log line under "Standard output" **(verified)**. The bootstrap also writes one line with the app's name and its start time.

## Not in the first cut

- **Fixtures that outlive a test.** A database is the usual case: start it once for the assembly and give each app its connection string. The core has no place for it today:
  - `[Container<T>]` is single-valued, so a test cannot have an assembly container for the database and a test container for the app.
  - A test-scoped container gets a null `TestContainerContext.Parent`.
  - `TestScope` holds no state, and the runner creates the attributes again for each scope, so an `ITestScopeListener` on the assembly scope cannot hand a value to a test scope without a static field.

  The core needs a place for a scope to hold a value that narrower scopes can read. The database itself stays outside the package. A test can set a fixed connection string with `[AppSetting]` in the meantime.
- **A per-test mock under a shared app.** VGT0008 stays.
- **Authentication.** The package has no helper for a test user. A test can add a scheme with a `[Mock]` of the app's own abstraction, or set the header on its `HttpClient`.
- **Capturing the app's standard error.** See above.
- **Apps that are not ASP.NET Core.** A worker service has a host but no `HttpClient`. The entry type and the host capture would work for it, but it is not tested.

## Documentation

This package owns these pages. The rules for producing and checking them are in the north star.

- README and `docs/guide/getting-started-aspnetcore.md`.
- Guides: "Configure the app for a test", "Share an app across a class", "Test an app on a real port", "Run app tests under Native AOT".
- A migration guide from `WebApplicationFactory`, as paired samples that both compile.
- `docs/diagnostics/VGA0001.md` to `VGA0003.md`.
- A generated-code page showing an entry type for each shape.
- The package's `llms.txt`.

## Delivery

Phases 1 and 2 ship as previews, and together they are the first public release. The release map is in the north star.

### Phase 0: spikes

When done, we know that an app can be the container of a test on the JIT and under Native AOT, and which entry points a generator can call.

Needs: `tests.md` Phase 2.

- [x] An app per test through `ITestContainerBootstrap`, with `HttpClient`, `[Mock]` parameters and the app's services, on TestServer and Kestrel, on the JIT and under Native AOT. `docs/research/aspnet-report.md`, "What it showed".
- [x] Entry point shapes and what a generator can read (ipjohnson/VerificationGlue#37). The report's "Entry point shapes".
- [x] An app that stops before it builds a host, and apps that start at once. The report's sections of the same names.

### Phase 1: an app per test

When done, a user references an app's project, writes `[WebAppEntry]` and `[WebApp<TEntry>]`, and writes tests that take `HttpClient`, `IHost`, the app's services and `[Mock]` parameters. Each test starts the app on TestServer and stops it when the test ends. The app's log is in the test's output, and the test project publishes with Native AOT.

Needs: Phase 0, and `tests.md` Phase 2, which holds containers, overrides and VGT0008.

- [ ] The projects `src/VerificationGlue.Tests.AspNetCore` and `src/VerificationGlue.Tests.AspNetCore.Generator`, with their specs, and the package in `eng/pack-and-consume.sh`.
- [ ] The entry generator, built on CSharpAuthor, for every shape in the report, with VGA0001 and VGA0002.
- [ ] The measurement of the generator in the IDE pipeline described under "Cost in the IDE", with the result recorded in this document.
- [x] `WebAppBootstrap<TEntry>` on TestServer with test scope, the watcher off, the logger provider, and the parameters above. `test/VerificationGlue.Tests.AspNetCore.Specs`, with a hand-written entry type until the generator ships.
- [ ] The serial host build under Native AOT, and a test that starts many apps at once.
- [x] The failures of an app that throws or returns before it builds a host, with the exit code in the message.
- [ ] A Native AOT test project in CI that runs app tests with no warnings.
- [ ] Pages: README, `docs/guide/getting-started-aspnetcore.md`, the guide "Run app tests under Native AOT", `docs/diagnostics/VGA0001.md` and `VGA0002.md`, the generated-code page, and the package's `llms.txt`.

### Phase 2: configuration and shared apps

When done, a user sets configuration values, the environment name and Kestrel on a test, a class or an assembly, and shares one app across a class or an assembly.

Needs: Phase 1.

- [ ] `[AppSetting]`, `[AppEnvironment]`, `[Kestrel]` and `[WatchAppConfiguration]`, with the placement rules above.
- [ ] Tests for an app that passes `args` to its builder and one that does not.
- [ ] Class and assembly scope, with a child per test, and VGA0003.
- [ ] Pages: the guides "Configure the app for a test", "Share an app across a class" and "Test an app on a real port", `docs/diagnostics/VGA0003.md`, and the migration guide from `WebApplicationFactory`.

## Risks and open questions

- The bootstrap depends on the `HostBuilding` and `HostBuilt` events of the hosting `DiagnosticListener`. `WebApplicationFactory` depends on them too, so they are unlikely to change, but they are not a documented API.
- The Native AOT fault in `UseStartup` is in ASP.NET Core. If it is fixed, the serial build can be removed. It should be reported to the ASP.NET Core repository with the `ConcurrentBuild` project.
- The spike's app has three services. An app with EF Core, hosted services or a slow start was not measured, so an app per test may be slower for such apps. A class-scoped app is the answer when it is.
- An app that reads configuration before `Build()` and does not pass `args` to its builder does not see a test's `[AppSetting]` values in that code.
- The package follows ASP.NET Core 10. A new ASP.NET Core major may need a new major of the package, which the shared version number then forces on all four packages.
