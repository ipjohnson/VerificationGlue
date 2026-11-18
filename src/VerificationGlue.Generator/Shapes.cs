using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Generator;

/// <summary>The tier 2 message for a call, from the shape of its argument.</summary>
internal static class Shapes
{
    /// <summary>
    /// The message for a <c>Verify</c> condition, or null when its shape has none. A shape whose
    /// operand spans several lines, such as a raw string, also has none, because the first line of
    /// the failure already shows the whole condition.
    /// </summary>
    public static string? Condition(ExpressionSyntax condition, SemanticModel model, CancellationToken token)
    {
        condition = Unwrap(condition);
        switch (condition)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.NotEqualsExpression) && NullComparand(binary) is { } operand:
                return Say("{0} can not be null", operand);
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) && NullComparand(binary) is { } operand:
                return Say("expected {0} to be null", operand);
            case BinaryExpressionSyntax binary when Relation(binary.Kind()) is { } relation:
                return Say("expected {0} " + relation + " {1}", binary.Left, binary.Right);
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.IsExpression):
                return Say("expected {0} to be of type {1}", binary.Left, binary.Right);
            case IsPatternExpressionSyntax pattern:
                return Pattern(pattern);
            case PrefixUnaryExpressionSyntax not when not.IsKind(SyntaxKind.LogicalNotExpression):
                return Say("expected {0} to be false", not.Operand);
            case InvocationExpressionSyntax invocation when model.GetTypeInfo(invocation, token).Type?.SpecialType == SpecialType.System_Boolean:
                return Say("expected {0} to return true", invocation);
            default:
                return null;
        }
    }

    /// <summary>
    /// The message for a <c>Throws</c> or <c>ThrowsAsync</c> call, or null when the exception type
    /// is a type parameter or the action spans several lines.
    /// </summary>
    public static string? Throws(ExpressionSyntax action, ITypeSymbol exceptionType, bool exactly = false)
    {
        if (exceptionType is ITypeParameterSymbol)
        {
            return null;
        }

        var body = Unwrap(action) is LambdaExpressionSyntax { ExpressionBody: { } expressionBody } ? expressionBody : action;
        return Say((exactly ? "expected {0} to throw exactly " : "expected {0} to throw ") + exceptionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), body);
    }

    private static string? Pattern(IsPatternExpressionSyntax pattern)
    {
        var subject = pattern.Expression;
        switch (pattern.Pattern)
        {
            case ConstantPatternSyntax constant when IsNull(constant.Expression):
                return Say("expected {0} to be null", subject);
            case UnaryPatternSyntax { Pattern: ConstantPatternSyntax notConstant } not when not.IsKind(SyntaxKind.NotPattern) && IsNull(notConstant.Expression):
                return Say("{0} can not be null", subject);
            case RecursivePatternSyntax { Type: null, Designation: null, PositionalPatternClause: null, PropertyPatternClause.Subpatterns.Count: 0 }:
                return Say("{0} can not be null", subject);
            case TypePatternSyntax type:
                return Say("expected {0} to be of type {1}", subject, type.Type);
            case DeclarationPatternSyntax declaration:
                return Say("expected {0} to be of type {1}", subject, declaration.Type);
            default:
                return Say("expected {0} to match {1}", subject, pattern.Pattern);
        }
    }

    /// <summary>Fills the format with the source text of each node, or returns null when a node spans several lines.</summary>
    private static string? Say(string format, params SyntaxNode[] nodes)
    {
        var texts = new object[nodes.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            var text = nodes[i].ToString();
            if (text.IndexOf('\n') >= 0)
            {
                return null;
            }

            texts[i] = text;
        }

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, format, texts);
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
