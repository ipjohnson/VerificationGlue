using Generator.Harness;
using Microsoft.CodeAnalysis;

namespace VerificationGlue.Tests.AspNetCore.Generator.Specs;

/// <summary>
/// The entry type for each shape in <see cref="Shapes"/>. A build compiles the test project against the
/// app's reference assembly, and the IDE can see the full assembly; for an async shape the two hold
/// different entry point methods. Both entry types call the app when it runs.
/// docs/guide/generated/web-app-entry.md shows the snapshots.
/// </summary>
public class EntryTypeSpecs
{
    public static TheoryData<string> Names => Shapes.Names;

    [Theory]
    [MemberData(nameof(Names))]
    public void Entry_type_from_the_reference_assembly(string name)
    {
        var shape = Shapes.Get(name);

        var source = GenerateAndRun(shape, shape.Reference);

        Snapshot.Match(typeof(EntryTypeSpecs), "Entry_type_for_" + name, source);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Entry_type_from_the_full_assembly(string name)
    {
        var shape = Shapes.Get(name);

        GenerateAndRun(shape, shape.Full);
    }

    [Fact]
    public void Entry_type_in_the_global_namespace()
    {
        var shape = Shapes.Get("VoidApp");

        var (run, _) = GeneratorHarness.Run(
            """[VerificationGlue.Tests.AspNetCore.WebAppEntry("VoidApp")] public sealed partial class VoidAppEntry;""",
            MetadataReference.CreateFromImage(shape.Reference));

        var generated = Assert.Single(run.GeneratedTrees);
        Assert.EndsWith("VoidAppEntry.WebAppEntry.g.cs", generated.FilePath);
        Assert.DoesNotContain("namespace", generated.ToString());
        Assert.Empty(run.Diagnostics);
    }

    /// <summary>
    /// Generates the entry type of <paramref name="shape"/> from <paramref name="appImage"/>, compiles it,
    /// calls its Run with the app's full assembly loaded, and returns the generated source.
    /// </summary>
    private static string GenerateAndRun(Shape shape, byte[] appImage)
    {
        var (run, output) = GeneratorHarness.Run(
            $$"""
            namespace Specs;

            [VerificationGlue.Tests.AspNetCore.WebAppEntry("{{shape.Name}}")]
            public sealed partial class {{shape.Name}}Entry;
            """,
            MetadataReference.CreateFromImage(appImage));
        Assert.Empty(run.Diagnostics);
        var errors = output.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));

        var context = new ShapeLoadContext(new() { [shape.Name] = shape.Full });
        try
        {
            var entry = context.LoadFromStream(new MemoryStream(GeneratorHarness.Emit(output))).GetType("Specs." + shape.Name + "Entry", throwOnError: true)!;
            AppContext.SetData(shape.Name, null);

            var applicationName = entry.GetProperty("ApplicationName")!.GetValue(null);
            var exitCode = entry.GetMethod("Run")!.Invoke(null, [new[] { "--applicationName=" + shape.Name, "--a=1" }]);

            Assert.Equal(shape.Name, applicationName);
            Assert.Equal(shape.ExpectedRun, exitCode);
            Assert.Equal($"--applicationName={shape.Name} --a=1", AppContext.GetData(shape.Name));
        }
        finally
        {
            context.Unload();
        }

        return Assert.Single(run.GeneratedTrees).ToString();
    }
}
