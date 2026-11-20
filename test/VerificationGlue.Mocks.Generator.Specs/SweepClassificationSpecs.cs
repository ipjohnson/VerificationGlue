using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MockSweep;

namespace VerificationGlue.Mocks.Generator.Specs;

public class SweepClassificationSpecs
{
    private const string Widget = "public interface IWidget { int Size(); }";

    [VerifyTest]
    public void An_interface_that_was_compiled_and_is_now_refused_fails_the_sweep()
    {
        var pinned = Pin(Sweep(Widget));
        var now = Sweep("public interface IWidget { static abstract IWidget Create(); int Size(); }");

        var changes = SweepClassifications.Compare(pinned, SweepClassifications.Of(now));

        Assert.Equal([new ClassificationChange("Case", "Widgets.IWidget", "compiled", "refused")], changes.Regressions);
        Assert.Empty(changes.Improvements);
        Assert.True(new SweepReport(now, []).Passed);
        Assert.False(new SweepReport(now, [], changes).Passed);
        Assert.Contains("- Case: `Widgets.IWidget`, compiled to refused", new SweepReport(now, [], changes).Markdown());
    }

    [VerifyTest]
    public void An_interface_whose_members_start_to_throw_is_a_regression()
    {
        var changes = SweepClassifications.Compare(
            Pin(Sweep(Widget)),
            SweepClassifications.Of(Sweep("public interface IWidget { int Size(); int Parse(System.ReadOnlySpan<char> text); }")));

        Assert.Equal([new ClassificationChange("Case", "Widgets.IWidget", "compiled", "compiled with VGM0003")], changes.Regressions);
    }

    [VerifyTest]
    public void The_same_outcomes_pass_and_other_changes_are_listed_apart_from_regressions()
    {
        var pinned = Pin(Sweep(Widget + "public interface IGone { void Run(); } public interface IFactory { static abstract IFactory Create(); }"));
        var now = Sweep(Widget + "public interface IFactory { IFactory Create(); } public interface INew { void Run(); }");

        Assert.True(SweepClassifications.Compare(pinned, pinned).None);
        var changes = SweepClassifications.Compare(pinned, SweepClassifications.Of(now));

        Assert.Empty(changes.Regressions);
        Assert.Equal([new ClassificationChange("Case", "Widgets.IFactory", "refused", "compiled")], changes.Improvements);
        Assert.Equal([new ClassificationChange("Case", "Widgets.INew", null, "compiled")], changes.Added);
        Assert.Equal([new ClassificationChange("Case", "Widgets.IGone", "compiled", null)], changes.Removed);
        Assert.False(new SweepReport(now, [], changes).Passed);
    }

    [VerifyTest]
    public void The_checked_in_classifications_are_in_the_written_form()
    {
        var path = Path.Combine(ShapeCase.Directory, "..", "..", "MockSweep", "classifications.tsv");
        var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);

        var classifications = SweepClassifications.Read(text);

        Assert.Equal(text, SweepClassifications.Write(classifications));
        Assert.Contains(classifications, pair => pair.Value == SweepClassifications.Refused);
    }

    private static IReadOnlyDictionary<(string Source, string Type), string> Pin(IReadOnlyList<SourceResult> sweep) =>
        SweepClassifications.Read(SweepClassifications.Write(SweepClassifications.Of(sweep)));

    private static IReadOnlyList<SourceResult> Sweep(string declarations)
    {
        var tree = CSharpSyntaxTree.ParseText("namespace Widgets;\n" + declarations, MockCompiler.ParseOptions);
        var compilation = CSharpCompilation.Create(
            "SweepCase",
            [tree],
            ShapeCase.FrameworkReferences.Append(MockCompiler.RuntimeReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var model = compilation.GetSemanticModel(tree);
        var interfaces = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax>()
            .Select(declaration => model.GetDeclaredSymbol(declaration)!)
            .ToList();
        var outcomes = MockCompiler.Compile(compilation, interfaces, out var otherErrors, out _);
        Assert.Empty(otherErrors);
        return [new SourceResult("Case", ["SweepCase"], outcomes, otherErrors, TimeSpan.Zero)];
    }
}
