using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using VerificationGlue.Tests;

namespace VerificationGlue.Tests.DependencyInjection;

/// <summary>
/// The generated call to the <c>Configure</c> method of <typeparamref name="T"/>, set by
/// <see cref="ServiceCollectionRegistry.Register{T}"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionSource<T>
{
    /// <summary>Calls <c>T.Configure</c>, or is null when the generator wrote no call for <typeparamref name="T"/>.</summary>
    public static Action<IServiceCollection, TestContainerContext>? Configure { get; internal set; }

    internal static void Apply(IServiceCollection services, TestContainerContext context) =>
        (Configure ?? throw new InvalidOperationException(
            $"No Configure call was generated for {typeof(T)} in [ServiceCollection<{typeof(T).Name}>]. The generator of " +
            "VerificationGlue.Tests.DependencyInjection writes it when the test project references the package."))(services, context);
}

/// <summary>The sources that generated module initializers register for <see cref="ServiceCollectionBootstrap"/>.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionRegistry
{
    private static readonly List<Action<IServiceCollection, IReadOnlyList<Attribute>>> s_sources = [];

    /// <summary>
    /// Registers the call to <c>T.Configure</c>. For a DependencyModules module <c>T</c>, generated code
    /// registers a call that adds nothing, because its source adds <c>new T()</c> with the other modules.
    /// Generated code calls this.
    /// </summary>
    /// <param name="configure">Calls <c>T.Configure</c> with the collection and, when it takes one, the context.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register<T>(Action<IServiceCollection, TestContainerContext> configure) =>
        ServiceCollectionSource<T>.Configure = configure;

    /// <summary>
    /// Registers a source that every container of the package calls before the <c>Configure</c> methods,
    /// with the collection and the attributes of the container's scope. Generated code calls this.
    /// </summary>
    /// <param name="source">Adds services for the attributes it knows.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void AddSource(Action<IServiceCollection, IReadOnlyList<Attribute>> source)
    {
        lock (s_sources)
        {
            s_sources.Add(source);
        }
    }

    internal static Action<IServiceCollection, IReadOnlyList<Attribute>>[] Sources()
    {
        lock (s_sources)
        {
            return [.. s_sources];
        }
    }
}
