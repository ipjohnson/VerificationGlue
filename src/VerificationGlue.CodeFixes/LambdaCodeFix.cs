using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.CodeFixes;

/// <summary>Fixes VG0009 by writing the condition of <c>Verify(condition)</c> as <c>() =&gt; condition</c>.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LambdaCodeFix))]
[Shared]
public sealed class LambdaCodeFix : CodeFixProvider
{
    public const string Title = "Write the condition as a lambda";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("VG0009");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArgumentSyntax>() is not { } argument)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(Title, token => WrapInLambda(context.Document, argument.Expression, token), equivalenceKey: Title),
                diagnostic);
        }
    }

    private static async Task<Document> WrapInLambda(Document document, ExpressionSyntax condition, CancellationToken token)
    {
        var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
        var none = SyntaxFactory.TriviaList();
        var space = SyntaxFactory.TriviaList(SyntaxFactory.Space);
        var lambda = SyntaxFactory.ParenthesizedLambdaExpression()
            .WithParameterList(SyntaxFactory.ParameterList(
                SyntaxFactory.Token(none, SyntaxKind.OpenParenToken, none),
                default,
                SyntaxFactory.Token(none, SyntaxKind.CloseParenToken, none)))
            .WithArrowToken(SyntaxFactory.Token(space, SyntaxKind.EqualsGreaterThanToken, space))
            .WithExpressionBody(condition.WithoutTrivia())
            .WithTriviaFrom(condition);
        return document.WithSyntaxRoot(root!.ReplaceNode(condition, lambda));
    }
}
