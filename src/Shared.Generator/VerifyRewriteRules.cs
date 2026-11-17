using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Shared;

/// <summary>
/// The tier 3 contract between the <c>VerificationGlue</c> generator, which writes the
/// <c>__Verified</c> copies, and the generators that call them without seeing them, such as the
/// test generator. Both compile this file, so they make the same decision.
/// </summary>
internal static class VerifyRewriteRules
{
    public const string Suffix = "__Verified";

    /// <summary>The version that <c>[VerifiedCopy]</c> carries. A copy from another assembly with a different version is not called.</summary>
    public const int ContractVersion = 1;

    public const string RewriteProperty = "build_property.VerificationGlueRewrite";

    private const string Runtime = "VerificationGlue";

    /// <summary>Whether the project turned tier 3 on with the <c>VerificationGlueRewrite</c> MSBuild property.</summary>
    public static bool IsEnabled(AnalyzerConfigOptions globalOptions) =>
        globalOptions.TryGetValue(RewriteProperty, out var value) && string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether tier 3 writes a copy of <paramref name="method"/> when rewriting is enabled. For a
    /// method from another assembly, whether that assembly holds a copy with this contract version.
    /// </summary>
    /// <remarks>
    /// The method must be ordinary, non-virtual, non-generic, return by value, and have a body
    /// without syntax errors or conditional directives. It must have <c>[Verify]</c> or an attribute
    /// marked <c>[VerifyRewrite]</c>, and sit in a chain of <c>partial</c> non-generic types that
    /// generated code can name. Its file must declare no <c>file</c> type. Semantic errors do not
    /// count, because members that another generator adds are errors to every generator.
    /// </remarks>
    public static bool WillRewrite(IMethodSymbol method)
    {
        method = method.ReducedFrom ?? method;
        if (method.MethodKind != MethodKind.Ordinary || method.IsVirtual || method.IsAbstract || method.IsOverride ||
            method.IsExtern || method.IsPartialDefinition || method.IsGenericMethod || method.ReturnsByRef || method.ReturnsByRefReadonly ||
            method.ContainingType is not { } type || type.TypeKind is not (TypeKind.Class or TypeKind.Struct) ||
            (type.TypeKind == TypeKind.Struct && !method.IsStatic) ||
            !CanName(method.ReturnType) || method.Parameters.Any(parameter => !CanName(parameter.Type)))
        {
            return false;
        }

        for (var containing = type; containing is not null; containing = containing.ContainingType)
        {
            if (containing.IsGenericType || containing.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        if (method.DeclaringSyntaxReferences.Length == 0)
        {
            return type.GetMembers(method.Name + Suffix).OfType<IMethodSymbol>().Any(copy =>
                copy.DeclaredAccessibility == Accessibility.Public &&
                copy.GetAttributes().Any(attribute =>
                    IsRuntimeType(attribute.AttributeClass, "VerifiedCopyAttribute") &&
                    attribute.ConstructorArguments.Length == 1 &&
                    attribute.ConstructorArguments[0].Value is ContractVersion));
        }

        if (!HasRewriteAttribute(method, out _) ||
            method.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax declaration ||
            ((SyntaxNode?)declaration.Body ?? declaration.ExpressionBody) is not { } body ||
            HasSyntaxErrors(declaration) || HasConditionalDirectives(body))
        {
            return false;
        }

        for (var containing = type; containing is not null; containing = containing.ContainingType)
        {
            if (!containing.DeclaringSyntaxReferences.All(reference =>
                    reference.GetSyntax() is TypeDeclarationSyntax syntax && syntax.Modifiers.Any(SyntaxKind.PartialKeyword)))
            {
                return false;
            }
        }

        return !DeclaresFileType(declaration.SyntaxTree);
    }

    /// <summary>
    /// True for <c>[Verify]</c> or an attribute marked <c>[VerifyRewrite]</c>.
    /// <paramref name="runnerCallsCopy"/> is true only for the second kind, whose methods a runner
    /// always calls through the copy.
    /// </summary>
    public static bool HasRewriteAttribute(IMethodSymbol method, out bool runnerCallsCopy)
    {
        runnerCallsCopy = false;
        var found = false;
        foreach (var attribute in method.GetAttributes())
        {
            if (IsRuntimeType(attribute.AttributeClass, "VerifyAttribute"))
            {
                found = true;
            }
            else if (attribute.AttributeClass?.GetAttributes().Any(marker => IsRuntimeType(marker.AttributeClass, "VerifyRewriteAttribute")) == true)
            {
                found = true;
                runnerCallsCopy = true;
            }
        }

        return found;
    }

    /// <summary>Whether the file declares a <c>file</c> type, which a copy in another file cannot see.</summary>
    public static bool DeclaresFileType(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax)
            .OfType<BaseTypeDeclarationSyntax>()
            .Any(declared => declared.Modifiers.Any(SyntaxKind.FileKeyword));

    private static bool HasSyntaxErrors(SyntaxNode node) =>
        node.ContainsDiagnostics && node.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    /// <summary>A copied body with <c>#if</c> or <c>#line</c> could pair with a directive outside it.</summary>
    private static bool HasConditionalDirectives(SyntaxNode body) =>
        body.ContainsDirectives && body.DescendantTrivia(descendIntoTrivia: true).Any(trivia =>
            trivia.IsKind(SyntaxKind.IfDirectiveTrivia) || trivia.IsKind(SyntaxKind.ElifDirectiveTrivia) ||
            trivia.IsKind(SyntaxKind.ElseDirectiveTrivia) || trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia) ||
            trivia.IsKind(SyntaxKind.DefineDirectiveTrivia) || trivia.IsKind(SyntaxKind.UndefDirectiveTrivia) ||
            trivia.IsKind(SyntaxKind.LineDirectiveTrivia) || trivia.IsKind(SyntaxKind.LineSpanDirectiveTrivia));

    /// <summary>Whether a generated interceptor outside the type can name <paramref name="type"/>.</summary>
    private static bool CanName(ITypeSymbol type)
    {
        switch (type)
        {
            case IPointerTypeSymbol or IFunctionPointerTypeSymbol or IDynamicTypeSymbol or ITypeParameterSymbol:
                return false;
            case IArrayTypeSymbol array:
                return CanName(array.ElementType);
            case INamedTypeSymbol named:
                if (named.IsAnonymousType || named.IsFileLocal)
                {
                    return false;
                }

                for (INamedTypeSymbol? containing = named; containing is not null; containing = containing.ContainingType)
                {
                    if (containing.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
                    {
                        return false;
                    }
                }

                return named.TypeArguments.All(CanName);
            default:
                return true;
        }
    }

    private static bool IsRuntimeType(INamedTypeSymbol? type, string name) =>
        type is not null && type.Name == name &&
        type.ContainingNamespace is { Name: Runtime, ContainingNamespace.IsGlobalNamespace: true };
}
