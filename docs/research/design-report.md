# VerificationGlue design research

Date: 2026-11-16. Status: research record. The plan now lives in `docs/north-star.md` and the three documents under `docs/design/`. This report is kept for its evidence and is not updated.

Both halves of the idea work. A test framework built as a small adapter on Microsoft.Testing.Platform (MTP) compiled to Native AOT and ran tests in 32 ms. A source generator turned `Verify(list.Count > 3)` into a check that reports every sub-expression value, evaluates each one exactly once, and also passed a Native AOT publish. The two pieces must live in one source generator, because the runner is the only caller of a test method and has to call the rewritten copy.

Labels: **[V]** verified by reading code or docs, or by running a spike. **[I]** inference.

## 1. Pain points in the existing xunit extensions

SharpCheck contains only a README, so it has nothing to learn from. The evidence comes from DependencyModules (DM), Hardened.Framework and SimpleFixture. All rows are [V].

| Finding | File | Complaint |
|---|---|---|
| xUnit disposes a test case only after the whole assembly has run. DM had to implement `ISelfExecutingXunitTestCase` and wrap the run in `try/finally { await DisposeProviders(); }`. | `DependencyModules/src/DependencyModules.xUnit/Impl/ModuleTestCase.cs` | scope disposal |
| Containers are built in `CreateTests()` before any test runs. `TestContext.Current.Test` is null there, so output written during container build is lost. Rows of one data-driven test are disposed together. | `.../Impl/XunitCurrentTestProvider.cs` | scope creation |
| xunit.v3 4.0 changed `ISelfExecutingXunitTestCase.Run`. DM ships `DependencyModules.xUnit` and `DependencyModules.xUnit4` from one source tree under `#if XUNIT_V4`. | both `.csproj` files | breaking changes, package count |
| The 13-parameter `XunitTestCase` constructor shifts between versions, so every base call uses named arguments. | `ModuleTestCase.cs` | not meant to be extended |
| Name and ID must come from `TestIntrospectionHelper.GetTestCaseDetails`. A colliding ID makes xUnit drop the test silently. | `Impl/ModuleTestDiscoverer.cs` | naming |
| Custom discovery must back-fill `ITypeAwareDataAttribute.MemberType`. Otherwise `[MemberData]` yields zero rows and the run passes. | `ModuleTestCase.SupplyReflectedType` | extensibility |
| `[CallerFilePath]` cannot follow `params Type[]`, so the multi-module attribute overload loses source navigation. | `Attributes/ModuleTestAttribute.cs` | extensibility |
| NUnit's pipeline is synchronous, which forces `ResolveArgumentsAsync(...).GetAwaiter().GetResult()`. | `src/DependencyModules.NUnit/Impl/ModuleTestCommand.cs` | scopes, DI |
| Attribute lookup is runtime reflection, modules come from `Activator.CreateInstance`, and extension interfaces take `MethodInfo` and `ParameterInfo`. | `src/DependencyModules.Testing/Impl/AttributeUtility.cs` | performance, AOT |
| A test project references `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `xunit.v3`, `coverlet.collector` and the DM package. | `integ-tests/SutProject.Tests/SutProject.Tests.csproj` | package count |
| Hardened adds warning `HRDT001` because `[ModuleTest]` lives in one of three runner packages. | `Hardened.Framework/src/Shared/Hardened.Shared.Testing/Package/Hardened.Shared.Testing.targets` | package count |
| SimpleFixture pins `xunit.v3.extensibility.core` to `1.*`, while DM needs 3.x and 4.x. `AutoDataAttribute.GetData` is reflection-only. | `SimpleFixture/src/SimpleFixture.xUnit/SimpleFixture.xUnit.csproj` | breaking changes, AOT |

## 2. MTP adapter findings

All [V] unless marked.

- Stable MTP is 2.5.3 (2026-11-11). 3.0.0 is in preview, drops the net8 and net9 assets, and removes `CancelledTestNodeStateProperty`. 2.0 was also a breaking major.
- A framework implements `ITestFramework` (`CreateTestSessionAsync`, `ExecuteRequestAsync`, `CloseTestSessionAsync`) and `IDataProducer`. It handles `DiscoverTestExecutionRequest` and `RunTestExecutionRequest`, publishes `TestNodeUpdateMessage`, and registers with `builder.RegisterTestFramework(...)`.
- MTP sends one request. The framework owns scheduling and parallelism.
- `Microsoft.Testing.Platform` has no package dependencies and is `IsAotCompatible`. `Microsoft.Testing.Platform.MSBuild` generates the entry point and calls each `TestingPlatformBuilderHook` item.
- With stable UIDs and `TestNodeUidListFilter`, these come free: `--list-tests`, `--filter-uid`, `--timeout`, `--results-directory`, `--minimum-expected-tests`, IDE server mode and the Retry extension. TRX needs `ITrxReportCapability`. HangDump, CrashDump and coverage are separate packages.
- `--treenode-filter` and graceful stop are marked experimental (`TPEXP`).
- On the .NET 10 SDK, `dotnet test` needs `"test": {"runner": "Microsoft.Testing.Platform"}` in `global.json`.
- MSTest's generator registers tests from a `[ModuleInitializer]` but still falls back to reflection and reports no source locations.

Spike: a hand-written version of the generated code below builds against MTP 2.5.3 alone. `--list-tests` and `--filter-uid` work. `PublishAot` gives no trim warnings, a 10.9 MB binary and a 2-test run in 32 ms.

## 3. Assertion rewrite findings

All [V], run on .NET SDK 10.0.403.

| Mechanism | Sub-expression values | Result |
|---|---|---|
| `[CallerArgumentExpression]` only | No, source text only | Works. Used as the fallback. |
| Interceptor per `Verify` call | No. It receives the evaluated `bool`, but it can specialise the message by syntax shape and embed file and line. | Works. |
| Expression trees | Yes | `Compile()` runs under AOT, but ten C# forms do not compile in a tree: `is` patterns, `?.`, async lambdas, tuple `==`, list patterns, switch expressions, span locals, `out var`, collection expressions. |
| Clone of the `[Verify]` method with each `Verify` statement rewritten, plus interceptors on direct callers | Yes, at every level | Works. Recommended. |

Clone results:

- `Verify(Next() + Next() == 10 || Next() == 0)` called `Next()` three times and printed each value.
- The right side of `&&` prints `<not evaluated>` when the left side is false.
- `Verify(d.TryGetValue("a", out var v)); Verify(v == 2);` compiles, because each rewrite is an `if` statement.
- `Verify(await x == 42)` works, so no async `Verify` overload is needed.
- `#line` directives keep stack traces on the original line.
- Native AOT publish: no warnings, 1.35 MB, same messages. AOT stack frames have no file or line, so the compile-time location in the message is needed.
- An error inside a `[Verify]` body is reported twice.
- 2000 `[Verify]` methods with 6000 calls: build 2.5 s without the generator, 6.6 s with it. The spike generator was not incremental and the machine was under load, so this figure is rough. It works out to about 0.7 ms per `Verify` call.

Cold builds matter as much as incremental ones. Agents and CI clone a repo, build once, make a small change and push, so most builds start with an empty generator cache. The target scale is about 10,000 tests averaging 10 `Verify` calls each, which is 100,000 calls across a few assemblies. At the spike's rate that adds roughly 70 s of total build time. Cold-build cost is therefore a design requirement with its own benchmark, not only incremental caching. The interceptor-only tier is cheaper, because it does not compile each method body a second time.
- `Verify.Throws<T>` cannot coexist with `using static` `Verify(...)` (CS0119), so exception helpers need their own names.

Generated shape (names shortened):

```csharp
// Verify(list.Count > 3);
if (!((v1 = (v0 = list).Count) > 3))
    throw Fail("list.Count > 3", file, 25).With("list.Count", v1).With("list", v0).Build();
```

Spike output:

```
Verify failed: list.Count > 3
  list.Count → 2
  list       → [1, 2]
  at Tests.cs:25

Verify failed: s.StartsWith("x") && s.Length < 10
  s.StartsWith("x") → false
  s                 → "hello"
  s.Length < 10     → <not evaluated>

Verify failed: x is { Name: "bob", Age: > 18 }
  x      → Person { Name = bob, Age = 12 }
  x.Name → "bob"
  x.Age  → 12   (does not match > 18)
```

Swift Testing's `#expect` is the closest prior art. It decomposes only the top-level operation. The spike goes deeper. pytest's opt-in rewrite of test modules and registered helpers matches the `[Verify]` opt-in. Shouldly and FluentAssertions read source files at run time, which fails under AOT.

## 4. Proposed design

### Packages

The user installs one package, `VerificationGlue`. It holds the runtime, the MTP adapter, the generator and `build/` props that set `OutputType=Exe`, `IsTestingPlatformApplication`, `InterceptorsNamespaces` and the `TestingPlatformBuilderHook`. It depends on `Microsoft.Testing.Platform`, `Microsoft.Testing.Platform.MSBuild` and `TrxReport.Abstractions`. The core uses only `System.IServiceProvider`. Container adapters ship separately, for example `DependencyModules.VerificationGlue` in the DM repo. [I]

### User code

```csharp
using static VerifyGlue.VerifySyntax;

[assembly: Container<ModuleBootstrap>]

[SutModule]
public partial class OrderTests
{
    [VerifyTest]
    [Row(2, 10)]
    public async Task Totals(int qty, int price, [Mock] IClock clock, OrderService sut)
    {
        var order = await sut.CreateAsync(qty, price);
        Verify(order.Total == 20);
        Check(order.Lines.Count == 1);
    }
}
```

`SutModule` is the system under test's DependencyModules module, declared as `[DependencyModule] public partial class SutModule` (see `DependencyModules/integ-tests/SutProject/SutModule.cs`). DM lets a module class be used as an attribute. Placing it on a test class tells `ModuleBootstrap` which modules to load into that class's test containers, so the container holds the same registrations the application uses. A class can list several modules, and a module at assembly level applies to every test. Without it the container would only hold what the test registers itself.

### Generated registration

```csharp
file static class OrderTests_Generated
{
    static readonly Attribute[] s_attrs = [new SutModuleAttribute()];
    static readonly TestParameter[] s_params = [ /* name, type, position, attributes */ ];

    [ModuleInitializer]
    internal static void Register() => TestRegistry.Add(new TestDescriptor(
        Uid: "MyApp.OrderTests.Totals(int,int,IClock,OrderService)#0",
        DisplayName: static ctx => DefaultNameFormatter.Format(ctx),
        FilePath: "OrderTests.cs", Line: 8,
        BuildContainer: static ctx => ModuleBootstrap.BuildAsync(ctx),
        Attributes: s_attrs, Parameters: s_params,
        Invoke: static async ctx =>
        {
            var instance = new OrderTests();
            await instance.Totals__Verified(2, 10,
                ctx.Resolve<IClock>(s_params[2]), ctx.Resolve<OrderService>(s_params[3]));
        }));
}
```

The runner research proposed this registration and the assertion research proposed the `__Verified` clone. Calling the clone from `Invoke` joins the two designs, and it is why both must be one generator. [I]

### Scopes

There are three levels: assembly, class and test. Each data row and each retry attempt gets its own test scope. A scope is created when its first selected test starts, so tests removed by a filter build nothing. It is disposed after its last test, in reverse creation order, with `await` inside `try/finally`. The per-test order is: build the container, enter middleware, construct the class, resolve parameters, invoke, dispose the instance, leave middleware, dispose the container, publish the result. A disposal failure fails the test. [I]

### Container contract

```csharp
public interface ITestContainerBootstrap
{
    static abstract ValueTask<ITestContainer> BuildAsync(TestContainerContext context);
}

public interface ITestContainer : IAsyncDisposable
{
    IServiceProvider Services { get; }
    bool TryResolve(TestParameter parameter, out object? value);
    ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context);
}
```

`TestContainerContext` carries the UID, the ordered attributes, the parameters, the parent container and a cancellation token. The DM adapter collects `IDependencyModuleProvider` attributes from the context, loads them into a `ServiceCollection`, applies `[Mock]` and other parameter attributes, and builds the provider. `[Container<T>(Scope = Class)]` shares one container per class and gives each test a child. [I]

Generic attributes (`[Container<T>]`, `[NameFormatter<T>]`, `[Use<TMiddleware>]`, `[Scheduler<T>]`) let the generator emit a direct static call, so nothing is created through `Activator`. An adapter can derive its own attribute, for example `ModuleTestAttribute : ContainerAttribute<ModuleBootstrap>`, which compiled in the spike. Single-valued attributes resolve method first, then class, then assembly. Multi-valued attributes add up, widest first, which is DM's current rule.

### Other pieces

- Parameter resolution order: row value, parameter-level provider, method/class/assembly providers, container, built-ins (`CancellationToken`, `TestContext`), default value. Anything else fails the test and names the parameter.
- The UID is computed at compile time from the symbol and row index. It never depends on the display name. A duplicate UID is a build error. `ITestNameFormatter` produces the display name.
- `[Row(...)]` emits one descriptor per row with compile-time type checks. `[RowSource(nameof(X))]` emits a typed call. Zero rows fails.
- Scheduling is per group, not one assembly-wide parallelism level. See below.

### Scheduling

Each test belongs to a schedule group. The generator assigns the group at compile time from attributes at method, class or assembly level, narrowest first. Each group has its own concurrency limit, and groups run side by side. [I]

```csharp
[assembly: ScheduleGroup("default", MaxParallel = 0)]        // 0 = processor count

[ScheduleGroup("database", MaxParallel = 2)]                 // shares two connections
public partial class OrderRepositoryTests { ... }

[ScheduleGroup("ui", MaxParallel = 1)]                        // one browser at a time
public partial class CheckoutPageTests { ... }

[Resource("payments-sandbox")]                                // never overlaps another test holding this key
[VerifyTest] public async Task Refund(...) { ... }

[RunAfter("database")]                                        // group waits until "database" is done
public partial class ReportTests { ... }
```

The scheduler receives every selected test with its group, resource keys and ordering constraints already resolved. The default scheduler runs one queue per group with that group's limit, holds a test until its resource keys are free, and starts a group after the groups it waits on. Scope creation follows the schedule: a class container is built when the first test of that class starts, wherever that test's group runs. `ITestScheduler` replaces the whole policy, for example to run slow tests first, to shard by machine, or to read limits from configuration. An open question is whether one test may belong to more than one group.

### Extension points

```csharp
interface ITestParameterProvider { bool TryProvide(TestContext c, TestParameter p, out object? value); }
interface ITestNameFormatter    { static abstract string Format(in TestNameContext c); }
interface ITestDataSource       { IAsyncEnumerable<TestRow> GetRowsAsync(TestDataContext c); }
interface ITestMiddleware       { ValueTask InvokeAsync(TestContext c, Func<ValueTask> next); }
interface ITestScopeListener    { ValueTask OnCreatedAsync(TestScope s); ValueTask OnDisposingAsync(TestScope s); }
interface ITestScheduler        { ValueTask RunAsync(IReadOnlyList<ScheduledTest> tests, Func<ScheduledTest, ValueTask> run, CancellationToken ct); }
interface ITestSessionSetup     { static abstract void Configure(ITestApplicationBuilder b, string[] args); }
```

### Assertion surface

```csharp
Verify(order.Total == 40);                               // hard, [DoesNotReturnIf(false)]
Verify(order.Total == 40, $"order {order.Id}");          // lazy message, works without the generator
Verify(order.Total == 40).Because("discount applies");   // needs the generator
Check(order.Lines.Count == 2);                           // soft, test continues
var ex = Throws<InvalidOperationException>(() => cart.Checkout());
var ex2 = await ThrowsAsync<TimeoutException>(() => client.GetAsync(url));
Verify(actual == Approx(3.14, 0.001));
await Eventually(() => queue.Count == 3, TimeSpan.FromSeconds(2));
using (VerifyScope.Begin()) { Check(a); Check(b); }      // soft checks outside a test
```

`.WithMessage(...)` and `.Because(...)` only work when the generator rewrites the chain. An analyzer reports an error when either is used in a method without `[Verify]` or `[VerifyTest]`, so a message can never be lost silently. The two-argument form works anywhere. Interpolated message holes are evaluated only on failure [V]. Strings, `SequenceEqual`, `Contains` and `ContainsKey` get specialised explanations because the generator knows the static types, so no extra assertion methods are needed. A `[VerifyFormatter] static string Format(Money m)` method is bound by static type at each capture site, with no registry. A method returning a `VerifyOutcome` struct adds its own explanation text. [I]

There are three tiers, all in the one package:

1. No generator: the message is the source text from `[CallerArgumentExpression]`.
2. Any `Verify` call: an interceptor adds a shape-specific message and the file and line.
3. `[Verify]` and `[VerifyTest]` methods in partial types: full sub-expression values.

## 5. Use cases unlocked

- Services as test method and constructor parameters, with no fixture classes.
- `[Mock] IFoo` registered before the container is built, so the system under test gets the same mock. Each test or row gets its own container, disposed when that test ends.
- Async setup and disposal with no sync-over-async.
- Native AOT test runs, including tests of AOT-only behaviour. Startup does no reflection scan.
- UIDs that survive display-name changes, so Retry and IDE history keep working.
- One adapter package per container library, not one per test framework major version.
- Compile-time errors for row type mismatches and duplicate test IDs.
- Test output available while the container is built.
- `[Verify]` guard clauses in production code that report values with no hand-written message. Failures on parameters could map to `ArgumentException`.
- `[Verify]` helpers called from xunit or NUnit tests get full values. The test bodies themselves only get tier 2, because those frameworks call test methods through reflection.

## 6. Risks and open questions

Decided:

- **Target framework.** net10 only, so VerificationGlue can follow MTP 3.
- **MTP versions.** Each MTP major gets a VerificationGlue major. The previous major stays on a release branch that receives fixes, so a project can stay on an older MTP while it migrates.
- **Analyzers.** `.WithMessage` and `.Because` outside a `[Verify]` method are build errors.
- **Hard and soft assertions.** `Verify(cond)` throws at once. `Check(cond)` records a failure with its values and lets the test continue. The test then fails at the end with every recorded failure listed.

Open:

1. **Partial test classes.** Full values need `partial` on the test class and every enclosing type. The plan is an analyzer with a code fix. Without `partial`, a test still gets tier 2 messages.
2. **`Note`.** A way to inspect state without failing, such as `Note(order.Total)`, would write the expression text and its value to the test output through the same generator capture, as a structured log line. It is not yet decided whether to add it.
3. **DM interfaces.** DM's `ITestMethodContext.Method` and `ITestParameterValueProvider` take `MethodInfo` and `ParameterInfo`. The adapter needs reflection-free versions, or `[Mock]` and `[TestExport]` have to be ported.
4. **Which attributes the generator materialises.** It could take every accessible attribute that is not from `System`, or only those implementing an opt-in interface.

To research:

- **Reflection-free mocking.** Moq, NSubstitute and FakeItEasy, which DM supports today through `src/DependencyModules.Moq` and its siblings, build proxies with Reflection.Emit and do not work under Native AOT. DependencyModules already has an `InterceptorSourceGenerator` that emits a wrapper class implementing a service's interface and routes every call through `IInterceptor` (`src/DependencyModules.Runtime/Attributes/InterceptAttribute.cs`). A generated mock is that same wrapper with no inner target and a recording, stubbing interceptor. The generator would emit one for each interface type used with `[Mock]`. The research should check how much of DM's generator can be shared, how the setup syntax looks without expression trees, and what existing source-generated mocking libraries such as Rocks already do.

Technical risks:

- MTP has shipped a breaking major about once a year. Keep to the narrow surface above and avoid `TPEXP` APIs. Rich filtering then needs a framework-owned option. [V for the facts, I for the mitigation]
- `dotnet test` on the .NET 10 SDK needs `"test": {"runner": "Microsoft.Testing.Platform"}` in `global.json`. [V] A package cannot set this, because the `dotnet` CLI reads `global.json` before it evaluates any project or restores any package. [I] The package can detect the missing setting and fail the build with a message that gives the exact line to add, as Hardened does with `HRDT001`. A `dotnet new` template can create the file. `dotnet run` on the test project works without it, because the test project is an executable.
- Debugging the clone: `#line` mapping is verified for stack traces only. Breakpoints, stepping and Hot Reload are untested. Stack frames show `Name__Verified`. This is the first spike in the next steps.
- Errors inside `[Verify]` bodies are reported twice. A likely fix is to skip the clone when the original body has errors.
- Virtual methods: a redirect binds to the static target and would skip overrides. Redirect only non-virtual methods. [I]
- Ref structs, pointers, `dynamic`, anonymous types and `ref`/`out` arguments were not captured in the spike. They should fall back to text and the analyzer should say so.
- Each `[Verify]` body compiles twice. The generator must be incremental and fast on a cold build.
- F# and VB get no generator.
- IDE support depends on MTP server mode. It is not re-checked for 2026.

## 7. Next steps

1. Debugging spike. Set breakpoints, step, and use Hot Reload in a `[Verify]` method in Visual Studio, Rider and VS Code. Record what works with `#line` mapping and what does not. The answer decides whether the clone approach is the default or an opt-in.
2. Cold-build benchmark. Generate 10,000 tests with 10 `Verify` calls each across three assemblies. Measure a clean build with the interceptor-only tier and with the clone tier.
3. Reflection-free mocking research, starting from DM's `InterceptorSourceGenerator`.
4. Decide the open items in section 6.

## Spike locations

These are temporary directories on this machine and are not committed.

- Runner spike: `/var/folders/0p/ht4ftmrs7w10___nk6pwwyw80000gn/T/tmp.n8poyxuGaD/Spike`
- Assertion spike: `/tmp/vg-spike.fOpufB` (`Gen/VerifyGenerator.cs`, `Lib/VerifyGlue.cs`, `App/Tests.cs`)
