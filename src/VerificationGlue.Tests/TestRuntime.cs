using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>Helpers that generated test registrations call.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestRuntime
{
    /// <summary>Disposes a test class instance that the runner created.</summary>
    /// <param name="instance">The instance.</param>
    public static ValueTask DisposeAsync(object instance)
    {
        if (instance is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        (instance as IDisposable)?.Dispose();
        return default;
    }

    /// <summary>The failure for a test parameter that no provider can supply.</summary>
    /// <param name="parameter">The parameter name.</param>
    /// <param name="type">The parameter type.</param>
    public static InvalidOperationException NoValue(string parameter, string type) =>
        new($"Test parameter '{parameter}' of type {type} has no value. The providers tried were: built-in values (CancellationToken, TestContext).");

    /// <summary>The failure for a test class the runner cannot create.</summary>
    /// <param name="type">The test class.</param>
    public static InvalidOperationException NoConstructor(string type) =>
        new($"The runner cannot create {type}. A test class needs a constructor without parameters, or the test method must be static.");
}
