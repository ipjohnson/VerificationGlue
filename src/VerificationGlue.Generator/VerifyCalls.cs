using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Generator;

/// <summary>Finds calls to the methods of <c>VerificationGlue.VerifySyntax</c>.</summary>
internal static class VerifyCalls
{
    public const string ConditionParameter = "condition";
    public const string ActionParameter = "action";

    /// <summary>A cheap syntax test: an invocation whose method name is one of ours.</summary>
    public static bool IsCandidate(SyntaxNode node) =>
        node is InvocationExpressionSyntax invocation && MethodName(invocation.Expression) is "Verify" or "Throws" or "ThrowsAsync";

    public static CallKind? KindOf(IMethodSymbol method)
    {
        var type = method.ContainingType;
        if (type is not { Name: "VerifySyntax", ContainingType: null } ||
            type.ContainingNamespace is not { Name: "VerificationGlue", ContainingNamespace.IsGlobalNamespace: true })
        {
            return null;
        }

        return method.Name switch
        {
            "Verify" => method.Parameters.Length == 3 ? CallKind.VerifyWithMessage : CallKind.Verify,
            "Throws" => CallKind.Throws,
            "ThrowsAsync" => CallKind.ThrowsAsync,
            _ => null,
        };
    }

    /// <summary>The argument passed to the parameter named <paramref name="parameter"/>, which is first.</summary>
    public static ExpressionSyntax? Argument(InvocationExpressionSyntax invocation, string parameter)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.NameColon?.Name.Identifier.ValueText == parameter)
            {
                return argument.Expression;
            }
        }

        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count > 0 && arguments[0].NameColon is null ? arguments[0].Expression : null;
    }

    private static string? MethodName(ExpressionSyntax expression) => expression switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };
}
