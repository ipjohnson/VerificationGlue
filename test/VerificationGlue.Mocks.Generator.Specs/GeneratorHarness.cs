using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Mocks.Generator.Specs;

internal static class GeneratorHarness
{
    public static GeneratorDriverRunResult Run(IIncrementalGenerator generator, string source, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
            "SpecsInput",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location), .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(generator).RunGenerators(compilation).GetRunResult();
    }
}
