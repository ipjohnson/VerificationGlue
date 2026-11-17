using System.Collections.Generic;
using System.ComponentModel;

namespace VerificationGlue.Tests;

/// <summary>
/// The tests of the running assembly. Generated module initializers add them before <c>Main</c>
/// runs, so discovery does no scanning.
/// </summary>
public static class TestRegistry
{
    private static readonly List<TestDescriptor> s_tests = [];
    private static readonly object s_lock = new();

    /// <summary>Adds a test. Generated code calls this.</summary>
    /// <param name="test">The test to add.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Add(TestDescriptor test)
    {
        lock (s_lock)
        {
            s_tests.Add(test);
        }
    }

    /// <summary>The registered tests, in registration order.</summary>
    public static IReadOnlyList<TestDescriptor> Tests
    {
        get
        {
            lock (s_lock)
            {
                return s_tests.ToArray();
            }
        }
    }
}
