using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Tests.AspNetCore.Generator.Specs;

public class DiagnosticSpecs
{
    [VerifyTest]
    public void An_assembly_the_project_does_not_reference_is_VGA0001_with_the_referenced_assemblies()
    {
        var source = """[VerificationGlue.Tests.AspNetCore.WebAppEntry("ShopApi")] public sealed partial class ShopApiEntry;""";
        var diagnostic = SingleDiagnostic(
            source,
            MetadataReference.CreateFromImage(Shapes.Get("VoidApp").Reference),
            MetadataReference.CreateFromImage(Shapes.Get("IntApp").Reference));

        Assert.Equal("VGA0001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "[WebAppEntry] names the assembly 'ShopApi', but the project does not reference it. It references these assemblies outside .NET and VerificationGlue: IntApp, VoidApp",
            diagnostic.GetMessage());
        Assert.Equal("""VerificationGlue.Tests.AspNetCore.WebAppEntry("ShopApi")""", Text(source, diagnostic));
    }

    [VerifyTest]
    public void An_assembly_with_no_entry_point_is_VGA0001()
    {
        var library = Library("Clock", "public interface IClock { System.DateTime Now { get; } } public static class Clocks { public static void Main(int seconds) { } }");

        var diagnostic = SingleDiagnostic("""[VerificationGlue.Tests.AspNetCore.WebAppEntry("Clock")] public sealed partial class ClockEntry;""", library);

        Assert.Equal("VGA0001", diagnostic.Id);
        Assert.Equal(
            "[WebAppEntry] names the assembly 'Clock', but it has no static method named Main, <Main> or <Main>$ with an entry point signature",
            diagnostic.GetMessage());
    }

    [VerifyTest]
    public void An_assembly_with_entry_points_in_two_types_is_VGA0001_with_each_entry_point()
    {
        var library = Library(
            "Twice",
            "namespace Twice; public static class Server { public static void Main() { } } internal static class Worker { private static int Main(string[] args) => 0; }");

        var diagnostic = SingleDiagnostic("""[VerificationGlue.Tests.AspNetCore.WebAppEntry("Twice")] public sealed partial class TwiceEntry;""", library);

        Assert.Equal("VGA0001", diagnostic.Id);
        Assert.Equal(
            "[WebAppEntry] names the assembly 'Twice', but more than one type in it has an entry point: Twice.Server.Main() returning void, Twice.Worker.Main(string[]) returning int",
            diagnostic.GetMessage());
    }

    [VerifyTest]
    [Row("public sealed class ShopEntry;", "ShopEntry", "is not partial")]
    [Row("public sealed partial class ShopEntry<T>;", "ShopEntry", "is generic")]
    [Row("public static partial class Outer { [WebAppEntry(\"VoidApp\")] public sealed partial class ShopEntry; }", "ShopEntry", "is nested in 'Specs.Outer'")]
    public void A_class_the_generator_cannot_add_to_is_VGA0002(string declaration, string name, string reason)
    {
        var source = "namespace Specs; using VerificationGlue.Tests.AspNetCore; " +
            (declaration.Contains("Outer", StringComparison.Ordinal) ? declaration : "[WebAppEntry(\"VoidApp\")] " + declaration);

        var diagnostic = SingleDiagnostic(source, MetadataReference.CreateFromImage(Shapes.Get("VoidApp").Reference));

        Assert.Equal("VGA0002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            $"The class '{name}' has [WebAppEntry], but it {reason}. The generator adds the entry type only to a partial class that is not generic and not nested.",
            diagnostic.GetMessage());
        Assert.Equal(name, Text(source, diagnostic));
    }

    private static Diagnostic SingleDiagnostic(string source, params MetadataReference[] references)
    {
        var (run, _) = GeneratorHarness.Run(source, references);

        Assert.Empty(run.GeneratedTrees);
        return Assert.Single(run.Diagnostics);
    }

    /// <summary>The text the diagnostic points at. The generator reports in Entry.cs, the file of the spec's source.</summary>
    private static string Text(string source, Diagnostic diagnostic)
    {
        Assert.Equal("Entry.cs", diagnostic.Location.GetLineSpan().Path);
        return source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
    }

    private static MetadataReference Library(string name, string source) =>
        MetadataReference.CreateFromImage(Shape.Compile(name, [source], OutputKind.DynamicallyLinkedLibrary).Full);
}
