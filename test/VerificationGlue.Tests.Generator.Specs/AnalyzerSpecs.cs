using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Tests.Generator.Specs;

public class AnalyzerSpecs
{
    [Fact]
    public async Task Runnable_tests_report_nothing()
    {
        var diagnostics = await Analyze("""
            using VerificationGlue.Tests;

            namespace Shop;

            [Tagged("class", Weight = 2)]
            public partial class OrderTests
            {
                [VerifyTest]
                [Tagged("method", Kinds = new[] { typeof(int), typeof(List<>) }, Level = Level.High)]
                public void Plain() { }

                [VerifyTest]
                public static async ValueTask Async(CancellationToken token, TestContext context) => await Task.Yield();

                public class Nested
                {
                    [VerifyTest]
                    public Task<int> Returns_a_value() => Task.FromResult(1);
                }
            }

            public enum Level { Low, High }

            [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
            public sealed class TaggedAttribute(string name) : Attribute
            {
                public string Name { get; } = name;
                public int Weight { get; set; }
                public Type[]? Kinds { get; set; }
                public Level Level;
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("internal void M() { }", "is not public")]
    [InlineData("public void M<T>() { }", "is generic, and a test needs data rows to close it")]
    [InlineData("public async void M() { await Task.Yield(); }", "is async void, so the runner cannot wait for it")]
    [InlineData("public int M() => 1;", "does not return void, Task or ValueTask")]
    [InlineData("public void M(ref int value) { }", "has a ref, out or in parameter")]
    [InlineData("public void M(Span<int> values) { }", "has the parameter values, whose type cannot be passed as an object")]
    public async Task A_method_the_runner_cannot_call_reports_VGT0004(string method, string reason)
    {
        var diagnostics = await Analyze($$"""
            using VerificationGlue.Tests;

            public partial class OrderTests
            {
                [VerifyTest]
                {{method}}
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("VGT0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal($"The [VerifyTest] method 'M' {reason}, so the runner does not register it", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("public abstract class OrderTests { [VerifyTest] public void M() { } }", "is an instance method of an abstract class")]
    [InlineData("public class OrderTests<T> { [VerifyTest] public void M() { } }", "is in the generic type OrderTests")]
    [InlineData("public class Outer { private class OrderTests { [VerifyTest] public void M() { } } }", "is in OrderTests, which generated code cannot reach")]
    public async Task A_class_the_runner_cannot_use_reports_VGT0004(string type, string reason)
    {
        var diagnostics = await Analyze("using VerificationGlue.Tests;\n" + type);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("VGT0004", diagnostic.Id);
        Assert.Contains(reason, diagnostic.GetMessage());
    }

    [Fact]
    public async Task A_method_that_reports_VGT0004_is_not_registered()
    {
        var input = GeneratorHarness.Compilation([("/repo/Tests/OrderTests.cs", "using VerificationGlue.Tests;\npublic partial class OrderTests { [VerifyTest] internal void M() { } [VerifyTest] public void N() { } }")]);

        var result = GeneratorHarness.Driver(rewrite: false, new TestGenerator()).RunGenerators(input, TestContext.Current.CancellationToken).GetRunResult();

        var registration = result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith("OrderTests.Tests.g.cs", StringComparison.Ordinal)).ToString();
        Assert.Contains("\"OrderTests.N()\"", registration);
        Assert.DoesNotContain("\"OrderTests.M()\"", registration);
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData("private sealed class SecretAttribute : Attribute { }", "[Secret]", "OrderTests.SecretAttribute", "the generated registration cannot access OrderTests.SecretAttribute")]
    [InlineData("", "[Missing]", "Missing", "its type cannot be resolved. Another source generator may produce it, and this generator cannot see that output")]
    public async Task An_attribute_the_registration_cannot_create_reports_VGT0006(string declaration, string usage, string name, string reason)
    {
        var diagnostics = await Analyze($$"""
            using VerificationGlue.Tests;

            public partial class OrderTests
            {
                {{declaration}}

                [VerifyTest]
                {{usage}}
                public void M() { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "VGT0006");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal($"The attribute '{name}' is not passed to extension points, because {reason}", diagnostic.GetMessage());
    }

    [Fact]
    public async Task A_file_local_attribute_reports_VGT0006()
    {
        var diagnostics = await Analyze("""
            using VerificationGlue.Tests;

            file sealed class LocalAttribute : Attribute { }

            [Local]
            public partial class OrderTests
            {
                [VerifyTest]
                public void M() { }

                [VerifyTest]
                public void N() { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("VGT0006", diagnostic.Id);
    }

    [Fact]
    public async Task Tests_with_the_same_id_report_VGT0001()
    {
        var first = Library("namespace Vendor { public sealed class Item { } }", "First");
        var second = Library("namespace Vendor { public sealed class Item { } }", "Second");

        var diagnostics = await Analyze(
            """
            extern alias First;
            extern alias Second;
            using VerificationGlue.Tests;

            public partial class OrderTests
            {
                [VerifyTest]
                public void M(First::Vendor.Item item) { }

                [VerifyTest]
                public void M(Second::Vendor.Item item) { }
            }
            """,
            first.WithAliases(["First"]),
            second.WithAliases(["Second"]));

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal("VGT0001", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("The test ID 'OrderTests.M(Vendor.Item)' is also the ID of another test. Each test ID must be unique.", diagnostic.GetMessage());
        });
    }

    [Theory]
    [InlineData("[Row(\"two\", 10)]", "gives \"two\" (string) for the parameter 'qty' of type int")]
    [InlineData("[Row(2.5, 10)]", "gives 2.5 (double) for the parameter 'qty' of type int")]
    [InlineData("[Row(2, 10L)]", "gives 10 (long) for the parameter 'price' of type int")]
    [InlineData("[Row(null, 10)]", "gives null for the parameter 'qty' of type int")]
    [InlineData("[Row(2, 10, \"note\", 4)]", "has 4 values, but the method has 3 parameters")]
    [InlineData("[Row]", "has no values")]
    [InlineData("[Row(300, 10)]", "gives 300 (int) for the parameter 'qty' of type byte")]
    public async Task A_row_that_does_not_match_its_parameters_reports_VGT0002(string row, string reason)
    {
        var type = row.Contains("300", StringComparison.Ordinal) ? "byte" : "int";
        var diagnostics = await Analyze($$"""
            using VerificationGlue.Tests;

            public partial class OrderTests
            {
                [VerifyTest]
                [Row(1, 1)]
                {{row}}
                public void Totals({{type}} qty, int price, string? note) { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("VGT0002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal($"The [Row] on 'Totals' {reason}, so the row is not registered", diagnostic.GetMessage());
        Assert.Equal(row, "[" + diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan) + "]");
    }

    [Theory]
    [InlineData("[RowSource(\"Missing\")]", "names 'Missing', which is not a method, property or field of OrderTests")]
    [InlineData("[RowSource(\"\")]", "names no member")]
    [InlineData("[RowSource(nameof(Instance))]", "names 'Instance', which is not static")]
    [InlineData("[RowSource(nameof(Hidden))]", "names 'Hidden', which is private, so the generated registration cannot read it")]
    [InlineData("[RowSource(nameof(Filtered))]", "names the method 'Filtered', which has parameters or type parameters")]
    [InlineData("[RowSource(nameof(Count))]", "names 'Count', which returns int, not IEnumerable<T> or IAsyncEnumerable<T> of the row values")]
    [InlineData("[RowSource(nameof(Words))]", "names 'Words', whose rows give string for the parameter 'qty' of type int")]
    [InlineData("[RowSource(nameof(Longs))]", "names 'Longs', whose rows give long for the parameter 'qty' of type int")]
    [InlineData("[RowSource(nameof(Triples))]", "names 'Triples', whose rows have 3 values, but the method has 2 parameters")]
    public async Task A_row_source_that_is_missing_or_has_the_wrong_shape_reports_VGT0005(string source, string reason)
    {
        var diagnostics = await Analyze($$"""
            using VerificationGlue.Tests;

            public partial class OrderTests
            {
                public IEnumerable<int> Instance => [1];
                private static IEnumerable<int> Hidden => [1];
                public static IEnumerable<int> Filtered(int min) => [min];
                public static int Count => 1;
                public static string[] Words = ["a"];
                public static IEnumerable<long> Longs() => [1L];
                public static IEnumerable<(int, int, int)> Triples() => [(1, 2, 3)];

                [VerifyTest]
                {{source}}
                public void Totals(int qty, int price) { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("VGT0005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal($"The [RowSource] on 'Totals' {reason}, so its rows are not registered", diagnostic.GetMessage());
        Assert.Equal(source, "[" + diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan) + "]");
    }

    [Fact]
    public async Task Row_sources_with_rows_that_match_their_parameters_report_nothing()
    {
        var diagnostics = await Analyze("""
            using VerificationGlue.Tests;

            public class Rows
            {
                public static IEnumerable<int> Inherited() => [1];
            }

            public partial class OrderTests : Rows
            {
                public static (int, long)[] Pairs = [(1, 2)];
                internal static IEnumerable<(short Qty, int Price)> Narrow => [(1, 2)];
                public static async IAsyncEnumerable<(int, long)> Later() { await Task.Yield(); yield return (1, 2); }

                [VerifyTest]
                [RowSource(nameof(Pairs))]
                [RowSource(nameof(Narrow), DeterministicReplay = true)]
                [RowSource(nameof(Later))]
                [RowSource(nameof(Inherited))]
                public void Totals(int qty, long price) { }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Rows_that_match_their_parameters_report_nothing()
    {
        var diagnostics = await Analyze("""
            using VerificationGlue.Tests;

            public enum Level { Low, High }

            public partial class OrderTests
            {
                [VerifyTest]
                [Row(2, 10, null, Level.High, 'a', 7)]
                [Row((byte)2, 10L, "note", Level.Low, 'b', null)]
                [Row(2)]
                public void Totals(long qty, double price, string? note, Level level, object letter, int? count) { }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Whole_numbers_given_for_decimal_nint_and_nuint_parameters_report_nothing()
    {
        var diagnostics = await Analyze("""
            using VerificationGlue.Tests;

            public partial class OrderTests
            {
                [VerifyTest]
                [Row(2, 10)]
                [Row(2, 10UL, 'a', 7, 7U)]
                public void Totals(int qty, decimal price, decimal? tax = null, nint offset = 0, nuint size = 0) { }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("[Row(2.5)]", "decimal", "gives 2.5 (double) for the parameter 'value' of type decimal")]
    [InlineData("[Row(2L)]", "nint", "gives 2 (long) for the parameter 'value' of type nint")]
    [InlineData("[Row(-1)]", "nuint", "gives -1 (int) for the parameter 'value' of type nuint")]
    public async Task A_value_with_no_implicit_conversion_to_decimal_nint_or_nuint_reports_VGT0002(string row, string type, string reason)
    {
        var diagnostics = await Analyze($$"""
            using VerificationGlue.Tests;

            public partial class OrderTests
            {
                [VerifyTest]
                {{row}}
                public void Totals({{type}} value) { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("VGT0002", diagnostic.Id);
        Assert.Equal($"The [Row] on 'Totals' {reason}, so the row is not registered", diagnostic.GetMessage());
    }

    [Fact]
    public async Task A_row_value_the_registration_cannot_write_reports_VGT0002_and_not_VGT0006()
    {
        var diagnostics = await Analyze("""
            using VerificationGlue.Tests;

            public partial class OrderTests
            {
                private sealed class Secret { }

                [VerifyTest]
                [Row(typeof(Secret))]
                public void Totals(Type type) { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("VGT0002", diagnostic.Id);
        Assert.Equal("The [Row] on 'Totals' gives typeof(OrderTests.Secret), which the generated registration cannot write, so the row is not registered", diagnostic.GetMessage());
    }

    private const string Usings = """
        using VerificationGlue.Mocks;
        using VerificationGlue.Tests;

        """;

    private const string Bootstraps = """
        public interface IClock { DateTime Now { get; } }

        public sealed class PlainBootstrap : ITestContainerBootstrap
        {
            public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context) => throw new NotImplementedException();
        }

        public sealed class ChildBootstrap : ITestContainerBootstrap, ISupportsChildOverrides
        {
            public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context) => throw new NotImplementedException();
        }

        public sealed class PlainContainerAttribute : ContainerAttribute<PlainBootstrap>;

        """;

    [Theory]
    [InlineData("", "[Container<PlainBootstrap>(Scope = ContainerScope.Class)]", "", "public void M([Mock] IClock clock) { }", "Class")]
    [InlineData("[assembly: Container<PlainBootstrap>(Scope = ContainerScope.Assembly)]", "", "", "public void M([Mock] IClock clock) { }", "Assembly")]
    [InlineData("", "", "[Container<PlainBootstrap>(Scope = ContainerScope.Class)]", "public void M([Mock] IClock clock) { }", "Class")]
    [InlineData("", "[Container<ChildBootstrap>(Scope = ContainerScope.Class)]", "[Container<PlainBootstrap>(Scope = ContainerScope.Assembly)]", "public void M([Mock] IClock clock) { }", "Assembly")]
    [InlineData("", "[PlainContainer(Scope = ContainerScope.Class)]", "", "public void M([Mock] IClock clock) { }", "Class")]
    [InlineData("", "[Container<PlainBootstrap>(Scope = ContainerScope.Class)]", "", "public void M() { } public OrderTests([Mock] IClock clock) { }", "Class")]
    public async Task A_mock_under_a_shared_container_without_child_overrides_reports_VGT0008(
        string assembly, string type, string method, string members, string scope)
    {
        var diagnostics = await Analyze($$"""
            {{Usings}}
            {{assembly}}
            {{Bootstraps}}
            {{type}}
            public partial class OrderTests
            {
                {{method}}
                [VerifyTest]
                {{members}}
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("VGT0008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            $"The test 'M' has [Mock] parameters, but its container PlainBootstrap is shared with Scope = {scope} and PlainBootstrap does not implement ISupportsChildOverrides, so the system under test would not receive the mocks",
            diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("[Container<PlainBootstrap>]", "public void M([Mock] IClock clock) { }")]
    [InlineData("[Container<PlainBootstrap>(Scope = ContainerScope.Test)]", "public void M([Mock] IClock clock) { }")]
    [InlineData("[Container<ChildBootstrap>(Scope = ContainerScope.Class)]", "public void M([Mock] IClock clock) { }")]
    [InlineData("[Container<PlainBootstrap>(Scope = ContainerScope.Assembly)]", "public void M(IClock clock) { }")]
    [InlineData("[Container<PlainBootstrap>(Scope = ContainerScope.Class)]", "public static void M() { } public OrderTests([Mock] IClock clock) { }")]
    public async Task A_container_that_returns_the_mocks_reports_nothing(string type, string members)
    {
        var diagnostics = await Analyze($$"""
            {{Usings}}
            {{Bootstraps}}
            {{type}}
            public partial class OrderTests
            {
                [VerifyTest]
                {{members}}
            }
            """);

        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source, params MetadataReference[] references)
    {
        var compilation = GeneratorHarness.Compilation([("/repo/Tests/OrderTests.cs", source)], references);
        var analyzed = compilation.WithAnalyzers([new TestAnalyzer()]);
        return await analyzed.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    private static PortableExecutableReference Library(string source, string name)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
