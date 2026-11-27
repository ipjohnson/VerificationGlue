using Microsoft.Extensions.DependencyInjection;
using VerificationGlue.Tests;

namespace VerificationGlue.Tests.DependencyInjection;

/// <summary>
/// Gives these tests a Microsoft.Extensions.DependencyInjection container. The container holds the
/// services of each <see cref="ServiceCollectionAttribute{T}"/> on the assembly, the class and the method,
/// and a test's <c>[Mock]</c> parameters replace the matching registrations. When the test project
/// references DependencyModules, it also holds the services of each module attribute on them.
/// With <c>Scope = ContainerScope.Class</c> or <c>ContainerScope.Assembly</c>, one container serves the tests
/// of the class or the assembly. Each test gets its own service scope, and a test cannot take a
/// <c>[Mock]</c> parameter (VGT0008).
/// </summary>
public sealed class ServiceCollectionAttribute : ContainerAttribute<ServiceCollectionBootstrap>;

/// <summary>
/// Adds the services that <typeparamref name="T"/> registers to the container of these tests, and gives
/// them the container as <see cref="ServiceCollectionAttribute"/> does. <typeparamref name="T"/> has
/// <c>public static void Configure(IServiceCollection services)</c>, or the same method with a second
/// parameter of <see cref="TestContainerContext"/>. The generator calls it with no reflection. When the
/// test project references DependencyModules, <typeparamref name="T"/> can instead be a module with a public
/// parameterless constructor, and the container adds <c>new T()</c> with the other modules of the test.
/// The generator fails the build with VGD0001 when <typeparamref name="T"/> is neither.
/// </summary>
/// <typeparam name="T">The type whose <c>Configure</c> method registers the services, or a DependencyModules module.</typeparam>
public sealed class ServiceCollectionAttribute<T> : ContainerAttribute<ServiceCollectionBootstrap<T>>, IServiceCollectionSource
{
    void IServiceCollectionSource.Configure(IServiceCollection services, TestContainerContext context) =>
        ServiceCollectionSource<T>.Apply(services, context);
}

/// <summary>An attribute that adds services to the collection that <see cref="ServiceCollectionBootstrap"/> builds.</summary>
internal interface IServiceCollectionSource
{
    void Configure(IServiceCollection services, TestContainerContext context);
}
