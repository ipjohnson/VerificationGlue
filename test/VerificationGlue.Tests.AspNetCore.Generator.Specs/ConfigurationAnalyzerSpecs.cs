using System.Collections.Immutable;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Tests.AspNetCore.Generator.Specs;

public class ConfigurationAnalyzerSpecs
{
    /// <summary>
    /// The types of VerificationGlue.Tests that the analyzer reads, with the members it reads. The specs
    /// cannot reference that project, for the reason <see cref="GeneratorHarness"/> gives.
    /// </summary>
    private const string Core = """
        namespace VerificationGlue.Tests
        {
            public enum ContainerScope { Test, Class, Assembly }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class VerifyTestAttribute : System.Attribute;

            [System.AttributeUsage(System.AttributeTargets.Assembly | System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public abstract class ContainerAttribute : System.Attribute
            {
                public ContainerScope Scope { get; set; }
            }

            public class ContainerAttribute<T> : ContainerAttribute;
        }

        namespace VerificationGlue.Tests.AspNetCore
        {
            public sealed class WebAppBootstrap<TEntry>;

            public sealed class WebAppAttribute<TEntry> : VerificationGlue.Tests.ContainerAttribute<WebAppBootstrap<TEntry>>;

            public sealed class ShopApiEntry;
        }
        """;

    private const string Usings = "using VerificationGlue.Tests; using VerificationGlue.Tests.AspNetCore; ";

    [VerifyTest]
    [Row("""AppSetting("Orders:MaxQuantity", "5")""", "AppSetting", "Class", "the class or the assembly")]
    [Row("""AppEnvironment("Staging")""", "AppEnvironment", "Class", "the class or the assembly")]
    [Row("Kestrel", "Kestrel", "Class", "the class or the assembly")]
    [Row("WatchAppConfiguration", "WatchAppConfiguration", "Class", "the class or the assembly")]
    [Row("Kestrel", "Kestrel", "Assembly", "the assembly")]
    public void A_configuration_attribute_on_a_test_method_under_a_shared_app_is_VGA0003(string attribute, string name, string scope, string place)
    {
        var source = Usings + (scope == "Assembly"
            ? "[assembly: WebApp<ShopApiEntry>(Scope = ContainerScope.Assembly)] public class OrderTests { [VerifyTest] [" + attribute + "] public void Rejects_a_large_order() { } }"
            : "[WebApp<ShopApiEntry>(Scope = ContainerScope.Class)] public class OrderTests { [VerifyTest] [" + attribute + "] public void Rejects_a_large_order() { } }");

        var diagnostic = Assert.Single(Analyze(source));

        Assert.Equal("VGA0003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            $"The test 'Rejects_a_large_order' has [{name}], but its app [WebApp<VerificationGlue.Tests.AspNetCore.ShopApiEntry>] is shared with Scope = {scope} and starts before the test, so it does not read the test's attributes. Move [{name}] to {place}, or give the app test scope.",
            diagnostic.GetMessage());
        Assert.Equal(attribute, source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [VerifyTest]
    public void Each_setting_on_the_method_is_its_own_VGA0003()
    {
        var source = Usings + """[WebApp<ShopApiEntry>(Scope = ContainerScope.Class)] public class OrderTests { [VerifyTest] [AppSetting("A", "1")] [AppSetting("B", "2")] public void Rejects_a_large_order() { } }""";

        var diagnostics = Analyze(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal("VGA0003", diagnostic.Id));
    }

    [VerifyTest]
    [Row("""[WebApp<ShopApiEntry>(Scope = ContainerScope.Class)] [AppSetting("A", "1")] [Kestrel] public class OrderTests { [VerifyTest] public void Test() { } }""")]
    [Row("""[assembly: WebApp<ShopApiEntry>(Scope = ContainerScope.Assembly)] [assembly: AppEnvironment("Staging")] [WatchAppConfiguration] public class OrderTests { [VerifyTest] public void Test() { } }""")]
    [Row("""[WebApp<ShopApiEntry>] public class OrderTests { [VerifyTest] [AppSetting("A", "1")] [AppEnvironment("Staging")] [Kestrel] [WatchAppConfiguration] public void Test() { } }""")]
    [Row("""[WebApp<ShopApiEntry>(Scope = ContainerScope.Test)] public class OrderTests { [VerifyTest] [Kestrel] public void Test() { } }""")]
    [Row("""[WebApp<ShopApiEntry>(Scope = ContainerScope.Class)] public class OrderTests { [VerifyTest] [WebApp<ShopApiEntry>] [Kestrel] public void Test() { } }""")]
    public void A_configuration_attribute_the_app_reads_is_not_reported(string source)
    {
        Assert.Empty(Analyze(Usings + source));
    }

    private static ImmutableArray<Diagnostic> Analyze(string source)
    {
        var configuration = File.ReadAllText(Path.Combine(GeneratorHarness.RepositoryRoot, "src", "VerificationGlue.Tests.AspNetCore", "AppConfigurationAttributes.cs"));
        var compilation = CSharpCompilation.Create(
            "SpecsInput",
            [
                CSharpSyntaxTree.ParseText(source, path: "Tests.cs"),
                CSharpSyntaxTree.ParseText(Core, path: "Core.cs"),
                CSharpSyntaxTree.ParseText(configuration, path: "AppConfigurationAttributes.cs"),
            ],
            PlatformReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        return compilation
            .WithAnalyzers([new ConfigurationAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
