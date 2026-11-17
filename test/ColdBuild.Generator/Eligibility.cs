using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ColdBuild.Generator;

internal static class Eligibility
{
    public const string Runtime = "VerificationGlue";
    public const string Suffix = "__Verified";

    public static bool IsVerifyCall(IMethodSymbol method) =>
        method.Name == "Verify" &&
        method.ContainingType is { Name: "VerifySyntax", ContainingNamespace: { Name: Runtime, ContainingNamespace.IsGlobalNamespace: true } };

    /// <summary>
    /// Whether tier 3 emits a copy of <paramref name="method"/>, or for a method from another
    /// assembly, whether that assembly holds a marked copy.
    /// </summary>
    public static bool WillRewrite(IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Ordinary || method.IsVirtual || method.IsAbstract || method.IsOverride ||
            method.IsExtern || method.IsGenericMethod || method.ContainingType is not { } type || IsGenericChain(type) ||
            type.TypeKind == TypeKind.Interface)
        {
            return false;
        }

        if (method.DeclaringSyntaxReferences.Length == 0)
        {
            return type.GetMembers(method.Name + Suffix).OfType<IMethodSymbol>().Any(copy =>
                copy.DeclaredAccessibility == Accessibility.Public &&
                copy.GetAttributes().Any(attribute => IsRuntimeType(attribute.AttributeClass, "VerifiedCopyAttribute")));
        }

        if (!HasRewriteAttribute(method, out _) ||
            method.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax { Body: not null } declaration)
        {
            return false;
        }

        for (var containing = type; containing is not null; containing = containing.ContainingType)
        {
            if (!containing.DeclaringSyntaxReferences.All(reference =>
                    reference.GetSyntax() is TypeDeclarationSyntax syntax && syntax.Modifiers.Any(SyntaxKind.PartialKeyword)) ||
                containing.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected)
            {
                return false;
            }
        }

        // A copy in another file cannot see a file-local type.
        return !declaration.SyntaxTree.GetRoot().DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .OfType<BaseTypeDeclarationSyntax>()
            .Any(declared => declared.Modifiers.Any(SyntaxKind.FileKeyword));
    }

    /// <summary>
    /// True for <c>[Verify]</c> or an attribute marked <c>[VerifyRewrite]</c>.
    /// <paramref name="runnerCallsCopy"/> is true only for the second kind.
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

    private static bool IsGenericChain(INamedTypeSymbol type)
    {
        for (var containing = type; containing is not null; containing = containing.ContainingType)
        {
            if (containing.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRuntimeType(INamedTypeSymbol? type, string name) =>
        type is not null && type.Name == name &&
        type.ContainingNamespace is { Name: Runtime, ContainingNamespace.IsGlobalNamespace: true };
}
