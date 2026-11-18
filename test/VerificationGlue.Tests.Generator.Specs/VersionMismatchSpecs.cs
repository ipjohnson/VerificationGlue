using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerificationGlue.Tests.Generator;

namespace VerificationGlue.Tests.Generator.Specs;

public class VersionMismatchSpecs
{
    private static readonly string GeneratorPackageVersion = typeof(TestGenerator).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    [VerifyTest]
    public void Matching_VerificationGlue_version_reports_nothing()
    {
        var result = GeneratorHarness.Run(new TestGenerator(), "class C { }", VerificationGlueReference(GeneratorPackageVersion));

        Assert.Empty(result.Diagnostics);
    }

    [VerifyTest]
    public void Different_VerificationGlue_version_reports_VGT0007()
    {
        var result = GeneratorHarness.Run(new TestGenerator(), "class C { }", VerificationGlueReference("99.0.0"));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("VGT0007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("VerificationGlue 99.0.0", diagnostic.GetMessage());
        Assert.Contains($"VerificationGlue.Tests {GeneratorPackageVersion}", diagnostic.GetMessage());
    }

    [VerifyTest]
    public void Build_metadata_is_ignored()
    {
        var result = GeneratorHarness.Run(new TestGenerator(), "class C { }", VerificationGlueReference(GeneratorPackageVersion + "+0123abcd"));

        Assert.Empty(result.Diagnostics);
    }

    [VerifyTest]
    public void No_VerificationGlue_reference_reports_nothing()
    {
        var result = GeneratorHarness.Run(new TestGenerator(), "class C { }");

        Assert.Empty(result.Diagnostics);
    }

    private static MetadataReference VerificationGlueReference(string informationalVersion)
    {
        var compilation = CSharpCompilation.Create(
            "VerificationGlue",
            [CSharpSyntaxTree.ParseText($"[assembly: System.Reflection.AssemblyInformationalVersion(\"{informationalVersion}\")]")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
