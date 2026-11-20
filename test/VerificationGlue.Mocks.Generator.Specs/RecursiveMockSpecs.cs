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

    [VerifyTest]
    public void The_default_depth_generates_mocks_two_steps_from_the_named_type(CancellationToken cancellationToken)
    {
        var sources = Generate(depth: null, cancellationToken);

        Assert.Equal(["App_IOrder_Mock", "App_IOrders_Mock", "App_IShop_Mock"], sources.Keys.Where(name => name.EndsWith("_Mock", StringComparison.Ordinal)).Order());
        Assert.Contains("CallMock<global::App.IOrders>", sources["App_IShop_Mock"]);
        Assert.Contains("CallMock<global::App.IOrder>", sources["App_IOrders_Mock"]);
        Assert.Contains("Call<global::App.ICustomer>", sources["App_IOrder_Mock"]);
        Assert.Contains("Call<global::System.IServiceProvider>", sources["App_IShop_Mock"]);
    }

    [VerifyTest]
    public void VerificationGlueMocksRecursionDepth_sets_how_far_the_generator_goes(CancellationToken cancellationToken)
    {
        Assert.Equal(["App_IShop_Mock"], Generate(depth: "0", cancellationToken).Keys.Where(name => name.EndsWith("_Mock", StringComparison.Ordinal)));
        Assert.Equal(4, Generate(depth: "3", cancellationToken).Keys.Count(name => name.EndsWith("_Mock", StringComparison.Ordinal)));
    }

    private const string ReturnsSource = """
        using System.Threading.Tasks;
        using VerificationGlue.Mocks;

        namespace App;

        public interface IStore
        {
            IOrders Orders { get; }
            IOrders Find(int id);
            Task<IOrders> OrdersAsync();
            ValueTask<IOrders> PeekAsync();
            System.IServiceProvider Services { get; }
        }

        public interface IOrders
        {
            int Count { get; }
        }

        public static class Probe
        {
            public static object?[] Read()
            {
                var store = Mock.Of<IStore>();
                var task = store.OrdersAsync();
                var valueTask = store.PeekAsync();
                return [store.Orders, store.Find(1), task.IsCompletedSuccessfully, task.Result, valueTask.IsCompletedSuccessfully, valueTask.Result, store.Services];
            }
        }
        """;

    [VerifyTest]
    public void A_task_or_System_interface_return_gets_no_recursive_mock_and_depth_0_returns_default(CancellationToken cancellationToken)
    {
        var atDefault = Read(depth: null, cancellationToken);
        var atZero = Read(depth: "0", cancellationToken);

        Assert.IsAssignableFrom<IMock>(atDefault[0]);
        Assert.IsAssignableFrom<IMock>(atDefault[1]);
        Assert.Equal(new object?[] { true, null, true, null, null }, atDefault[2..]);
        Assert.Equal(new object?[] { null, null, true, null, true, null, null }, atZero);
    }

    private static object?[] Read(string? depth, CancellationToken cancellationToken)
    {
        var output = Compile(ReturnsSource, depth, cancellationToken);
        using var image = new MemoryStream();
        var emit = output.Emit(image, cancellationToken: cancellationToken);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        var probe = System.Reflection.Assembly.Load(image.ToArray()).GetType("App.Probe")!;
        return (object?[])probe.GetMethod("Read")!.Invoke(null, null)!;
    }

    private static Dictionary<string, string> Generate(string? depth, CancellationToken cancellationToken) =>
        Compile(Source, depth, cancellationToken).SyntaxTrees
            .Where(tree => tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal))
            .ToDictionary(tree => Path.GetFileName(tree.FilePath)[..^".g.cs".Length], tree => tree.ToString());

    private static Compilation Compile(string source, string? depth, CancellationToken cancellationToken)
    {
        var parseOptions = MockCompiler.ParseOptions.WithFeatures([new("InterceptorsNamespaces", MockEmitter.GeneratedNamespace)]);
        var compilation = CSharpCompilation.Create(
            "RecursiveCase" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parseOptions, "Source.cs")],
            ShapeCase.FrameworkReferences.Append(MockCompiler.RuntimeReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var properties = new BuildProperties(depth is null ? [] : new() { ["VerificationGlueMocksRecursionDepth"] = depth });

        CSharpGeneratorDriver.Create([new MockGenerator().AsSourceGenerator()], parseOptions: parseOptions, optionsProvider: properties)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, cancellationToken);

        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));

        return output;
    }
}
