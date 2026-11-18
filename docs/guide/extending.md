# Extending VerificationGlue.Tests

At the end of this page you can write a container bootstrap, a parameter provider, middleware, a scope listener, a data source, a name formatter and a scheduler for `VerificationGlue.Tests`.

Every extension point is a public interface in the `VerificationGlue.Tests` namespace. The runner finds them through the test's attributes, which the generator creates without reflection, so extensions work under Native AOT. A breaking change to any of these interfaces needs a major version.

## Containers

A container supplies test parameters and the services they need. It has two parts:

- `ITestContainerBootstrap` has one static method, `BuildAsync`, which builds a container from a `TestContainerContext`. `[Container<T>]` names the bootstrap.
- `ITestContainer` exposes `Services`, supplies a parameter in `TryResolve`, creates a child for one test in `CreateChildAsync`, and is disposed with `DisposeAsync`.

`TestContainerContext` holds the scope that will own the container, the scope's attributes, the test's constructor and method parameters, the overrides, the parent container, the test's output and a cancellation token.

This container wraps a `Microsoft.Extensions.DependencyInjection` service provider:

<!-- snippet: tests-service-provider-container -->
<a id='snippet-tests-service-provider-container'></a>
```cs
/// <summary>An <see cref="ITestContainer"/> over a service provider. A child is a scope, so it adds no registrations.</summary>
public sealed class ServiceProviderContainer(IServiceProvider services, IAsyncDisposable? owner = null) : ITestContainer
{
    public IServiceProvider Services => services;

    /// <summary>
    /// Registers each override as an instance, so the container returns it and does not dispose it.
    /// A keyed override is registered under its key.
    /// </summary>
    public static void AddOverrides(IServiceCollection collection, IReadOnlyList<TestOverride> overrides)
    {
        foreach (var entry in overrides)
        {
            if (entry.Key is null)
            {
                collection.AddSingleton(entry.ServiceType, entry.Instance);
            }
            else
            {
                collection.AddKeyedSingleton(entry.ServiceType, entry.Key, entry.Instance);
            }
        }
    }

    public bool TryResolve(TestParameter parameter, out object? value)
    {
        value = parameter.ServiceKey is null
            ? services.GetService(parameter.Type)
            : (services as IKeyedServiceProvider)?.GetKeyedService(parameter.Type, parameter.ServiceKey);
        return value is not null;
    }

    public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context)
    {
        var scope = services.CreateAsyncScope();
        return new(new ServiceProviderContainer(scope.ServiceProvider, scope));
    }

    public ValueTask DisposeAsync() => (owner ?? services as IAsyncDisposable)?.DisposeAsync() ?? default;
}
```
<sup><a href='/samples/TestsContainers/Bootstraps.cs#L22-L63' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-service-provider-container' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Follow these rules in a container:

- Return each override's instance for its `ServiceType` and `Key`. An override with a key goes in as a keyed service.
- Do not dispose an override. The test that added it owns it.
- Dispose every instance the container creates.

### Children of a shared container

Under `Scope = ContainerScope.Class` or `Scope = ContainerScope.Assembly`, the runner calls `CreateChildAsync` on the shared container for each test. When the child must return the test's overrides, the bootstrap implements `ISupportsChildOverrides`, and the child applies `context.Overrides` itself. [Containers and scopes](containers-and-scopes.md#overrides-under-a-shared-container) shows `SharedStoreBootstrap`, which does this with `Microsoft.Extensions.DependencyInjection`.

A bootstrap without `ISupportsChildOverrides` never receives overrides in `CreateChildAsync`. A test that would need them fails with [VGT0008](../diagnostics/VGT0008.md).

## Parameter providers

An attribute that implements `ITestParameterProvider` supplies parameters. On a parameter it supplies that parameter. On a method, class or assembly it is asked for every parameter of those tests, narrowest first. [Parameters and where they come from](test-parameters.md) gives the full order.

<!-- snippet: tests-parameter-provider -->
<a id='snippet-tests-parameter-provider'></a>
```cs
/// <summary>Supplies the string parameters of the tests, or of the parameter, it is placed on.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter)]
public sealed class SettingAttribute(string value) : Attribute, ITestParameterProvider
{
    public bool TryProvide(TestContext context, TestParameter parameter, out object? provided)
    {
        provided = parameter.Type == typeof(string) ? value : null;
        return provided is not null;
    }
}
```
<sup><a href='/samples/TestsContainers/ParameterTests.cs#L7-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-parameter-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Middleware

`ITestMiddleware` runs around each test, after the container is built. Its `next` creates the test class, resolves the parameters, runs the test and disposes an instance the runner created. `[Use<T>]` adds it, and a new instance runs for each test:

<!-- snippet: tests-middleware -->
<a id='snippet-tests-middleware'></a>
```cs
public sealed class LogMiddleware : ITestMiddleware
{
    public async ValueTask InvokeAsync(TestContext context, Func<ValueTask> next)
    {
        Steps.Write("middleware entered");
        try
        {
            await next();
        }
        finally
        {
            Steps.Write("middleware left");
        }
    }
}
```
<sup><a href='/samples/TestsLifecycle/LifecycleTests.cs#L55-L71' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-middleware' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`[Use<T>]` can go on the assembly, a class or a method, more than once. Middleware from the assembly runs outside middleware from the class, which runs outside middleware from the method. An exception that `next` throws reaches the middleware, and the runner reports it whether or not the middleware catches it. An exception the middleware throws on leaving is reported after it.

## Scope listeners

An attribute that implements `ITestScopeListener` is told when each scope it applies to is created and before it is disposed. An assembly attribute hears about the assembly scope, each class scope and each test scope. A class attribute hears about its class scope and its test scopes.

<!-- snippet: tests-scope-listener -->
<a id='snippet-tests-scope-listener'></a>
```cs
/// <summary>Writes a step when each assembly, class and test scope is created and before it is disposed.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class LogScopesAttribute : Attribute, ITestScopeListener
{
    public ValueTask OnCreatedAsync(TestScope scope)
    {
        Steps.Write($"{Name(scope)} created");
        return default;
    }

    public ValueTask OnDisposingAsync(TestScope scope)
    {
        Steps.Write($"{Name(scope)} disposing");
        return default;
    }

    private static string Name(TestScope scope) => scope.Kind switch
    {
        TestScopeKind.Assembly => "assembly scope",
        TestScopeKind.Class => "class scope " + scope.Uid.Split('.')[^1],
        _ => "test scope " + scope.Uid[..scope.Uid.IndexOf('(')].Split('.')[^1],
    };
}
```
<sup><a href='/samples/TestsLifecycle/LifecycleTests.cs#L29-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-scope-listener' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`OnCreatedAsync` for a test scope runs before the runner creates mocks and builds the container. A listener can add an override there with `TestScope.AddOverride`. After the container is built, `AddOverride` throws.

`OnDisposingAsync` runs before the scope's containers are disposed. It runs for each listener whose `OnCreatedAsync` returned, in reverse order, even when the test failed.

## Data sources

An attribute that implements `ITestDataSource` gives the rows of the test methods it is placed on. The runner calls `GetRowsAsync` at discovery, in every process that runs tests, and each `TestRow` it returns is its own test. `TestDataContext` passes the method's `Uid`, `TestClass`, `MethodName` and `Parameters`, and a `CancellationToken` that is cancelled when the session is cancelled.

This source gives one row per stored customer. Each row's `Key` is the customer's name:

<!-- snippet: tests-data-source -->
<a id='snippet-tests-data-source'></a>
```cs
/// <summary>Gives one row per stored customer, keyed by the customer's name.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CustomersAttribute : Attribute, ITestDataSource
{
    public async IAsyncEnumerable<TestRow> GetRowsAsync(TestDataContext context)
    {
        await Task.Yield();
        foreach (var customer in CustomerStore.All())
        {
            yield return new TestRow(customer) { Key = customer.Name };
        }
    }
}
```
<sup><a href='/samples/TestsDataRows/PricingTests.cs#L64-L78' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-data-source' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The `Values` of a `TestRow` go to the method's first parameters, in order. The other parameters resolve as usual. The runner checks each value against its parameter type at discovery, because the attribute's values are not known at compile time.

A row's test ID must select the same data in a fresh process, such as a retry. Give each row a `Key` that no other row of the test has, and that stays the same when the source adds, removes or reorders rows. A row whose values are all primitives, strings or enums can leave `Key` unset, and takes its ID from its values. When the source gives the same rows in the same order in every process, it can declare that instead by returning `true` from `DeterministicReplay`. Its unkeyed rows then take their IDs from their positions. [Data rows](data-rows.md#test-ids-of-runtime-rows) gives the full rules.

When `GetRowsAsync` throws, gives no rows, gives two rows the same key, or gives a row that fits none of these rules, the test method fails discovery with the reason and runs no row. [Data rows](data-rows.md#a-source-that-fails) lists the cases.

A method can have several data source attributes and `[RowSource]` members. Their rows are read in that order: the `[RowSource]` members first, then the attributes.

## Name formatters

An `ITestNameFormatter` writes the display name that a test explorer and the TRX report show. It has one static method, `Format`, which takes a `TestNameContext`:

- `TypeName` and `MethodName` name the test method.
- `Parameters` holds the method's parameters.
- `Arguments` holds the row values, which go to the first parameters in order. It is empty for a test without [data rows](data-rows.md).

This formatter writes the row values without the parameter names. `DefaultNameFormatter.FormatValue` writes each value as the default name does:

<!-- snippet: tests-name-formatter -->
<a id='snippet-tests-name-formatter'></a>
```cs
/// <summary>Writes the method name and the row values, without the parameter names.</summary>
public sealed class ValuesOnlyNameFormatter : ITestNameFormatter
{
    public static string Format(in TestNameContext context) =>
        context.Arguments.Count == 0
            ? context.MethodName
            : context.MethodName + " " + string.Join(" / ", context.Arguments.Select(DefaultNameFormatter.FormatValue));
}
```
<sup><a href='/samples/TestsDataRows/PricingTests.cs#L80-L89' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-name-formatter' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`[NameFormatter<T>]` names the formatter. It can go on the assembly, a class or a method, and the narrowest one applies:

<!-- snippet: tests-name-formatter-usage -->
<a id='snippet-tests-name-formatter-usage'></a>
```cs
[NameFormatter<ValuesOnlyNameFormatter>]
public partial class ShippingTests
{
    [VerifyTest]
    [Row(1, "standard")]
    [Row(5, "express")]
    public void Ships(int days, string speed) => Verify(days > 0 && speed.Length > 0);
}
```
<sup><a href='/samples/TestsDataRows/PricingTests.cs#L91-L100' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-name-formatter-usage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The two rows show as `Ships 1 / "standard"` and `Ships 5 / "express"`. Without the attribute, `DefaultNameFormatter` writes `Ships(days: 1, speed: "standard")`.

The generator calls `Format` directly, so a formatter works under Native AOT. The runner calls it when it first needs the name. When `Format` throws, the test gets the default name. The display name does not change the test ID, so a new formatter keeps every filter and retry working.

## Schedulers

A scheduler decides when each selected test runs. `DefaultTestScheduler` runs the rules in [Scheduling groups and resources](scheduling-groups-and-resources.md). To replace it, implement `ITestScheduler` and name the type on the assembly. The type needs a public parameterless constructor:

<!-- snippet: tests-scheduler-attribute -->
<a id='snippet-tests-scheduler-attribute'></a>
```cs
[assembly: Scheduler<TestsScheduling.ShardScheduler>]
```
<sup><a href='/samples/TestsScheduling/SchedulingTests.cs#L8-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-scheduler-attribute' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The runner calls `RunAsync` once per run with these arguments:

- `tests` is every selected test, ordered by UID. Each `ScheduledTest` has its `Uid`, `DisplayName`, `TestClass`, `Group`, the group's `MaxParallel` and `RunAfter`, and its `Resources`. `MaxParallel` is at least 1, and every test of a group has the same `MaxParallel` and `RunAfter`.
- `run` runs one test through its scopes and reports its result. Call it at most once for each test. A failed test does not make it throw, but a second call for the same test throws `InvalidOperationException`.
- `cancellationToken` is cancelled when the run is cancelled. Start no more tests after that.

Return after every call to `run` has finished, because the runner disposes the class and assembly scopes when `RunAsync` returns. A test the scheduler never runs is reported as skipped. When `RunAsync` throws, each test it did not run fails with its message.

This scheduler splits the tests between machines. It picks its share, and passes it to `DefaultTestScheduler`, so groups, resources and `[RunAfter]` still apply within the share:

<!-- snippet: tests-shard-scheduler -->
<a id='snippet-tests-shard-scheduler'></a>
```cs
/// <summary>
/// Runs every n-th test when <c>VG_SHARD</c> is set to "index/count", such as "0/2" on one machine and
/// "1/2" on another, and leaves the rest to the default scheduler's rules.
/// </summary>
public sealed class ShardScheduler : ITestScheduler
{
    private readonly DefaultTestScheduler _default = new();

    public ValueTask RunAsync(IReadOnlyList<ScheduledTest> tests, Func<ScheduledTest, ValueTask> run, CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("VG_SHARD")?.Split('/') is not [var index, var count])
        {
            return _default.RunAsync(tests, run, cancellationToken);
        }

        var shard = tests.Where((test, position) => position % int.Parse(count) == int.Parse(index)).ToArray();
        return _default.RunAsync(shard, run, cancellationToken);
    }
}
```
<sup><a href='/samples/TestsScheduling/SchedulingTests.cs#L96-L116' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-shard-scheduler' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Tests are ordered by UID, so every machine computes the same split. `test/VerificationGlue.Tests.Specs/SchedulerSpecs.cs` runs the sample with `VG_SHARD` set to `0/2` and `1/2`, and checks that each test runs on exactly one of them.

## Next

- [Containers and scopes](containers-and-scopes.md)
- [Data rows](data-rows.md)
- [Test lifecycle](test-lifecycle.md)
- [Scheduling groups and resources](scheduling-groups-and-resources.md)
