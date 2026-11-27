# Containers and scopes

At the end of this page you can give tests their services from a container, replace a service with a `[Mock]` parameter, and share one container across a class or the whole test assembly.

## Give a test a container

`[Container<T>]` names a bootstrap, a type that implements `ITestContainerBootstrap`. The runner calls its static `BuildAsync` and resolves the test's parameters from the container it returns. This bootstrap uses `Microsoft.Extensions.DependencyInjection`:

<!-- snippet: tests-shop-bootstrap -->
<a id='snippet-tests-shop-bootstrap'></a>
```cs
/// <summary>Builds a Microsoft.Extensions.DependencyInjection container with the shop's services and the test's overrides.</summary>
public sealed class ShopBootstrap : ITestContainerBootstrap
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IOrderStore, InMemoryOrderStore>();
        services.AddTransient<OrderService>();
        ServiceProviderContainer.AddOverrides(services, context.Overrides);
        return new(new ServiceProviderContainer(services.BuildServiceProvider()));
    }
}
```
<sup><a href='/samples/TestsContainers/Bootstraps.cs#L6-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-shop-bootstrap' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ServiceProviderContainer` adapts a service provider to `ITestContainer`. [Extending VerificationGlue.Tests](extending.md#containers) shows it.

The `VerificationGlue.Tests.DependencyInjection` package writes this bootstrap for you. `[ServiceCollection<T>]` builds the container from a static `Configure` method on `T`. [Give tests a service collection](service-collection.md) shows it.

## Replace a service with a mock

Mark a parameter `[Mock]`, and the runner creates it with `Mock.Of<T>()`. It registers the mock as an override before it builds the container, so the system under test receives the same instance as the parameter:

<!-- snippet: tests-container-per-test -->
<a id='snippet-tests-container-per-test'></a>
```cs
[Container<ShopBootstrap>]
public partial class OrderServiceTests
{
    [VerifyTest]
    public void Create_stamps_the_order_with_the_clock([Mock] IClock clock, OrderService sut, IOrderStore store)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var order = sut.Create(quantity: 2, price: 10);

        Verify(order.Created == new DateTime(2026, 11, 17));
        Verify(store.Orders.Count == 1);
    }
}
```
<sup><a href='/samples/TestsContainers/ContainerTests.cs#L7-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-container-per-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`OrderService` takes an `IClock`. The container returns the `clock` mock for it, so the order carries the date the test set up.

A `[Mock]` parameter that also has `[FromKeyedServices(key)]` registers its mock under that key. The mock generator writes the mock for each `[Mock]` parameter, so the test project needs no `[assembly: GenerateMock]` for it.

By default each test builds its own container and disposes it when the test ends.

## Share a container across a class or the assembly

`Scope = ContainerScope.Class` builds one container for each test class. `Scope = ContainerScope.Assembly` builds one for the whole test run. Each test gets a child of the shared container, with its own overrides:

<!-- snippet: tests-container-per-class -->
<a id='snippet-tests-container-per-class'></a>
```cs
[Container<SharedStoreBootstrap>(Scope = ContainerScope.Class)]
public partial class OrderHistoryTests
{
    [VerifyTest]
    public void Each_test_gets_its_own_mock([Mock] IClock clock, OrderService sut, IOrderStore store)
    {
        clock.Now.Returns(new DateTime(2026, 1, 1));

        var order = sut.Create(quantity: 1, price: 5);

        Verify(order.Created.Year == 2026);
        Verify(store.Orders.Contains(order));
    }

    [VerifyTest]
    public void The_store_is_shared_by_the_class(IOrderStore store, OrderService sut)
    {
        var order = sut.Create(quantity: 3, price: 2);

        Verify(store.Orders.Contains(order));
    }
}
```
<sup><a href='/samples/TestsContainers/ContainerTests.cs#L24-L47' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-container-per-class' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: tests-container-per-assembly -->
<a id='snippet-tests-container-per-assembly'></a>
```cs
[Container<SharedStoreBootstrap>(Scope = ContainerScope.Assembly)]
public partial class ReportTests
{
    [VerifyTest]
    public void Sees_orders_from_the_assembly_store([Mock] IClock clock, OrderService sut, IOrderStore store)
    {
        sut.Create(quantity: 4, price: 1);

        Verify(store.Orders.Any(order => order.Total == 4));
    }
}
```
<sup><a href='/samples/TestsContainers/ContainerTests.cs#L49-L61' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-container-per-assembly' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The class container is built when the first selected test of the class starts, and disposed after its last selected test. The assembly container is built when the first test that uses it starts, and disposed after the last test of the run. Two classes that name the same bootstrap with `Scope = ContainerScope.Assembly` share one container. [Test lifecycle](test-lifecycle.md) shows the order of every step.

## Overrides under a shared container

A child of a shared container must apply the test's overrides itself. A child that is a `Microsoft.Extensions.DependencyInjection` scope cannot: `CreateScope()` adds no registrations, so the services it creates still get the shared container's `IClock` and not the test's mock.

A bootstrap whose children apply overrides implements `ISupportsChildOverrides`. `SharedStoreBootstrap` builds one store for the scope, and a new service provider for each child, with the shared store and the test's overrides:

<!-- snippet: tests-shared-store-bootstrap -->
<a id='snippet-tests-shared-store-bootstrap'></a>
```cs
/// <summary>
/// Builds one order store for the class or assembly. Each test gets a child container with its own
/// services and overrides, and the shared store.
/// </summary>
public sealed class SharedStoreBootstrap : ITestContainerBootstrap, ISupportsChildOverrides
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context)
    {
        var shared = new ServiceCollection();
        shared.AddSingleton<IOrderStore, InMemoryOrderStore>();
        return new(new SharedStoreContainer(shared.BuildServiceProvider()));
    }

    private sealed class SharedStoreContainer(ServiceProvider shared) : ITestContainer
    {
        public IServiceProvider Services => shared;

        public bool TryResolve(TestParameter parameter, out object? value)
        {
            value = shared.GetService(parameter.Type);
            return value is not null;
        }

        public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context)
        {
            var services = new ServiceCollection();
            services.AddSingleton(shared.GetRequiredService<IOrderStore>());
            services.AddSingleton<IClock, SystemClock>();
            services.AddTransient<OrderService>();
            ServiceProviderContainer.AddOverrides(services, context.Overrides);
            return new(new ServiceProviderContainer(services.BuildServiceProvider()));
        }

        public ValueTask DisposeAsync() => shared.DisposeAsync();
    }
}
```
<sup><a href='/samples/TestsContainers/Bootstraps.cs#L65-L102' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-shared-store-bootstrap' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A test with `[Mock]` parameters under a shared container whose bootstrap does not implement `ISupportsChildOverrides` fails the build with [VGT0008](../diagnostics/VGT0008.md). The runner checks again before it runs each test, because a [scope listener](extending.md#scope-listeners) can add overrides that the build cannot see. That test fails with a message that starts with `VGT0008:` and does not run.

## Where the attribute goes

`[Container<T>]` can go on the assembly, a class or a method. The runner uses the narrowest one: the method's, then the class's, then the assembly's. An adapter can derive its own attribute, as `ServiceCollectionAttribute<T> : ContainerAttribute<ServiceCollectionBootstrap<T>>` does.

## Who disposes what

The container disposes the instances it creates. It does not dispose an override, such as a mock, because the test that added it owns it. When the container returns the test class, the container owns it. Otherwise the runner creates the class and disposes it after the test.

## Next

- [Parameters and where they come from](test-parameters.md)
- [Test lifecycle](test-lifecycle.md)
- [Extending VerificationGlue.Tests](extending.md)
