using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VerificationGlue.Tests;

namespace VerificationGlue.Tests.DependencyInjection;

/// <summary>
/// An <see cref="ITestContainer"/> over a service provider. A child is a service scope, so it adds no
/// registrations.
/// </summary>
/// <param name="services">The service provider.</param>
/// <param name="owner">What the container disposes, or null to dispose <paramref name="services"/>.</param>
public sealed class ServiceProviderContainer(IServiceProvider services, IAsyncDisposable? owner = null) : ITestContainer
{
    /// <inheritdoc/>
    public IServiceProvider Services => services;

    /// <summary>
    /// Registers each override as an instance, so the container returns it and does not dispose it.
    /// A keyed override is registered under its key.
    /// </summary>
    /// <param name="collection">The collection to add the overrides to, after its other registrations.</param>
    /// <param name="overrides">The test's overrides.</param>
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

    /// <inheritdoc/>
    public bool TryResolve(TestParameter parameter, out object? value)
    {
        value = parameter.ServiceKey is null
            ? services.GetService(parameter.Type)
            : (services as IKeyedServiceProvider)?.GetKeyedService(parameter.Type, parameter.ServiceKey);
        return value is not null;
    }

    /// <summary>A child for one test: a service scope of this container.</summary>
    /// <param name="context">The test. It holds no overrides, because the bootstrap does not implement <see cref="ISupportsChildOverrides"/>.</param>
    public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context)
    {
        var scope = services.CreateAsyncScope();
        return new(new ServiceProviderContainer(scope.ServiceProvider, scope));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => (owner ?? services as IAsyncDisposable)?.DisposeAsync() ?? default;
}
