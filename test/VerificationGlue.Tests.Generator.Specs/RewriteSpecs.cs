using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using VerificationGlue.Generator;

namespace VerificationGlue.Tests.Generator.Specs;

/// <summary>
/// The registrations call each test's <c>__Verified</c> copy exactly when tier 3 is enabled and the
/// shared <c>WillRewrite</c> is true, and every call binds, in either generator order.
/// </summary>
public class RewriteSpecs
{
    private const string Tests = """
        using VerificationGlue.Tests;
        using static VerificationGlue.VerifySyntax;

        namespace Shop;

        public partial class OrderTests
        {
            [VerifyTest]
            public void Instance()
            {
                var quantity = 2;
                Verify(Total(quantity) == 5);
            }

            [VerifyTest]
            public async Task Awaits(CancellationToken cancellationToken)
            {
                await Task.Delay(1, cancellationToken);
                Verify(Total(4) == 1);
            }

            [VerifyTest]
            public static void Static() => Verify(Total(0) == 2);

            [VerifyTest]
            public virtual void Virtual() => Verify(Total(1) == 0);

            private static int Total(int value) => value + 1;
        }

        public class NotPartialTests
        {
            [VerifyTest]
            public void Not_partial() => Verify(Value() == 3);

            private static int Value() => 2;
        }
        """;

    public static TheoryData<bool, bool> Combinations => new()
    {
        { false, false },
        { false, true },
        { true, false },
        { true, true },
    };

    [Theory]
    [MemberData(nameof(Combinations))]
    public async Task Each_registration_calls_the_copy_only_when_WillRewrite_is_true(bool rewrite, bool reverseOrder)
    {
        var registry = Load(rewrite, reverseOrder);

        var expected = new Dictionary<string, (bool Copy, string Message)>
        {
            ["Shop.OrderTests.Instance()"] = rewrite
                ? (true, "Verify failed: Total(quantity) == 5\n  Total(quantity) → 3\n  quantity        → 2\n")
                : (false, "Verify failed: Total(quantity) == 5\n  expected Total(quantity) to equal 5\n"),
            ["Shop.OrderTests.Awaits(System.Threading.CancellationToken)"] = rewrite
                ? (true, "Verify failed: Total(4) == 1\n  Total(4) → 5\n")
                : (false, "Verify failed: Total(4) == 1\n  expected Total(4) to equal 1\n"),
            ["Shop.OrderTests.Static()"] = rewrite
                ? (true, "Verify failed: Total(0) == 2\n  Total(0) → 1\n")
                : (false, "Verify failed: Total(0) == 2\n  expected Total(0) to equal 2\n"),
            ["Shop.OrderTests.Virtual()"] = (false, "Verify failed: Total(1) == 0\n  expected Total(1) to equal 0\n"),
            ["Shop.NotPartialTests.Not_partial()"] = (false, "Verify failed: Value() == 3\n  expected Value() to equal 3\n"),
        };

        Assert.Equal(expected.Keys.Order(), registry.Keys.Order());
        foreach (var (uid, (copy, message)) in expected)
        {
            var failure = await Assert.ThrowsAsync<VerifyException>(registry[uid]);
            Assert.StartsWith(message, failure.Message);
            Assert.True(
                copy == failure.StackTrace!.Contains("__Verified", StringComparison.Ordinal),
                $"{uid} {(copy ? "did not run" : "ran")} its __Verified copy with rewriting {(rewrite ? "on" : "off")}.\n{failure.StackTrace}");
        }
    }

    /// <summary>The emitted tests by ID, each as a function that runs it the way the runner does.</summary>
    private static Dictionary<string, Func<Task>> Load(bool rewrite, bool reverseOrder)
    {
        IIncrementalGenerator[] generators = [new VerificationGlueGenerator(), new TestGenerator()];
        var input = GeneratorHarness.Compilation([("/repo/Tests/OrderTests.cs", Tests)]);
        GeneratorHarness.Driver(rewrite, reverseOrder ? [.. generators.Reverse()] : generators)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics, TestContext.Current.CancellationToken);
        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        using var stream = new MemoryStream();
        var emitted = output.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        stream.Position = 0;
        var assembly = new AssemblyLoadContext("RewriteSpecs", isCollectible: true).LoadFromStream(stream);
        var registry = assembly.GetType("VerificationGlue.Tests.TestRegistry", throwOnError: true)!;
        var descriptorType = assembly.GetType("VerificationGlue.Tests.TestDescriptor", throwOnError: true)!;
        var contextType = assembly.GetType("VerificationGlue.Tests.TestContext", throwOnError: true)!;
        var newContext = contextType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, [descriptorType, typeof(CancellationToken)])!;
        var uid = descriptorType.GetProperty("Uid")!;
        var invoke = descriptorType.GetProperty("Invoke")!;

        var tests = new Dictionary<string, Func<Task>>();
        foreach (var descriptor in (System.Collections.IEnumerable)registry.GetProperty("Tests")!.GetValue(null)!)
        {
            var run = (Delegate)invoke.GetValue(descriptor)!;
            var context = newContext.Invoke([descriptor, CancellationToken.None]);
            tests.Add((string)uid.GetValue(descriptor)!, () => ((ValueTask)run.DynamicInvoke(context)!).AsTask());
        }

        return tests;
    }
}
