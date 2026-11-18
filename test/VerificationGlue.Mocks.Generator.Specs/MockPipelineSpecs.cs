using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MockSweep;
using static Microsoft.CodeAnalysis.IncrementalStepRunReason;

namespace VerificationGlue.Mocks.Generator.Specs;

/// <summary>
/// The pipeline layer: each row of the "Interface changes" table in mocks.md, checked with the step
/// reasons of a generator driver that is kept between runs, and output that is the same whatever
/// order the sources arrive in.
/// </summary>
public class MockPipelineSpecs
{
    private const string Contracts = """
        namespace App;

        public interface ICalculator
        {
            int Add(int a, int b);
        }
        """;

    private const string Tests = """
        using VerificationGlue.Mocks;

        namespace App;

        public class Tests
        {
            public void Run()
            {
                var calc = Mock.Of<ICalculator>();
                calc.Add(0, Arg.Any<int>()).Returns(3);
                var gateway = Mock.Of<Vendor.IGateway>();
                gateway.Send(Arg.Any<string>());
            }
        }
        """;

    private const string GatewayV1 = """
        namespace Vendor;

        public interface IGateway
        {
            void Send(string message);
        }

        public static class Defaults
        {
            public static int Retries() => 1;
        }
        """;

    private static readonly CSharpParseOptions ParseOptions = MockCompiler.ParseOptions
        .WithFeatures([new("InterceptorsNamespaces", MockEmitter.GeneratedNamespace)]);

    [VerifyTest]
    public void An_unrelated_edit_regenerates_nothing()
    {
        var harness = Harness();

        harness.WithSource("Unrelated.cs", "namespace App; public class Unrelated { public int Value => 2; }").Run();

        AssertNotRegenerated(harness, MockGenerator.MocksStep, MockGenerator.RegistryStep, MockGenerator.ArgFilesStep);
    }

    [VerifyTest]
    public void An_edit_above_an_Arg_call_regenerates_its_interceptors_and_not_the_mocks()
    {
        var harness = Harness();

        harness.WithSource("Tests.cs", "// A new first line moves every call below it.\n" + Tests).Run();

        AssertNotRegenerated(harness, MockGenerator.MocksStep, MockGenerator.RegistryStep);
        Assert.Contains(Modified, harness.StepReasons(MockGenerator.ArgFilesStep));
        Assert.Empty(harness.OutputCompilationDiagnostics());
    }

    [VerifyTest]
    public void A_member_added_to_an_interface_in_the_project_regenerates_its_mock()
    {
        var harness = Harness();

        harness.WithSource("Contracts.cs", Contracts.Replace("int Add(int a, int b);", "int Add(int a, int b);\n    void Reset();")).Run();

        Assert.Equal([Modified, Unchanged], harness.StepReasons(MockGenerator.MocksStep).Order());
        Assert.Contains("Reset", MockSource(harness, "App_ICalculator_Mock"));
        Assert.Empty(harness.OutputCompilationDiagnostics());
    }

    [VerifyTest]
    public void A_member_added_in_a_referenced_project_regenerates_its_mock()
    {
        var harness = Harness();

        harness.WithReference("Vendor", Library(GatewayV1.Replace("void Send(string message);", "void Send(string message);\n    void Close();")).ToMetadataReference()).Run();

        Assert.Equal([Modified, Unchanged], harness.StepReasons(MockGenerator.MocksStep).Order());
        Assert.Contains("Close", MockSource(harness, "Vendor_IGateway_Mock"));
        Assert.Empty(harness.OutputCompilationDiagnostics());
    }

    [VerifyTest]
    public void A_method_body_changed_in_a_referenced_project_regenerates_nothing()
    {
        var harness = Harness();

        harness.WithReference("Vendor", Library(GatewayV1.Replace("=> 1;", "=> 2;")).ToMetadataReference()).Run();

        AssertNotRegenerated(harness, MockGenerator.MocksStep, MockGenerator.RegistryStep, MockGenerator.ArgFilesStep);
    }

    [VerifyTest]
    public void A_referenced_DLL_replaced_with_a_new_member_regenerates_its_mock()
    {
        var harness = Harness(image: true);

        harness.WithReference("Vendor", Image(Library(GatewayV1.Replace("void Send(string message);", "void Send(string message);\n    int Pending { get; }")))).Run();

        Assert.Equal([Modified, Unchanged], harness.StepReasons(MockGenerator.MocksStep).Order());
        Assert.Contains("Pending", MockSource(harness, "Vendor_IGateway_Mock"));
        Assert.Empty(harness.OutputCompilationDiagnostics());
    }

    /// <summary>
    /// The stale-model regression. A model that compares too little, such as by name alone, keeps the
    /// old mock after the interface changes, and the output fails with CS0535 in a long-lived IDE
    /// process. Three changes in a row each have to reach the generated code.
    /// </summary>
    [VerifyTest]
    public void A_mock_never_goes_stale_across_successive_interface_changes()
    {
        var harness = Harness();
        string[] members = ["void Reset();", "string Name { get; set; }", "event System.EventHandler? Changed;"];
        var contracts = Contracts;

        foreach (var member in members)
        {
            contracts = contracts.Replace("int Add(int a, int b);", "int Add(int a, int b);\n    " + member);
            harness.WithSource("Contracts.cs", contracts).Run();

            Assert.DoesNotContain(harness.OutputCompilationDiagnostics(), diagnostic => diagnostic.Id == "CS0535");
            Assert.Empty(harness.OutputCompilationDiagnostics());
        }
    }

    [VerifyTest]
    public void Output_is_the_same_whatever_order_the_sources_arrive_in()
    {
        var forward = new GeneratorDriverHarness(new MockGenerator(), References, ParseOptions)
            .WithSource("Contracts.cs", Contracts)
            .WithSource("Tests.cs", Tests)
            .WithSource("More.cs", "using VerificationGlue.Mocks; namespace App; public interface IClock { } public class More { object M() => Mock.Of<IClock>(); }");
        var backward = new GeneratorDriverHarness(new MockGenerator(), References, ParseOptions)
            .WithSource("More.cs", "using VerificationGlue.Mocks; namespace App; public interface IClock { } public class More { object M() => Mock.Of<IClock>(); }")
            .WithSource("Tests.cs", Tests)
            .WithSource("Contracts.cs", Contracts);
        forward.WithReference("Vendor", Library(GatewayV1).ToMetadataReference());
        backward.WithReference("Vendor", Library(GatewayV1).ToMetadataReference());

        Assert.Equal(Outputs(forward.Run()), Outputs(backward.Run()));
        Assert.Empty(forward.OutputCompilationDiagnostics());
    }

    private static GeneratorDriverHarness Harness(bool image = false)
    {
        var harness = new GeneratorDriverHarness(new MockGenerator(), References, ParseOptions)
            .WithSource("Contracts.cs", Contracts)
            .WithSource("Tests.cs", Tests);
        var library = Library(GatewayV1);
        harness.WithReference("Vendor", image ? Image(library) : library.ToMetadataReference());
        harness.Run();
        Assert.Empty(harness.OutputCompilationDiagnostics());
        Assert.Equal(
            ["App_ICalculator_Mock.g.cs", "Tests.ArgInterceptors.g.cs", "Vendor_IGateway_Mock.g.cs", "VerificationGlue.Mocks.Registry.g.cs"],
            Outputs(harness.LastRun).Select(output => output.HintName.StartsWith("Tests.", StringComparison.Ordinal) ? "Tests.ArgInterceptors.g.cs" : output.HintName));
        return harness;
    }

    private static readonly MetadataReference[] References = [.. PlatformReferences.All, MockCompiler.RuntimeReference];

    private static CSharpCompilation Library(string source) =>
        CSharpCompilation.Create(
            "Vendor.Contracts",
            [CSharpSyntaxTree.ParseText(source, MockCompiler.ParseOptions)],
            PlatformReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    private static MetadataReference Image(CSharpCompilation library)
    {
        using var stream = new MemoryStream();
        var result = library.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static void AssertNotRegenerated(GeneratorDriverHarness harness, params string[] steps)
    {
        foreach (var step in steps)
        {
            var reasons = harness.StepReasons(step);
            Assert.NotEmpty(reasons);
            Assert.All(reasons, reason => Assert.True(reason is Cached or Unchanged, $"{step} ran with reason {reason}"));
        }
    }

    private static string MockSource(GeneratorDriverHarness harness, string mockName) =>
        harness.LastRun.Results.Single().GeneratedSources.Single(source => source.HintName == mockName + ".g.cs").SourceText.ToString();

    private static List<(string HintName, string Text)> Outputs(GeneratorDriverRunResult run) =>
        [.. run.Results.Single().GeneratedSources.Select(source => (source.HintName, source.SourceText.ToString())).OrderBy(output => output.HintName, StringComparer.Ordinal)];
}
