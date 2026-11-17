using System;
using System.Collections.Generic;
using System.Threading;

namespace VerificationGlue.Tests;

/// <summary>The running test. A <see cref="TestContext"/> test parameter receives it.</summary>
public sealed class TestContext
{
    private readonly TestDescriptor _test;
    private IReadOnlyList<Attribute>? _attributes;

    internal TestContext(TestDescriptor test, CancellationToken cancellationToken)
    {
        _test = test;
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
    /// from <c>System</c> namespaces. They are created on first use, inside the test, so a constructor
    /// that throws fails only this test.
    /// </summary>
    public IReadOnlyList<Attribute> Attributes => _attributes ??= _test.CreateAttributes();
}
