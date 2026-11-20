using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MockSweep;
using VerificationGlue.Mocks.Generator;

namespace VerificationGlue.Mocks.Generator.Specs;

public class MockGeneratorSpecs
{
    [VerifyTest]
    public void Runs_without_output_or_diagnostics()
    {
        var result = GeneratorHarness.Run(new MockGenerator(), "class C { }");

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GeneratedTrees);
    }

    // MockCompiler, which ShapeSpecs and the sweep use, writes no registry, so Obsolete.cs did not show this.
    [VerifyTest]
    public void The_registry_of_a_mock_of_a_type_obsolete_as_an_error_compiles(CancellationToken cancellationToken)
    {
        const string Source = """
            using VerificationGlue.Mocks;

            [System.Obsolete("Use INew.", error: true)]
            public interface IOld
            {
                int Value();
            }

            public interface ICurrent
            {
                int Value();
            }

            public static class Uses
            {
                [System.Obsolete("Names IOld.")]
                public static IOld Old() => Mock.Of<IOld>();

                public static ICurrent Current() => Mock.Of<ICurrent>();
            }
            """;
        var parseOptions = MockCompiler.ParseOptions.WithFeatures([new("InterceptorsNamespaces", MockEmitter.GeneratedNamespace)]);
        var compilation = CSharpCompilation.Create(
            "ObsoleteCase",
            [CSharpSyntaxTree.ParseText(Source, parseOptions, "Source.cs")],
            ShapeCase.FrameworkReferences.Append(MockCompiler.RuntimeReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver.Create([new MockGenerator().AsSourceGenerator()], parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, cancellationToken);

        var errors = output.GetDiagnostics(cancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        var registry = output.SyntaxTrees.Single(tree => tree.FilePath.EndsWith(RegistryEmitter.HintName, StringComparison.Ordinal)).ToString();
        Assert.Contains("RegisterObsolete();", registry);
        Assert.Contains("[global::System.Obsolete]", registry);
    }
}
