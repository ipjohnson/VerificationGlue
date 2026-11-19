using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using VerificationGlue.Shared;

namespace VerificationGlue.Generator;

/// <summary>
/// Reports what keeps a <c>[Verify]</c> method or a call in it from tier 3, and a <c>Check</c>
/// that no scope can collect.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RewriteAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Diagnostics.BecauseNotRewritten,
        Diagnostics.TypeNotPartial,
        Diagnostics.NotCaptured,
        Diagnostics.CheckWithoutScope,
        Diagnostics.FileTypeInFile);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (declaration.AttributeLists.Count == 0 ||
            context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } method ||
            !VerifyRewriteRules.HasRewriteAttribute(method, out _))
        {
            return;
        }

        foreach (var type in declaration.Ancestors().OfType<TypeDeclarationSyntax>())
        {
            if (!type.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.TypeNotPartial, declaration.Identifier.GetLocation(), method.Name, type.Identifier.ValueText));
                return;
            }
        }

        var fileType = declaration.SyntaxTree.GetRoot(context.CancellationToken)
            .DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax)
            .OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault(declared => declared.Modifiers.Any(SyntaxKind.FileKeyword));
        if (fileType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.FileTypeInFile, declaration.Identifier.GetLocation(), method.Name, fileType.Identifier.ValueText));
        }
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.Syntax is not InvocationExpressionSyntax syntax)
        {
            return;
        }

        if (invocation.TargetMethod is { Name: "Because" or "WithMessage", ContainingType: { Name: "VerifyResult", ContainingNamespace: { Name: "VerificationGlue", ContainingNamespace.IsGlobalNamespace: true } } } &&
            syntax.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax verify } access)
        {
            if (!IsRewritten(context, verify, out _, out _))
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.BecauseNotRewritten, access.Name.GetLocation(), invocation.TargetMethod.Name));
            }

            return;
        }

        var kind = VerifyCalls.KindOf(invocation.TargetMethod);
        if (kind is { } found && VerifyCalls.IsCheck(found) && ScopelessMember(context.ContainingSymbol) is { } member)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CheckWithoutScope, syntax.GetLocation(), member));
        }

        if (kind is CallKind.Verify or CallKind.VerifyWithMessage or CallKind.Check or CallKind.VerifyLambda or CallKind.CheckLambda && IsRewritten(context, syntax, out var condition, out var model))
        {
            var rewritten = new ConditionRewriter(model, node => node.ToString(), _ => false, "__vg_", context.CancellationToken).Rewrite(condition);
            foreach (var part in rewritten.Uncaptured)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NotCaptured, part.Location, part.Text, part.Reason));
            }
        }
    }

    /// <summary>Whether tier 3 rewrites the <c>Verify</c> or <c>Check</c> statement of <paramref name="call"/>.</summary>
    private static bool IsRewritten(OperationAnalysisContext context, InvocationExpressionSyntax call, out ExpressionSyntax condition, out SemanticModel model)
    {
        condition = null!;
        model = context.Operation.SemanticModel!;
        if (!VerifyRewriteRules.IsEnabled(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions) ||
            call.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } declaration ||
            ((SyntaxNode?)declaration.Body ?? declaration.ExpressionBody) is not { } body ||
            model.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } method ||
            !VerifyRewriteRules.WillRewrite(method) ||
            model.GetSymbolInfo(call, context.CancellationToken).Symbol is not IMethodSymbol target ||
            VerifyCalls.KindOf(target) is not { } kind)
        {
            return false;
        }

        return CopyBuilder.Tier3Statement(call, kind, body, method, out condition) is not null;
    }

    /// <summary>A description of the member when no <c>VerifyScope</c> can be open while it runs.</summary>
    private static string? ScopelessMember(ISymbol symbol) => symbol switch
    {
        IMethodSymbol { MethodKind: MethodKind.StaticConstructor } method => $"the static constructor of '{method.ContainingType.Name}'",
        IMethodSymbol { MethodKind: MethodKind.Destructor } method => $"the finalizer of '{method.ContainingType.Name}'",
        IMethodSymbol method when method.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.ModuleInitializerAttribute") =>
            $"the module initializer '{method.Name}'",
        _ => null,
    };
}
