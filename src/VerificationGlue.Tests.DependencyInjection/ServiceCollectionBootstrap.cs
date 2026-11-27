using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VerificationGlue.Tests;

namespace VerificationGlue.Tests.DependencyInjection;

/// <summary>
/// Builds the container of <see cref="ServiceCollectionAttribute"/>. It applies the sources the generator
/// registered, such as the DependencyModules modules of the scope's attributes, then the <c>Configure</c>
/// method of each <see cref="ServiceCollectionAttribute{T}"/> from the assembly to the method, and
/// registers the test's overrides last.
/// </summary>
public sealed class ServiceCollectionBootstrap : ITestContainerBootstrap
{
    /// <summary>
    /// Builds the container for a test, or for a class or an assembly that shares it. A shared container
    /// reads the attributes of its scope, which are the class's and the assembly's or the assembly's alone.
    /// </summary>
    /// <param name="context">The scope, its attributes and the test's overrides.</param>
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context)
    {
        var services = new ServiceCollection();
        foreach (var source in ServiceCollectionRegistry.Sources())
        {
            source(services, context.Attributes);
        }

        foreach (var attribute in context.Attributes)
        {
            if (attribute is IServiceCollectionSource source)
            {
                source.Configure(services, context);
            }
        }

        ServiceProviderContainer.AddOverrides(services, context.Overrides);
        return new(new ServiceProviderContainer(services.BuildServiceProvider()));
    }
}

/// <summary>
/// Builds the container of <see cref="ServiceCollectionAttribute{T}"/> as <see cref="ServiceCollectionBootstrap"/>
/// does. Its type names <typeparamref name="T"/>, so two classes that share a container with
/// <c>Scope = ContainerScope.Assembly</c> share one only when they name the same type.
/// </summary>
/// <typeparam name="T">The type whose <c>Configure</c> method registers the services.</typeparam>
public sealed class ServiceCollectionBootstrap<T> : ITestContainerBootstrap
{
    /// <inheritdoc cref="ServiceCollectionBootstrap.BuildAsync"/>
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context) =>
        ServiceCollectionBootstrap.BuildAsync(context);
}
