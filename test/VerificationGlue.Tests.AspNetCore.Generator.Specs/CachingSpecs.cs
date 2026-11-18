using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Tests.AspNetCore.Generator.Specs;

/// <summary>
/// Which stages of <see cref="EntryGenerator"/> run again after an edit, as docs/design/aspnet.md
/// asks under Cost in the IDE.
/// </summary>
public class CachingSpecs
{
    private const string Entry = """[VerificationGlue.Tests.AspNetCore.WebAppEntry("VoidApp")] public sealed partial class VoidAppEntry;""";

    [VerifyTest]
    public void An_edit_to_the_test_project_does_not_read_the_app_again()
    {
        var harness = Pipeline();
        harness.Run();
        Assert.Equal([IncrementalStepRunReason.New], harness.StepReasons(EntryGenerator.AppsStep));

        harness.WithSource("OrderTests.cs", "public class OrderTests { }").Run();
        Assert.Equal([IncrementalStepRunReason.Cached], harness.StepReasons(EntryGenerator.AppsStep));

        harness.WithSource("OrderTests.cs", "public class OrderTests { public void Places_an_order() { } }").Run();
        Assert.Equal([IncrementalStepRunReason.Cached], harness.StepReasons(EntryGenerator.AppsStep));
        Assert.Equal([IncrementalStepRunReason.Unchanged], harness.StepReasons(EntryGenerator.EntriesStep));
        Assert.Single(harness.LastRun.GeneratedTrees);
    }

    [VerifyTest]
    public void An_edit_to_the_app_reads_it_once()
    {
        var harness = Pipeline();
        harness.Run();

        harness.WithReference("VoidApp", AppReference("public sealed class OrderService { }")).Run();
        Assert.Equal([IncrementalStepRunReason.Unchanged], harness.StepReasons(EntryGenerator.AppsStep));
        Assert.Single(harness.LastRun.GeneratedTrees);

        harness.WithSource("OrderTests.cs", "public class OrderTests { }").Run();
        Assert.Equal([IncrementalStepRunReason.Cached], harness.StepReasons(EntryGenerator.AppsStep));
    }

    [VerifyTest]
    public void An_edit_that_changes_the_entry_point_writes_the_entry_type_again()
    {
        var harness = Pipeline();
        harness.Run();

        harness.WithReference("VoidApp", MetadataReference.CreateFromImage(Shape.Compile("VoidApp", ["return 3;"], OutputKind.ConsoleApplication).Reference)).Run();

        Assert.Equal([IncrementalStepRunReason.Modified], harness.StepReasons(EntryGenerator.AppsStep));
        Assert.Contains("return EntryPoint(null, args);", harness.LastRun.GeneratedTrees.Single().ToString(), StringComparison.Ordinal);
    }

    private static GeneratorDriverHarness Pipeline() =>
        GeneratorHarness.Pipeline()
            .WithSource("Entry.cs", Entry)
            .WithReference("VoidApp", MetadataReference.CreateFromImage(Shapes.Get("VoidApp").Reference));

    private static MetadataReference AppReference(string extraSource) =>
        MetadataReference.CreateFromImage(Shape.Compile("VoidApp", [.. Shapes.Get("VoidApp").Sources, extraSource], OutputKind.ConsoleApplication).Reference);
}
