using System.Collections.Immutable;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Tests.Generator.Specs;

/// <summary>
/// The schedule group, limit, <c>[RunAfter]</c> groups and <c>[Resource]</c> keys that the generator
/// writes into each descriptor, and VGT0003 for a <c>[RunAfter]</c> cycle.
/// </summary>
public class ScheduleSpecs
{
    [VerifyTest]
    public void The_narrowest_ScheduleGroup_wins_and_resources_add_up()
    {
        var schedules = Schedules("""
            using VerificationGlue.Tests;

            [assembly: ScheduleGroup("everything", MaxParallel = 8)]

            namespace Shop;

            [ScheduleGroup("database", MaxParallel = 2)]
            [Resource("orders-table")]
            [RunAfter("migrations")]
            [RunAfter("seed")]
            public partial class OrderTests
            {
                [VerifyTest]
                public void From_the_class() { }

                [VerifyTest]
                [ScheduleGroup("ui", MaxParallel = 1)]
                [Resource("browser")]
                [Resource("orders-table")]
                public void From_the_method() { }
            }

            public partial class OtherTests
            {
                [VerifyTest]
                public void From_the_assembly() { }
            }
            """);

        Assert.Equal(("database", 2, "migrations,seed", "orders-table"), schedules["Shop.OrderTests.From_the_class()"]);
        Assert.Equal(("ui", 1, "migrations,seed", "orders-table,browser"), schedules["Shop.OrderTests.From_the_method()"]);
        Assert.Equal(("everything", 8, "", ""), schedules["Shop.OtherTests.From_the_assembly()"]);
    }

    [VerifyTest]
    public void A_test_with_no_ScheduleGroup_is_in_the_default_group()
    {
        var schedules = Schedules("""
            using VerificationGlue.Tests;

            public partial class OrderTests
            {
                [VerifyTest]
                public void M() { }
            }
            """);

        Assert.Equal(("default", 0, "", ""), schedules["OrderTests.M()"]);
    }

    [VerifyTest]
    [Row("""[ScheduleGroup("a")] [RunAfter("a")] public partial class A { [VerifyTest] public void M() { } }""", "a", "a", "a -> a")]
    [Row("""
        [ScheduleGroup("a")] [RunAfter("b")] public partial class A { [VerifyTest] public void M() { } }
        [ScheduleGroup("b")] [RunAfter("c")] public partial class B { [VerifyTest] public void M() { } }
        [ScheduleGroup("c")] public partial class C { [VerifyTest] public void M() { } }
        [ScheduleGroup("c")] [RunAfter("a")] public partial class C2 { [VerifyTest] public void M() { } }
        """, "a", "b", "a -> b -> c -> a")]
    public async Task A_RunAfter_cycle_reports_VGT0003_on_each_RunAfter_in_it(string types, string group, string waitsOn, string cycle, CancellationToken cancellationToken)
    {
        var diagnostics = await Analyze("using VerificationGlue.Tests;\n" + types);

        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal("VGT0003", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        });
        Assert.Contains(
            $"[RunAfter(\"{waitsOn}\")] makes the schedule group '{group}' wait on itself through {cycle}, so none of these groups can start",
            diagnostics.Select(diagnostic => diagnostic.GetMessage()));
        Assert.Equal(cycle.Split(" -> ").Length - 1, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.StartsWith("RunAfter(", diagnostic.Location.SourceTree!.GetText(cancellationToken).ToString(diagnostic.Location.SourceSpan)));
    }

    [VerifyTest]
    public async Task A_RunAfter_chain_without_a_cycle_reports_nothing()
    {
        var diagnostics = await Analyze("""
            using VerificationGlue.Tests;

            [ScheduleGroup("database")]
            public partial class OrderTests { [VerifyTest] public void M() { } }

            [ScheduleGroup("reports")]
            [RunAfter("database")]
            public partial class ReportTests { [VerifyTest] public void M() { } }

            [RunAfter("reports")]
            [RunAfter("a group with no tests")]
            public partial class CleanupTests { [VerifyTest] public void M() { } }
            """);

        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        var compilation = GeneratorHarness.Compilation([("/repo/Tests/OrderTests.cs", source)]);
        return await compilation.WithAnalyzers([new TestAnalyzer()]).GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    [VerifyTest]
    public void Traits_come_from_the_class_then_the_method()
    {
        var traits = Descriptors("""
            using VerificationGlue.Tests;

            [Trait("Category", "Checkout")]
            public partial class OrderTests
            {
                [VerifyTest]
                public void From_the_class() { }

                [VerifyTest]
                [Trait("Speed", "Slow")]
                [Trait("Category", "Checkout")]
                public void From_both() { }
            }

            public partial class OtherTests
            {
                [VerifyTest]
                public void Without_traits() { }
            }
            """, read => read("Traits"));

        Assert.Equal("Category=Checkout", traits["OrderTests.From_the_class()"]);
        Assert.Equal("Category=Checkout,Speed=Slow", traits["OrderTests.From_both()"]);
        Assert.Equal("", traits["OtherTests.Without_traits()"]);
    }

    /// <summary>Each registered test's group, limit, <c>[RunAfter]</c> groups and resource keys, by UID.</summary>
    private static Dictionary<string, (string Group, int MaxParallel, string RunAfter, string Resources)> Schedules(string source) =>
        Descriptors(source, read => (read("ScheduleGroup"), int.Parse(read("MaxParallel")), read("RunAfter"), read("Resources")));

    /// <summary>Runs the generator on <paramref name="source"/> and projects each registered descriptor by its ID.</summary>
    private static Dictionary<string, T> Descriptors<T>(string source, Func<Func<string, string>, T> project)
    {
        var input = GeneratorHarness.Compilation([("/repo/Tests/OrderTests.cs", source)]);
        GeneratorHarness.Driver(rewrite: false, new TestGenerator())
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics, CancellationToken.None);
        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        using var stream = new MemoryStream();
        var emitted = output.Emit(stream, cancellationToken: CancellationToken.None);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        stream.Position = 0;
        var assembly = new AssemblyLoadContext("Schedules", isCollectible: true).LoadFromStream(stream);
        var registry = assembly.GetType("VerificationGlue.Tests.TestRegistry", throwOnError: true)!;
        var descriptorType = assembly.GetType("VerificationGlue.Tests.TestDescriptor", throwOnError: true)!;
        string Read(object descriptor, string property) => descriptorType.GetProperty(property)!.GetValue(descriptor) switch
        {
            IEnumerable<string> values => string.Join(",", values),
            IEnumerable<KeyValuePair<string, string>> pairs => string.Join(",", pairs.Select(pair => pair.Key + "=" + pair.Value)),
            var value => value!.ToString()!,
        };

        return ((System.Collections.IEnumerable)registry.GetProperty("Tests")!.GetValue(null)!).Cast<object>().ToDictionary(
            descriptor => Read(descriptor, "Uid"),
            descriptor => project(property => Read(descriptor, property)));
    }
}
