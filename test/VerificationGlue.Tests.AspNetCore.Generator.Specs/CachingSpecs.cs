using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Tests.AspNetCore.Generator.Specs;

/// <summary>The stage that reads the app's entry point runs once, and not again for an edit to the test project.</summary>
public class CachingSpecs
{
    [Fact]
    public void An_edit_to_the_test_project_does_not_read_the_app_again()
    {
        var app = MetadataReference.CreateFromImage(Shapes.Get("VoidApp").Reference);
        var compilation = GeneratorHarness.Compilation("""[VerificationGlue.Tests.AspNetCore.WebAppEntry("VoidApp")] public sealed partial class VoidAppEntry;""", app);
        var driver = GeneratorHarness.Driver().RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.Equal([IncrementalStepRunReason.New], Reasons(driver, EntryGenerator.AppsStep));

        driver = driver.RunGenerators(
            compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class OrderTests { }", path: "OrderTests.cs", cancellationToken: TestContext.Current.CancellationToken)),
            TestContext.Current.CancellationToken);

        Assert.Equal([IncrementalStepRunReason.Cached], Reasons(driver, EntryGenerator.AppsStep));
        Assert.Single(driver.GetRunResult().GeneratedTrees);
    }

    [Fact]
    public void A_new_build_of_the_app_reads_it_again()
    {
        var shape = Shapes.Get("VoidApp");
        var compilation = GeneratorHarness.Compilation(
            """[VerificationGlue.Tests.AspNetCore.WebAppEntry("VoidApp")] public sealed partial class VoidAppEntry;""",
            MetadataReference.CreateFromImage(shape.Reference));
        var driver = GeneratorHarness.Driver().RunGenerators(compilation, TestContext.Current.CancellationToken);

        driver = driver.RunGenerators(
            compilation.RemoveAllReferences().AddReferences([.. global::Generator.Harness.PlatformReferences.All, MetadataReference.CreateFromImage(shape.Reference)]),
            TestContext.Current.CancellationToken);

        Assert.Equal([IncrementalStepRunReason.Unchanged], Reasons(driver, EntryGenerator.AppsStep));
    }

    private static IncrementalStepRunReason[] Reasons(GeneratorDriver driver, string step) =>
        [.. driver.GetRunResult().Results.Single().TrackedSteps[step].SelectMany(run => run.Outputs).Select(output => output.Reason)];
}
