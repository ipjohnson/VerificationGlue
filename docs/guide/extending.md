# Extending VerificationGlue.Tests

At the end of this page you can write a container bootstrap, a parameter provider, middleware and a scope listener for `VerificationGlue.Tests`.

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
<sup><a href='/samples/TestsLifecycle/LifecycleTests.cs#L52-L68' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-middleware' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/samples/TestsLifecycle/LifecycleTests.cs#L26-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-scope-listener' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`OnCreatedAsync` for a test scope runs before the runner creates mocks and builds the container. A listener can add an override there with `TestScope.AddOverride`. After the container is built, `AddOverride` throws.

`OnDisposingAsync` runs before the scope's containers are disposed. It runs for each listener whose `OnCreatedAsync` returned, in reverse order, even when the test failed.

## Next

- [Containers and scopes](containers-and-scopes.md)
- [Test lifecycle](test-lifecycle.md)
