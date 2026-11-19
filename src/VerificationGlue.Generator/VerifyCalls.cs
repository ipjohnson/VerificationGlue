using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Generator;

/// <summary>Finds calls to the methods of <c>VerificationGlue.VerifySyntax</c>.</summary>
internal static class VerifyCalls
{
    public const string ConditionParameter = "condition";
    public const string ActionParameter = "action";
    public const string MessageParameter = "message";
    public const string ExpressionParameter = "expression";
    public const string ItemsParameter = "items";
    public const string PredicateParameter = "predicate";
    public const string PredicateExpressionParameter = "predicateExpression";
    public const string ValueParameter = "value";

    /// <summary>A cheap syntax test: an invocation whose method name is one of ours.</summary>
    public static bool IsCandidate(SyntaxNode node) =>
        node is InvocationExpressionSyntax invocation && MethodName(invocation.Expression) is
            "Verify" or "Check" or "Throws" or "ThrowsAsync" or "ThrowsExactly" or "ThrowsExactlyAsync" or "Single" or "Is" or "NotNull";

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
            "Check" => CallKind.Check,
            "Throws" => CallKind.Throws,
            "ThrowsAsync" => CallKind.ThrowsAsync,
            "ThrowsExactly" => CallKind.ThrowsExactly,
            "ThrowsExactlyAsync" => CallKind.ThrowsExactlyAsync,
            "Single" => method.Parameters.Length == 2 ? CallKind.Single : CallKind.SingleWhere,
            "Is" => CallKind.Is,
            "NotNull" => method.OriginalDefinition.TypeParameters[0].HasValueTypeConstraint ? CallKind.NotNullValue : CallKind.NotNull,
            _ => null,
        };
    }

    /// <summary>The argument passed to the parameter named <paramref name="parameter"/>, which is first.</summary>
    public static ExpressionSyntax? Argument(InvocationExpressionSyntax invocation, string parameter) =>
        Argument(invocation, parameter, 0);

    /// <summary>The argument passed to the parameter named <paramref name="parameter"/> at <paramref name="position"/>.</summary>
    public static ExpressionSyntax? Argument(InvocationExpressionSyntax invocation, string parameter, int position)
    {
        var arguments = invocation.ArgumentList.Arguments;
        foreach (var argument in arguments)
        {
            if (argument.NameColon?.Name.Identifier.ValueText == parameter)
            {
                return argument.Expression;
            }
        }

        for (var index = 0; index < arguments.Count && index <= position; index++)
        {
            if (arguments[index].NameColon is not null)
            {
                return null;
            }
        }

        return arguments.Count > position ? arguments[position].Expression : null;
    }

    /// <summary>The method that a <c>.Because(...)</c> or <c>.WithMessage(...)</c> call on a <c>Verify</c> result names, or null.</summary>
    public static string? ChainedName(InvocationExpressionSyntax verify) =>
        verify.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name and ("Because" or "WithMessage") } access &&
        access.Expression == verify && access.Parent is InvocationExpressionSyntax
            ? name
            : null;

    /// <summary>
    /// The statement that a tier 3 rewrite replaces for a <c>Verify</c> or <c>Check</c> call: the
    /// call alone, or the call followed by <c>.Because(...)</c> or <c>.WithMessage(...)</c>, as an
    /// expression statement or as the body of an expression-bodied method. Null when the call is
    /// in a lambda or a local function, or is part of a larger expression.
    /// </summary>
    public static SyntaxNode? RewritableStatement(InvocationExpressionSyntax call, SyntaxNode methodBody)
    {
        SyntaxNode expression = ChainedName(call) is not null ? call.Parent!.Parent! : call;
        var statement = expression.Parent switch
        {
            ExpressionStatementSyntax expressionStatement => expressionStatement,
            ArrowExpressionClauseSyntax arrow when arrow == methodBody => (SyntaxNode)arrow,
            _ => null,
        };

        if (statement is null)
        {
            return null;
        }

        for (var node = statement.Parent; node is not null && node != methodBody; node = node.Parent)
        {
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return null;
            }
        }

        return statement;
    }

    private static string? MethodName(ExpressionSyntax expression) => expression switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };
}
