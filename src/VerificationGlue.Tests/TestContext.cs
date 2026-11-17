using System;
using System.Collections.Generic;
using System.Threading;

namespace VerificationGlue.Tests;

/// <summary>The running test. A <see cref="TestContext"/> test parameter receives it.</summary>
public sealed class TestContext
{
    private readonly TestDescriptor _test;
    private readonly Dictionary<TestParameter, object> _mocks = [];

    internal TestContext(TestDescriptor test, TestScope scope, CancellationToken cancellationToken)
    {
        _test = test;
        Scope = scope;
        CancellationToken = cancellationToken;
    }

    /// <summary>The stable test ID.</summary>
    public string Uid => _test.Uid;

    /// <summary>The name a test explorer shows.</summary>
    public string DisplayName => _test.DisplayName;

    /// <summary>Cancelled when the run is cancelled. A <see cref="System.Threading.CancellationToken"/> test parameter receives it.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// The attributes of the assembly, the class and the method, in that order, except attributes
    /// from <c>System</c> namespaces. They are created inside the test, so a constructor that throws
    /// fails only this test.
    /// </summary>
    public IReadOnlyList<Attribute> Attributes => Scope.Attributes;

    /// <summary>The test's scope.</summary>
    public TestScope Scope { get; }

    /// <summary>The test's output. An <see cref="ITestOutput"/> test parameter receives it.</summary>
    public ITestOutput Output => Scope.Output;

    /// <summary>The test's container, or null when no <c>[Container&lt;T&gt;]</c> applies.</summary>
    public ITestContainer? Container { get; internal set; }

    /// <summary>The services of the test's container, or null.</summary>
    public IServiceProvider? Services => Container?.Services;

    /// <summary>The constructor parameters of the test class, in order.</summary>
    public IReadOnlyList<TestParameter> ConstructorParameters => _test.ConstructorParameters;

    /// <summary>The parameters of the test method, in order.</summary>
    public IReadOnlyList<TestParameter> Parameters => _test.Parameters;

    internal TestDescriptor Test => _test;

    internal void SetMock(TestParameter parameter, object mock) => _mocks[parameter] = mock;

    internal bool TryGetMock(TestParameter parameter, out object? mock)
    {
        var found = _mocks.TryGetValue(parameter, out var value);
        mock = value;
        return found;
    }
}
