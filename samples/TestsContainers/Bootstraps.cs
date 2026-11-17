using Microsoft.Extensions.DependencyInjection;
using VerificationGlue.Tests;

namespace TestsContainers;

#region tests-shop-bootstrap
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
#endregion

#region tests-service-provider-container
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
#endregion

#region tests-shared-store-bootstrap
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
#endregion
