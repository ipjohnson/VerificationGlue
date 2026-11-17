using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>Builds a test container. <see cref="ContainerAttribute{T}"/> names the bootstrap.</summary>
public interface ITestContainerBootstrap
{
    /// <summary>
    /// Builds a container. For a test-scoped container, <paramref name="context"/> holds the test's
    /// overrides, and the container must return each override's instance for its service type and key.
    /// For a class or assembly container, it holds no overrides.
    /// </summary>
    /// <param name="context">The scope, its attributes and the overrides.</param>
    static abstract ValueTask<ITestContainer> BuildAsync(TestContainerContext context);
}

/// <summary>
/// Declares that a bootstrap's containers apply per-test overrides to a child container. Only then
/// may a test with overrides use a class or assembly container (VGT0008).
/// </summary>
public interface ISupportsChildOverrides
{
}

/// <summary>A container that supplies test parameters and the services they need.</summary>
public interface ITestContainer : IAsyncDisposable
{
    /// <summary>The container's services.</summary>
    IServiceProvider Services { get; }

    /// <summary>Supplies a value for <paramref name="parameter"/>, or returns false when the container has none.</summary>
    /// <param name="parameter">The test or constructor parameter.</param>
    /// <param name="value">The value.</param>
    bool TryResolve(TestParameter parameter, out object? value);

    /// <summary>
    /// Creates a child for one test. <paramref name="context"/> holds the test's overrides. A bootstrap
    /// that does not implement <see cref="ISupportsChildOverrides"/> never receives overrides here.
    /// </summary>
    /// <param name="context">The test, its attributes and its overrides.</param>
    ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context);
}

/// <summary>An instance a container returns for a service type and an optional key, such as a <c>[Mock]</c> parameter's mock.</summary>
/// <param name="ServiceType">The service type.</param>
/// <param name="Instance">The instance. The container does not own it and must not dispose it.</param>
/// <param name="Key">The service key, or null.</param>
public sealed record TestOverride(Type ServiceType, object Instance, object? Key);

/// <summary>What a bootstrap or a parent container needs to build a container.</summary>
public sealed class TestContainerContext
{
    internal TestContainerContext(
        TestScope scope,
        IReadOnlyList<TestParameter> parameters,
        IReadOnlyList<TestOverride> overrides,
        ITestContainer? parent,
        ITestOutput output,
        CancellationToken cancellationToken)
    {
        Scope = scope;
        Parameters = parameters;
        Overrides = overrides;
        Parent = parent;
        Output = output;
        CancellationToken = cancellationToken;
    }

    /// <summary>The scope that will own the container.</summary>
    public TestScope Scope { get; }

    /// <summary>The UID of the test, or of the class or assembly scope.</summary>
    public string Uid => Scope.Uid;

    /// <summary>The attributes of the scope: the assembly's, then the class's, then the method's.</summary>
    public IReadOnlyList<Attribute> Attributes => Scope.Attributes;

    /// <summary>The constructor parameters and then the method parameters of the test, or none for a class or assembly container.</summary>
    public IReadOnlyList<TestParameter> Parameters { get; }

    /// <summary>The overrides to register, in the order they were added.</summary>
    public IReadOnlyList<TestOverride> Overrides { get; }

    /// <summary>The class or assembly container this one is a child of, or null.</summary>
    public ITestContainer? Parent { get; }

    /// <summary>The output of the test that the container is built for.</summary>
    public ITestOutput Output { get; }

    /// <summary>Cancelled when the run is cancelled.</summary>
    public CancellationToken CancellationToken { get; }
}
