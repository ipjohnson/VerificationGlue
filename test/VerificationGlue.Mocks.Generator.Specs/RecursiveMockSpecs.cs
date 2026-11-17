using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MockSweep;

namespace VerificationGlue.Mocks.Generator.Specs;

/// <summary>The mocks the generator adds for interface-typed returns, and how far it goes.</summary>
public class RecursiveMockSpecs
{
    private const string Source = """
        using VerificationGlue.Mocks;

        namespace App;

        public interface IShop
        {
            IOrders Orders { get; }
            System.IServiceProvider Services { get; }
        }

        public interface IOrders
        {
            IOrder Find(int id);
        }

        public interface IOrder
        {
            ICustomer Customer { get; }
        }

        public interface ICustomer
        {
            string Name { get; }
        }

        public class Tests
        {
            public object Run() => Mock.Of<IShop>();
        }
        """;

    [Fact]
    public void The_default_depth_generates_mocks_two_steps_from_the_named_type()
    {
        var sources = Generate(depth: null);

        Assert.Equal(["App_IOrder_Mock", "App_IOrders_Mock", "App_IShop_Mock"], sources.Keys.Where(name => name.EndsWith("_Mock", StringComparison.Ordinal)).Order());
        Assert.Contains("CallMock<global::App.IOrders>", sources["App_IShop_Mock"]);
        Assert.Contains("CallMock<global::App.IOrder>", sources["App_IOrders_Mock"]);
        Assert.Contains("Call<global::App.ICustomer>", sources["App_IOrder_Mock"]);
        Assert.Contains("Call<global::System.IServiceProvider>", sources["App_IShop_Mock"]);
    }

    [Fact]
    public void VerificationGlueMocksRecursionDepth_sets_how_far_the_generator_goes()
    {
        Assert.Equal(["App_IShop_Mock"], Generate(depth: "0").Keys.Where(name => name.EndsWith("_Mock", StringComparison.Ordinal)));
        Assert.Equal(4, Generate(depth: "3").Keys.Count(name => name.EndsWith("_Mock", StringComparison.Ordinal)));
    }

    private static Dictionary<string, string> Generate(string? depth)
    {
        var parseOptions = MockCompiler.ParseOptions.WithFeatures([new("InterceptorsNamespaces", MockEmitter.GeneratedNamespace)]);
        var compilation = CSharpCompilation.Create(
            "RecursiveCase",
            [CSharpSyntaxTree.ParseText(Source, parseOptions, "Source.cs")],
            ShapeCase.FrameworkReferences.Append(MockCompiler.RuntimeReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var properties = new BuildProperties(depth is null ? [] : new() { ["VerificationGlueMocksRecursionDepth"] = depth });

        CSharpGeneratorDriver.Create([new MockGenerator().AsSourceGenerator()], parseOptions: parseOptions, optionsProvider: properties)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);

        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));

        return output.SyntaxTrees
            .Where(tree => tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal))
            .ToDictionary(tree => Path.GetFileName(tree.FilePath)[..^".g.cs".Length], tree => tree.ToString());
    }
}
