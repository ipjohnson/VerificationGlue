using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>The level of a <see cref="TestScope"/>.</summary>
public enum TestScopeKind
{
    /// <summary>The test assembly. Created when the first selected test starts and disposed after the last.</summary>
    Assembly,

    /// <summary>A test class. Created when its first selected test starts and disposed after its last.</summary>
    Class,

    /// <summary>One run of one test.</summary>
    Test,
}

/// <summary>An assembly, a class or one test run, with the containers and listeners it owns.</summary>
public sealed class TestScope
{
    private readonly List<TestOverride> _overrides = [];
    private bool _overridesClosed;

    internal TestScope(TestScopeKind kind, string uid, TestScope? parent, IReadOnlyList<Attribute> attributes, ITestOutput output, CancellationToken cancellationToken)
    {
        Kind = kind;
        Uid = uid;
        Parent = parent;
        Attributes = attributes;
        Output = output;
        CancellationToken = cancellationToken;
    }

    /// <summary>The level of the scope.</summary>
    public TestScopeKind Kind { get; }

    /// <summary>The test UID, the full name of the class, or the name of the assembly.</summary>
    public string Uid { get; }

    /// <summary>The class scope of a test scope, the assembly scope of a class scope, or null.</summary>
    public TestScope? Parent { get; }

    /// <summary>
    /// The attributes of the scope: the assembly's, then the class's for a class or test scope, then
    /// the method's for a test scope.
    /// </summary>
    public IReadOnlyList<Attribute> Attributes { get; }

    /// <summary>
    /// The output of the running test. A class or assembly scope writes to the test whose code is
    /// writing, such as the test that created the scope, and output written outside any test is dropped.
    /// </summary>
    public ITestOutput Output { get; }

    /// <summary>Cancelled when the run is cancelled.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>The overrides of a test scope, in the order they were added.</summary>
    public IReadOnlyList<TestOverride> Overrides => _overrides;

    /// <summary>Registers <paramref name="instance"/> in the test's container for <typeparamref name="TService"/>.</summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="instance">The instance. The caller keeps ownership of it.</param>
    /// <param name="key">The service key, or null.</param>
    public void AddOverride<TService>(TService instance, object? key = null)
        where TService : class =>
        AddOverride(typeof(TService), instance, key);

    /// <summary>Registers <paramref name="instance"/> in the test's container for <paramref name="serviceType"/>.</summary>
    /// <param name="serviceType">The service type.</param>
    /// <param name="instance">The instance. The caller keeps ownership of it.</param>
    /// <param name="key">The service key, or null.</param>
    public void AddOverride(Type serviceType, object instance, object? key = null)
    {
        if (Kind != TestScopeKind.Test)
        {
            throw new InvalidOperationException($"Only a test scope takes overrides, and {Uid} is a {Kind.ToString().ToLowerInvariant()} scope.");
        }

        if (_overridesClosed)
        {
            throw new InvalidOperationException(
                "Overrides must be added before the test's container is built, such as in ITestScopeListener.OnCreatedAsync.");
        }

        _overrides.Add(new TestOverride(serviceType, instance, key));
    }

    internal void CloseOverrides() => _overridesClosed = true;

    /// <summary>The listeners whose <see cref="ITestScopeListener.OnCreatedAsync"/> returned, in that order.</summary>
    internal List<ITestScopeListener> Listeners { get; } = [];

    /// <summary>The class or assembly containers, in the order they were first asked for.</summary>
    internal List<SharedContainer> Containers { get; } = [];

    /// <summary>Why the scope could not be created, or null.</summary>
    internal Exception? Failure { get; set; }
}

/// <summary>A class or assembly container, or why it failed to build. The first test that asks for it builds it.</summary>
internal sealed class SharedContainer
{
    private readonly Lazy<Task> _built;

    public SharedContainer(Type bootstrap, Func<ValueTask<ITestContainer>> build)
    {
        Bootstrap = bootstrap;
        _built = new(async () =>
        {
            try
            {
                Container = await build();
            }
            catch (Exception failure)
            {
                Failure = failure;
            }
        });
    }

    public Type Bootstrap { get; }

    public ITestContainer? Container { get; private set; }

    public Exception? Failure { get; private set; }

    public Task BuildAsync() => _built.Value;
}
