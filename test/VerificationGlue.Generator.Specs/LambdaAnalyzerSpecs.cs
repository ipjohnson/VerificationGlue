using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using VerificationGlue.CodeFixes;

namespace VerificationGlue.Generator.Specs;

public class LambdaAnalyzerSpecs
{
    private const string Header = """
        using System;
        using System.Threading.Tasks;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Xunit { public class FactAttribute : Attribute { } public class TheoryAttribute : Attribute { } }
        namespace NUnit.Framework { public class TestAttribute : Attribute { } public class TestCaseAttribute : Attribute { } }
        namespace Microsoft.VisualStudio.TestTools.UnitTesting { public class TestMethodAttribute : Attribute { } public class DataTestMethodAttribute : Attribute { } }
        namespace Runner { public class VerifyTestAttribute : Attribute { } }
        namespace Custom { public class MyTestAttribute : Attribute { } public class HelperAttribute : Attribute { } }

        public record Order(int Id, int Total, string? Customer = null);

        """;

    [VerifyTest]
    [Row("[Xunit.Fact]")]
    [Row("[Xunit.FactAttribute]")]
    [Row("[Xunit.Theory]")]
    [Row("[NUnit.Framework.Test]")]
    [Row("[NUnit.Framework.TestCase]")]
    [Row("[Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]")]
    [Row("[Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethod]")]
    [Row("[Runner.VerifyTest]")]
    [Row("[Custom.MyTest]")]
    [Row("[Verify]")]
    public void VG0009_reports_Verify_bool_in_a_test_method(string attribute)
    {
        var diagnostic = Assert.Single(Analyze(Method(attribute, "var order = new Order(7, 40); Verify(order.Total == 40);")));

        Assert.Equal("VG0009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal("Write 'Verify(() => order.Total == 40)' so that a failure lists the value of each part", diagnostic.GetMessage());
        Assert.Equal("order.Total == 40", Text(diagnostic));
    }

    [VerifyTest]
    public void VG0009_reports_Check_bool()
    {
        var diagnostic = Assert.Single(Analyze(Method("[Xunit.Fact]", "var order = new Order(7, 40); Check(order.Total == 40);")));

        Assert.Equal("Write 'Check(() => order.Total == 40)' so that a failure lists the value of each part", diagnostic.GetMessage());
    }

    [VerifyTest]
    [Row("")]
    [Row("[Obsolete]")]
    [Row("[Custom.Helper]")]
    public void VG0009_does_not_report_outside_a_test_method(string attribute)
    {
        Assert.Empty(Analyze(Method(attribute, "var order = new Order(7, 40); Verify(order.Total == 40);")));
    }

    [VerifyTest]
    [Row("public async Task M() { var order = new Order(7, 40); Verify(await Task.FromResult(order.Total) == 40); }", "await")]
    [Row("public void M(int[] values) { Span<int> span = values; Verify(span.Length == 0); }", "a Span local")]
    [Row("public void M(ReadOnlySpan<char> text) { Verify(text.Length == 0); }", "a Span parameter")]
    [Row("public void M(int[] values) { ref int first = ref values[0]; Verify(first == 1); }", "a ref local")]
    [Row("public void M(ref int x) { Verify(x == 1); }", "a ref parameter")]
    [Row("public void M(in int x) { Verify(x == 1); }", "an in parameter")]
    [Row("public void M(out int x) { x = 1; Verify(x == 1); }", "an out parameter")]
    [Row("public void M(System.Collections.Generic.Dictionary<int, int> map) { int value; Verify(map.TryGetValue(1, out value)); Console.WriteLine(value); }", "an out argument to a local")]
    [Row("public void M(System.Collections.Generic.Dictionary<int, int> map) { Verify(map.TryGetValue(1, out var value)); Console.WriteLine(value); }", "an out var used later")]
    [Row("public void M(Order? order) { Verify(order != null); Console.WriteLine(order.Total); }", "a null test used later")]
    [Row("public void M(Order order) { Verify(order.Customer is not null); Console.WriteLine(order.Customer.Length); }", "a null test of a property used later")]
    [Row("public void M(string? name) { Verify(!string.IsNullOrEmpty(name)); Console.WriteLine(name.Length); }", "a null test by a call used later")]
    [Row("public void M(Order order) { Verify(order.Total == 40, $\"order {order.Id}\"); }", "an interpolated message")]
    [Row("public void M(Order order) { Verify(order.Total == 40, \"order.Total == 40\"); }", "an explicit expression")]
    [Row("public VerifyResult M(Order order) => Verify(order.Total == 40);", "a used return value")]
    [Row("public void M(Order order) { Verify(order.Total == 40).Because(\"why\"); }", "a Because chain")]
    [Row("public void M() { Verify(1 + 1 == 2); }", "a constant")]
    [Row("public void M(int x) { Verify((x = 1) == 1); }", "an assignment")]
    public void VG0009_does_not_report_when_the_lambda_would_not_compile_or_would_lose_something(string method, string reason)
    {
        var diagnostics = Analyze(Header + "public partial class Tests { [Xunit.Fact] " + method + " }", allowWarnings: true);

        Assert.False(diagnostics.Any(diagnostic => diagnostic.Id == "VG0009"), $"VG0009 was reported for {reason}");
    }

    [VerifyTest]
    public void VG0009_does_not_report_this_in_a_struct()
    {
        var source = Header + "public struct Tests { public int Total; [Xunit.Fact] public void M() { Verify(Total == 40); } }";

        Assert.Empty(Analyze(source));
    }

    [VerifyTest]
    [Row("public void M(object o) { Verify(o is Order order); }")]
    [Row("public void M(System.Collections.Generic.Dictionary<int, int> map) { Verify(map.TryGetValue(1, out var value)); }")]
    [Row("public void M(Order? order) { Verify(order != null); }")]
    [Row("public void M(Order order) { Verify(order.Total == 40); Console.WriteLine(order.Id); }")]
    [Row("public void M(int x) => Verify(x == 1);")]
    [Row("public void M(int[] values) { Verify(values.AsSpan().Length == 0); }")]
    [Row("public void M(int[] values) { Action check = () => Verify(values.Length == 0); check(); }")]
    public void VG0009_reports_when_nothing_is_lost(string method)
    {
        var diagnostics = Analyze(Header + "public partial class Tests { [Xunit.Fact] " + method + " }");

        Assert.Equal("VG0009", Assert.Single(diagnostics).Id);
    }

    [VerifyTest]
    public void VG0009_reports_a_null_test_used_later_when_nullable_is_disabled()
    {
        var source = Header + "#nullable disable\npublic class Tests { [Xunit.Fact] public void M(Order order) { Verify(order != null); Console.WriteLine(order.Total); } }";

        Assert.Equal("VG0009", Assert.Single(Analyze(source)).Id);
    }

    [VerifyTest]
    public void VG0009_does_not_report_in_a_method_that_tier_3_rewrites()
    {
        var source = Method("[Verify]", "var order = new Order(7, 40); Verify(order.Total == 40);");

        Assert.Empty(Analyze(source, rewrite: true));
        Assert.Single(Analyze(source, rewrite: false));
    }

    [VerifyTest]
    [Row(null, DiagnosticSeverity.Info)]
    [Row("info", DiagnosticSeverity.Info)]
    [Row("Info", DiagnosticSeverity.Info)]
    [Row("warning", DiagnosticSeverity.Warning)]
    [Row("error", DiagnosticSeverity.Error)]
    public void VG0009_uses_the_severity_that_VerificationGlueLambdaSeverity_sets(string? severity, DiagnosticSeverity expected)
    {
        var diagnostic = Assert.Single(Analyze(Method("[Xunit.Fact]", "var order = new Order(7, 40); Verify(order.Total == 40);"), severity: severity));

        Assert.Equal(expected, diagnostic.Severity);
    }

    [VerifyTest]
    public void The_VG0009_descriptor_is_a_warning_so_that_a_command_line_build_runs_the_analyzer()
    {
        Assert.Equal(DiagnosticSeverity.Warning, Assert.Single(new LambdaAnalyzer().SupportedDiagnostics).DefaultSeverity);
    }

    [VerifyTest]
    public void VG0009_is_not_reported_when_VerificationGlueLambdaSeverity_is_none()
    {
        Assert.Empty(Analyze(Method("[Xunit.Fact]", "var order = new Order(7, 40); Verify(order.Total == 40);"), severity: "none"));
    }

    [VerifyTest]
    [Row("warning", DiagnosticSeverity.Error)]
    [Row("info", DiagnosticSeverity.Info)]
    public void VG0009_as_a_warning_is_an_error_with_warnings_as_errors(string severity, DiagnosticSeverity expected)
    {
        var source = Method("[Xunit.Fact]", "var order = new Order(7, 40); Verify(order.Total == 40);");
        var options = Compilation(source).Options.WithGeneralDiagnosticOption(ReportDiagnostic.Error);

        var diagnostic = Assert.Single(Analyze(source, severity: severity, options: options));

        Assert.Equal(expected, diagnostic.Severity);
    }

    [VerifyTest]
    [Row(ReportDiagnostic.Suppress, null)]
    [Row(ReportDiagnostic.Error, DiagnosticSeverity.Error)]
    public void An_editorconfig_severity_wins_over_VerificationGlueLambdaSeverity(ReportDiagnostic configured, DiagnosticSeverity? expected)
    {
        var source = Method("[Xunit.Fact]", "var order = new Order(7, 40); Verify(order.Total == 40);");
        var options = Compilation(source).Options.WithSyntaxTreeOptionsProvider(new TreeOptions(configured));

        var diagnostics = Analyze(source, severity: "warning", options: options);

        Assert.Equal(expected, diagnostics.SingleOrDefault()?.Severity);
    }

    [VerifyTest]
    [Row("Verify(order.Total == 40);", "Verify(() => order.Total == 40);")]
    [Row("Check(order.Total == 40);", "Check(() => order.Total == 40);")]
    [Row("Verify( order.Total == 40 );", "Verify( () => order.Total == 40 );")]
    [Row("Verify(condition: order.Total == 40);", "Verify(condition: () => order.Total == 40);")]
    [Row("VerifySyntax.Verify(order.Total == 40 &&\n            order.Id > 0);", "VerifySyntax.Verify(() => order.Total == 40 &&\n            order.Id > 0);")]
    public async Task The_fix_for_VG0009_writes_the_condition_as_a_lambda(string call, string expected, CancellationToken cancellationToken)
    {
        var source = Method("[Xunit.Fact]", "var order = new Order(7, 40);\n        " + call);

        var fixedSource = await Fix(source, cancellationToken);

        Assert.Equal(source.Replace(call, expected), fixedSource);
        Assert.Empty(Analyze(fixedSource));
    }

    private static string Method(string attribute, string body) =>
        Header + "public partial class Tests\n{\n    " + attribute + "\n    public void M()\n    {\n        " + body + "\n    }\n}\n";

    private static CSharpCompilation Compilation(string source) => GeneratorHarness.Compilation(("/repo/Project/Input.cs", source));

    private static ImmutableArray<Diagnostic> Analyze(string source, bool rewrite = false, string? severity = null, CompilationOptions? options = null, bool allowWarnings = false)
    {
        var compilation = Compilation(source);
        if (options is not null)
        {
            compilation = compilation.WithOptions((CSharpCompilationOptions)options);
        }

        Assert.Empty(compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error || (!allowWarnings && diagnostic.Severity == DiagnosticSeverity.Warning))
            .Select(diagnostic => diagnostic.ToString()));
        return compilation
            .WithAnalyzers([new LambdaAnalyzer()], new AnalyzerOptions([], new Options(rewrite, severity)))
            .GetAnalyzerDiagnosticsAsync().Result;
    }

    private static string Text(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static async Task<string> Fix(string source, CancellationToken cancellationToken)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Fix",
            "Fix",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable),
            parseOptions: GeneratorHarness.ParseOptions,
            metadataReferences: GeneratorHarness.References));
        var document = workspace.AddDocument(project.Id, "Tests.cs", Microsoft.CodeAnalysis.Text.SourceText.From(source));
        var compilation = (await document.Project.GetCompilationAsync(cancellationToken))!;
        var diagnostics = await compilation.WithAnalyzers([new LambdaAnalyzer()]).GetAnalyzerDiagnosticsAsync(cancellationToken);
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, Assert.Single(diagnostics), (action, _) => actions.Add(action), cancellationToken);

        await new LambdaCodeFix().RegisterCodeFixesAsync(context);

        var operations = await Assert.Single(actions).GetOperationsAsync(cancellationToken);
        var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        return (await solution.GetDocument(document.Id)!.GetTextAsync(cancellationToken)).ToString();
    }

    private sealed class Options(bool rewrite, string? severity) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Values(rewrite, severity);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Values(false, null);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Values(false, null);

        private sealed class Values(bool rewrite, string? severity) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                value = key switch
                {
                    "build_property.VerificationGlueRewrite" when rewrite => "true",
                    LambdaAnalyzer.SeverityProperty => severity,
                    _ => null,
                };
                return value is not null;
            }
        }
    }

    /// <summary>The severity that a <c>dotnet_diagnostic.VG0009.severity</c> line in .editorconfig sets.</summary>
    private sealed class TreeOptions(ReportDiagnostic configured) : SyntaxTreeOptionsProvider
    {
        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) => GeneratedKind.NotGenerated;

        public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
        {
            severity = configured;
            return diagnosticId == "VG0009";
        }

        public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
        {
            severity = ReportDiagnostic.Default;
            return false;
        }
    }
}
