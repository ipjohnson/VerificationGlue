using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using VerificationGlue.Tests.Generator;

namespace VerificationGlue.Tests.DependencyInjection.Generator.Specs;

/// <summary>
/// An attribute whose class another generator writes, such as the attribute DependencyModules writes for a
/// module declared in the test project. The input declares the module class <c>Shop.SutModule</c>, and
/// <see cref="StandIn"/> plays the other generator's output: it is added only after the generators have run.
/// </summary>
public class UnresolvedAttributeSpecs
{
    private static readonly MetadataReference DependencyModulesRuntime = CSharpCompilation.Create(
            "DependencyModules.Runtime",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
        .ToMetadataReference();

    private const string Declarations = """
        namespace Shop
        {
            public partial class SutModule { }

            public enum Kind { A, B }
        }

        public static class Seen
        {
            public static IReadOnlyList<Attribute> Test = [];
            public static IReadOnlyList<Attribute> Container = [];
        }

        public sealed class Recorder
        {
            public static void Configure(IServiceCollection services, TestContainerContext context) => Seen.Container = context.Attributes;
        }

        """;

    private const string StandIn = """
        namespace Shop
        {
            [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
            public sealed class SutModuleAttribute : System.Attribute
            {
                public SutModuleAttribute() { }

                public SutModuleAttribute(int level, string name)
                {
                    Level = level;
                    Name = name;
                }

                public int Level { get; }

                public string? Name { get; }

                public Kind Kind { get; set; }

                public System.Type[]? Types { get; set; }

                public override string ToString() =>
                    Name + " " + Level + " " + Kind + (Types is null ? "" : " " + string.Join(",", System.Linq.Enumerable.Select(Types, type => type.Name)));
            }
        }
        """;

    private static string Source(string assembly, string type, string method) => $$"""
        using Shop;

        {{assembly}}
        [assembly: Shop.SutModule(1, "assembly")]

        {{type}}
        [SutModuleAttribute(2, nameof(OrderTests), Kind = Kind.B)]
        public partial class OrderTests
        {
            [VerifyTest]
            {{method}}
            [SutModule(3, "method", Types = new[] { typeof(int), typeof(List<>) })]
            public void Pays(TestContext context) => Seen.Test = context.Attributes;
        }
        """;

    [VerifyTest]
    [Row("[assembly: ServiceCollection<Recorder>]", "", "")]
    [Row("", "[ServiceCollection<Recorder>]", "")]
    [Row("", "", "[ServiceCollection<Recorder>]")]
    [Row("[assembly: ServiceCollection]", "", "[ServiceCollection<Recorder>]")]
    public async Task An_attribute_another_generator_writes_reaches_the_test_and_its_container(string assembly, string type, string method)
    {
        var source = Source(assembly, type, method);

        var diagnostics = await Analyze(source, DependencyModulesRuntime);
        var output = RunWithStandIn(source, DependencyModulesRuntime);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "VGT0006");
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var expected = new[] { "assembly 1 A", "OrderTests 2 B", "method 3 A Int32,List`1" };
        var (test, container) = await RunTests(output);
        Assert.Equal(expected, test);
        Assert.Equal(expected, container);
    }

    [VerifyTest]
    public void The_registration_writes_the_attribute_in_the_namespace_of_the_type_it_is_named_for()
    {
        var output = RunWithStandIn(Source("[assembly: ServiceCollection]", "", ""), DependencyModulesRuntime);

        var registration = Registration(output);
        Assert.Contains("new global::Shop.SutModuleAttribute(2, \"OrderTests\") { Kind = (global::Shop.Kind)(1) }", registration);
        Assert.Contains("new global::Shop.SutModuleAttribute(3, \"method\") { Types = new global::System.Type[] { typeof(int), typeof(global::System.Collections.Generic.List<>) } }", registration);
        Assert.Contains(
            "new global::Shop.SutModuleAttribute(1, \"assembly\")",
            output.SyntaxTrees.Single(tree => tree.FilePath.EndsWith("VerificationGlue.Tests.AssemblyAttributes.g.cs", StringComparison.Ordinal)).ToString());
    }

    [VerifyTest]
    [Row("[SutModule(level: 4, name: \"named\")]", "new global::Shop.SutModuleAttribute(level: 4, name: \"named\")")]
    [Row("[SutModule(4, \"x\" + \"y\")]", "new global::Shop.SutModuleAttribute(4, \"xy\")")]
    [Row("[SutModule(Types = [typeof(string)])]", "new global::Shop.SutModuleAttribute() { Types = new global::System.Type[] { typeof(string) } }")]
    [Row("[SutModule(Kind = (Kind)1)]", "new global::Shop.SutModuleAttribute() { Kind = (global::Shop.Kind)(1) }")]
    [Row("[Shop.SutModuleAttribute(-1, null)]", "new global::Shop.SutModuleAttribute(-1, null)")]
    public void Each_argument_is_read_from_syntax(string usage, string expected)
    {
        var output = RunWithStandIn($$"""
            using Shop;

            [ServiceCollection]
            public partial class OrderTests
            {
                [VerifyTest]
                {{usage}}
                public void Pays() { }
            }
            """,
            DependencyModulesRuntime);

        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains(expected, Registration(output));
    }

    [VerifyTest]
    [Row(false, "[ServiceCollection]")]
    [Row(true, "")]
    public async Task Without_DependencyModules_or_a_ServiceCollection_the_attribute_is_left_out_and_reported(bool referencesDependencyModules, string type)
    {
        var source = Source("", type, "");
        MetadataReference[] references = referencesDependencyModules ? [DependencyModulesRuntime] : [];

        var diagnostics = await Analyze(source, references);
        var output = RunWithStandIn(source, references);

        Assert.Equal(
            ["Shop.SutModule(1, \"assembly\")", "SutModule(3, \"method\", Types = new[] { typeof(int), typeof(List<>) })", "SutModuleAttribute(2, nameof(OrderTests), Kind = Kind.B)"],
            diagnostics.Where(diagnostic => diagnostic.Id == "VGT0006").Select(Name).Order(StringComparer.Ordinal));
        Assert.All(
            diagnostics.Where(diagnostic => diagnostic.Id == "VGT0006"),
            diagnostic => Assert.EndsWith("because its type cannot be resolved. Another source generator may produce it, and this generator cannot see that output", diagnostic.GetMessage()));
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.DoesNotContain(output.SyntaxTrees.Where(tree => tree.FilePath != StandInPath), tree => tree.ToString().Contains("new global::Shop.SutModuleAttribute(", StringComparison.Ordinal));
    }

    [VerifyTest]
    [Row("[Missing]", "Missing", "its type cannot be resolved. Another source generator may produce it, and this generator cannot see that output")]
    [Row("[SutModule(typeof(Secret))]", "SutModule(typeof(Secret))", "an argument cannot be written as C#")]
    [Row("[SutModule(1.5m)]", "SutModule(1.5m)", "an argument cannot be written as C#")]
    public async Task An_attribute_the_rule_cannot_write_is_left_out_and_reported(string usage, string name, string reason)
    {
        var source = $$"""
            using Shop;

            [ServiceCollection]
            public partial class OrderTests
            {
                private sealed class Secret { }

                [VerifyTest]
                {{usage}}
                public void Pays() { }
            }
            """;

        var diagnostics = await Analyze(source, DependencyModulesRuntime);
        var registration = Registration(RunWithStandIn(source, DependencyModulesRuntime));

        var diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "VGT0006");
        Assert.Equal($"The attribute '{name}' is not passed to extension points, because {reason}", diagnostic.GetMessage());
        Assert.DoesNotContain("new global::Shop.SutModuleAttribute(", registration);
        Assert.DoesNotContain("Missing", registration);
    }

    private static string Name(Diagnostic diagnostic)
    {
        var message = diagnostic.GetMessage();
        var start = message.IndexOf('\'', StringComparison.Ordinal) + 1;
        return message.Substring(start, message.IndexOf("' is not passed", StringComparison.Ordinal) - start);
    }

    private const string StandInPath = "/repo/Tests/obj/SutModuleAttribute.g.cs";

    /// <summary>Runs the Tests and the package generators, then adds the other generator's output.</summary>
    private static Compilation RunWithStandIn(string source, params MetadataReference[] references)
    {
        CSharpGeneratorDriver.Create([new TestGenerator().AsSourceGenerator(), new ConfigureGenerator().AsSourceGenerator()], parseOptions: GeneratorHarness.ParseOptions)
            .RunGeneratorsAndUpdateCompilation(GeneratorHarness.Compilation(GeneratorHarness.Usings + source, Declarations, references), out var output, out _, CancellationToken.None);
        return output.AddSyntaxTrees(CSharpSyntaxTree.ParseText(StandIn, GeneratorHarness.ParseOptions, StandInPath));
    }

    private static Task<ImmutableArray<Diagnostic>> Analyze(string source, params MetadataReference[] references) =>
        GeneratorHarness.Compilation(GeneratorHarness.Usings + source, Declarations, references)
            .WithAnalyzers([new TestAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);

    private static string Registration(Compilation output) =>
        output.SyntaxTrees.Single(tree => tree.FilePath.EndsWith("OrderTests.Tests.g.cs", StringComparison.Ordinal)).ToString();

    /// <summary>Runs the one registered test and returns the module attributes its test context and its container context held.</summary>
    private static async Task<(string[] Test, string[] Container)> RunTests(Compilation output)
    {
        var assembly = System.Reflection.Assembly.Load(GeneratorHarness.Emit(output));
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        var descriptorType = assembly.GetType("VerificationGlue.Tests.TestDescriptor", throwOnError: true)!;
        var runnerType = assembly.GetType("VerificationGlue.Tests.TestRunner", throwOnError: true)!;
        var descriptors = ((IEnumerable)assembly.GetType("VerificationGlue.Tests.TestRegistry", throwOnError: true)!.GetProperty("Tests")!.GetValue(null)!).Cast<object>().ToArray();
        var descriptor = Assert.Single(descriptors);
        var selected = Array.CreateInstance(descriptorType, 1);
        selected.SetValue(descriptor, 0);
        var runner = Activator.CreateInstance(runnerType, [selected, CancellationToken.None])!;

        var task = (Task)runnerType.GetMethod("RunAsync")!.Invoke(runner, [descriptor])!;
        await task;
        var outcome = task.GetType().GetProperty("Result")!.GetValue(task)!;
        Assert.Empty((IReadOnlyList<Exception>)outcome.GetType().GetProperty("Failures")!.GetValue(outcome)!);

        var seen = assembly.GetType("Seen", throwOnError: true)!;
        return (Modules(seen.GetField("Test")!), Modules(seen.GetField("Container")!));

        static string[] Modules(System.Reflection.FieldInfo field) =>
            ((IReadOnlyList<Attribute>)field.GetValue(null)!).Where(attribute => attribute.GetType().Name == "SutModuleAttribute").Select(attribute => attribute.ToString()!).ToArray();
    }
}
