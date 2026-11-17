using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using VerificationGlue.CodeFixes;

namespace VerificationGlue.Generator.Specs;

public class MakePartialCodeFixSpecs
{
    [Fact]
    public async Task The_fix_for_VG0005_makes_every_containing_type_partial()
    {
        var source = """
            using VerificationGlue;
            using static VerificationGlue.VerifySyntax;

            public class Outer
            {
                [System.Obsolete]
                public static class Tests
                {
                    [Verify]
                    public static void M(int x) => Verify(x == 1);
                }
            }
            """;

        var fixedSource = await Fix(source);

        Assert.Equal(source.Replace("public class Outer", "public partial class Outer").Replace("public static class Tests", "public static partial class Tests"), fixedSource);
        Assert.DoesNotContain(await Diagnostics(fixedSource), diagnostic => diagnostic.Id == "VG0005");
    }

    [Fact]
    public async Task The_fix_adds_partial_to_a_type_with_no_modifiers()
    {
        var source = "using VerificationGlue;\nusing static VerificationGlue.VerifySyntax;\n\nclass Tests\n{\n    [Verify]\n    static void M(int x) => Verify(x == 1);\n}\n";

        Assert.Equal(source.Replace("class Tests", "partial class Tests"), await Fix(source));
    }

    private static async Task<string> Fix(string source)
    {
        var document = Document(source);
        var diagnostic = Assert.Single(await Diagnostics(document), diagnostic => diagnostic.Id == "VG0005");
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), TestContext.Current.CancellationToken);

        await new MakePartialCodeFix().RegisterCodeFixesAsync(context);

        var operations = await Assert.Single(actions).GetOperationsAsync(TestContext.Current.CancellationToken);
        var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        return (await solution.GetDocument(document.Id)!.GetTextAsync(TestContext.Current.CancellationToken)).ToString();
    }

    private static Task<ImmutableArray<Diagnostic>> Diagnostics(string source) => Diagnostics(Document(source));

    private static async Task<ImmutableArray<Diagnostic>> Diagnostics(Document document)
    {
        var compilation = (await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken))!;
        return await compilation.WithAnalyzers([new RewriteAnalyzer()]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    private static Document Document(string source)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Fix",
            "Fix",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            parseOptions: GeneratorHarness.ParseOptions,
            metadataReferences: GeneratorHarness.References));
        return workspace.AddDocument(project.Id, "Tests.cs", Microsoft.CodeAnalysis.Text.SourceText.From(source));
    }
}
