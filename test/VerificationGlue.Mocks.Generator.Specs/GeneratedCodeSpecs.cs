using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MockSweep;

namespace VerificationGlue.Mocks.Generator.Specs;

/// <summary>
/// Snapshots of the generated mock for <c>IPriceList</c> in <c>samples/Billing</c>.
/// <c>docs/guide/generated/mocks.md</c> shows these snapshots.
/// </summary>
public class GeneratedCodeSpecs
{
    private static readonly Lazy<GeneratorDriverRunResult> Result = new(Generate);

    [VerifyTest]
    public void Mock_for_the_billing_sample() =>
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Mock_for_the_billing_sample), Source("Billing_IPriceList_Mock.g.cs"));

    [VerifyTest]
    public void Registry_for_the_billing_sample() =>
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Registry_for_the_billing_sample), Source(RegistryEmitter.HintName));

    private static string Source(string hintName) =>
        Result.Value.Results.Single().GeneratedSources.Single(source => source.HintName == hintName).SourceText.ToString();

    private static GeneratorDriverRunResult Generate()
    {
        var parseOptions = MockCompiler.ParseOptions.WithFeatures([new("InterceptorsNamespaces", "VerificationGlue.Mocks.Generated")]);
        var billing = Path.Combine(RepositoryRoot(), "samples", "Billing", "Billing.cs");
        var compilation = CSharpCompilation.Create(
            "Billing",
            [
                CSharpSyntaxTree.ParseText(File.ReadAllText(billing), parseOptions, "Billing.cs"),
                CSharpSyntaxTree.ParseText("global using System; global using System.Linq; global using System.Threading.Tasks;", parseOptions, "ImplicitUsings.cs"),
                CSharpSyntaxTree.ParseText("static class Uses { static object Price() => VerificationGlue.Mocks.Mock.Of<Billing.IPriceList>(); }", parseOptions, "Uses.cs"),
            ],
            ShapeCase.FrameworkReferences.Append(MockCompiler.RuntimeReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        // Both tests share the result, so one test's token does not cancel it.
        var driver = CSharpGeneratorDriver.Create([new MockGenerator().AsSourceGenerator()], parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, CancellationToken.None);

        var errors = output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        return driver.GetRunResult();
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VerificationGlue.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No VerificationGlue.slnx above {AppContext.BaseDirectory}.");
    }
}
