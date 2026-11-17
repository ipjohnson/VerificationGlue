using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Generator;

/// <summary>The tier 2 message for a call, from the shape of its argument.</summary>
internal static class Shapes
{
    /// <summary>The message for a <c>Verify</c> condition, or null when its shape has none.</summary>
    public static string? Condition(ExpressionSyntax condition, SemanticModel model, CancellationToken token)
    {
        condition = Unwrap(condition);
        switch (condition)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.NotEqualsExpression) && NullComparand(binary) is { } operand:
                return $"{OneLine(operand)} can not be null";
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) && NullComparand(binary) is { } operand:
                return $"expected {OneLine(operand)} to be null";
            case BinaryExpressionSyntax binary when Relation(binary.Kind()) is { } relation:
                return $"expected {OneLine(binary.Left)} {relation} {OneLine(binary.Right)}";
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.IsExpression):
                return $"expected {OneLine(binary.Left)} to be of type {OneLine(binary.Right)}";
            case IsPatternExpressionSyntax pattern:
                return Pattern(pattern);
            case PrefixUnaryExpressionSyntax not when not.IsKind(SyntaxKind.LogicalNotExpression):
                return $"expected {OneLine(not.Operand)} to be false";
            case InvocationExpressionSyntax invocation when model.GetTypeInfo(invocation, token).Type?.SpecialType == SpecialType.System_Boolean:
                return $"expected {OneLine(invocation)} to return true";
            default:
                return null;
        }
    }

    /// <summary>The message for a <c>Throws</c> or <c>ThrowsAsync</c> call, or null when the exception type is a type parameter.</summary>
    public static string? Throws(ExpressionSyntax action, ITypeSymbol exceptionType)
    {
        if (exceptionType is ITypeParameterSymbol)
        {
            return null;
        }

        var body = Unwrap(action) is LambdaExpressionSyntax { ExpressionBody: { } expressionBody } ? expressionBody : action;
        return $"expected {OneLine(body)} to throw {exceptionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}";
    }

    public static string OneLine(SyntaxNode node) =>
        string.Join(" ", node.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()));

    private static string Pattern(IsPatternExpressionSyntax pattern)
    {
        var subject = OneLine(pattern.Expression);
        switch (pattern.Pattern)
        {
            case ConstantPatternSyntax constant when IsNull(constant.Expression):
                return $"expected {subject} to be null";
            case UnaryPatternSyntax { Pattern: ConstantPatternSyntax notConstant } not when not.IsKind(SyntaxKind.NotPattern) && IsNull(notConstant.Expression):
                return $"{subject} can not be null";
            case RecursivePatternSyntax { Type: null, Designation: null, PositionalPatternClause: null, PropertyPatternClause.Subpatterns.Count: 0 }:
                return $"{subject} can not be null";
            case TypePatternSyntax type:
                return $"expected {subject} to be of type {OneLine(type.Type)}";
            case DeclarationPatternSyntax declaration:
                return $"expected {subject} to be of type {OneLine(declaration.Type)}";
            default:
                return $"expected {subject} to match {OneLine(pattern.Pattern)}";
        }
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static ExpressionSyntax? NullComparand(BinaryExpressionSyntax binary) =>
        IsNull(binary.Right) ? binary.Left : IsNull(binary.Left) ? binary.Right : null;

    private static bool IsNull(ExpressionSyntax expression) => Unwrap(expression).IsKind(SyntaxKind.NullLiteralExpression);

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
