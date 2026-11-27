using System;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>Which scope owns the container that <see cref="ContainerAttribute{T}"/> builds.</summary>
public enum ContainerScope
{
    /// <summary>Each test builds its own container and disposes it when the test ends.</summary>
    Test,

    /// <summary>Each test class builds one container. Each test gets a child of it.</summary>
    Class,

    /// <summary>The assembly builds one container. Each test gets a child of it.</summary>
    Assembly,
}

/// <summary>
/// The base of <see cref="ContainerAttribute{T}"/>. The runner uses the narrowest one: the method's,
/// then the class's, then the assembly's.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public abstract class ContainerAttribute : Attribute
{
    private protected ContainerAttribute()
    {
    }

    /// <summary>Which scope owns the container. The default is <see cref="ContainerScope.Test"/>.</summary>
    public ContainerScope Scope { get; set; }

    /// <summary>The bootstrap type, which also tells shared containers apart.</summary>
    public abstract Type Bootstrap { get; }

    /// <summary>True when the bootstrap implements <see cref="ISupportsChildOverrides"/>.</summary>
    public abstract bool SupportsChildOverrides { get; }

    internal abstract ValueTask<ITestContainer> BuildAsync(TestContainerContext context);
}

/// <summary>
/// Builds the container of these tests with <typeparamref name="T"/>. An adapter can derive its own
/// attribute from this one, as <c>ServiceCollectionAttribute : ContainerAttribute&lt;ServiceCollectionBootstrap&gt;</c> does.
/// </summary>
/// <typeparam name="T">The bootstrap.</typeparam>
public class ContainerAttribute<T> : ContainerAttribute
    where T : ITestContainerBootstrap
{
    /// <inheritdoc/>
    public override Type Bootstrap => typeof(T);

    /// <inheritdoc/>
    public override bool SupportsChildOverrides => typeof(ISupportsChildOverrides).IsAssignableFrom(typeof(T));

    internal override ValueTask<ITestContainer> BuildAsync(TestContainerContext context) => T.BuildAsync(context);
}
