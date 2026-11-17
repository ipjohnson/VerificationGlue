using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ColdBuild.Generator;

/// <summary>The tier 2 message for a condition, from its shape alone.</summary>
internal static class Shapes
{
    public static string Message(ExpressionSyntax condition, SemanticModel model)
    {
        while (condition is ParenthesizedExpressionSyntax parenthesized)
        {
            condition = parenthesized.Expression;
        }

        switch (condition)
        {
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression, Right: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } binary:
                return $"{OneLine(binary.Left)} can not be null";
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression, Right: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } binary:
                return $"{OneLine(binary.Left)} should be null";
            case IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax { Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } } } notNull:
                return $"{OneLine(notNull.Expression)} can not be null";
            case IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } } isNull:
                return $"{OneLine(isNull.Expression)} should be null";
            case IsPatternExpressionSyntax pattern:
                return $"expected {OneLine(pattern.Expression)} to match {OneLine(pattern.Pattern)}";
            case BinaryExpressionSyntax binary when Relation(binary.Kind()) is { } relation:
                return $"expected {OneLine(binary.Left)} {relation} {OneLine(binary.Right)}";
            case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not:
                return $"expected {OneLine(not.Operand)} to be false";
            case InvocationExpressionSyntax invocation when model.GetTypeInfo(invocation).Type?.SpecialType == SpecialType.System_Boolean:
                return $"expected {OneLine(invocation)} to return true";
            default:
                return "the condition was false";
        }
    }

    public static string OneLine(SyntaxNode node) =>
        string.Join(" ", node.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()));

    private static string? Relation(SyntaxKind kind) => kind switch
    {
        SyntaxKind.EqualsExpression => "to equal",
        SyntaxKind.NotEqualsExpression => "to not equal",
        SyntaxKind.LessThanExpression => "to be less than",
        SyntaxKind.LessThanOrEqualExpression => "to be at most",
        SyntaxKind.GreaterThanExpression => "to be greater than",
        SyntaxKind.GreaterThanOrEqualExpression => "to be at least",
        _ => null,
    };
}
