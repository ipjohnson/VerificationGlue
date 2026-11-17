using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.CodeFixes;

/// <summary>Fixes VG0005 by adding <c>partial</c> to each type that contains the <c>[Verify]</c> method.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakePartialCodeFix))]
[Shared]
public sealed class MakePartialCodeFix : CodeFixProvider
{
    public const string Title = "Make the containing types partial";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("VG0005");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindNode(context.Span).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(Title, token => MakePartial(context.Document, method, token), equivalenceKey: Title),
                diagnostic);
        }
    }

    private static async Task<Document> MakePartial(Document document, MethodDeclarationSyntax method, CancellationToken token)
    {
        var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
        var types = method.Ancestors().OfType<TypeDeclarationSyntax>().Where(type => !type.Modifiers.Any(SyntaxKind.PartialKeyword)).ToList();
        var fixedRoot = root!.ReplaceNodes(types, (_, rewritten) => AddPartial(rewritten));
        return document.WithSyntaxRoot(fixedRoot);
    }

    /// <summary>Adds <c>partial</c> last among the modifiers, where C# requires it.</summary>
    private static TypeDeclarationSyntax AddPartial(TypeDeclarationSyntax type)
    {
        var partial = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        if (type.Modifiers.Count == 0)
        {
            var keyword = type.Keyword;
            return type
                .WithKeyword(keyword.WithLeadingTrivia())
                .WithModifiers(SyntaxFactory.TokenList(partial.WithLeadingTrivia(keyword.LeadingTrivia)));
        }

        return type.WithModifiers(type.Modifiers.Add(partial));
    }
}
