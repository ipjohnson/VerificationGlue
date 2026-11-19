using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Generator;

/// <summary>
/// The lines a tier 3 message adds for a condition of a recognised shape, written as calls on the
/// failure builder. A line reads only the locals the rewritten condition captured, and constants,
/// so nothing in the condition runs again. A shape whose values were not all captured adds no line.
/// </summary>
internal static class Explanations
{
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>The builder calls, without the leading dot, for the shapes the condition matches.</summary>
    public static IReadOnlyList<string> For(ExpressionSyntax condition, RewrittenCondition rewritten, SemanticModel model, Func<SyntaxNode, string> render, CancellationToken token)
    {
        var lines = new List<string>();
        if (Tolerance(condition, rewritten, model, render, token) is { } tolerance)
        {
            lines.Add(tolerance);
        }

        return lines;
    }

    /// <summary>
    /// <c>Math.Abs(a - b) &lt;= t</c> or <c>Math.Abs(a - b) &lt; t</c>, with either side of the
    /// comparison first, for <c>double</c>, <c>float</c> and <c>decimal</c>. When one of <c>a</c>
    /// and <c>b</c> is a constant, the line names the other first.
    /// </summary>
    private static string? Tolerance(ExpressionSyntax condition, RewrittenCondition rewritten, SemanticModel model, Func<SyntaxNode, string> render, CancellationToken token)
    {
        if (Unwrap(condition) is not BinaryExpressionSyntax comparison)
        {
            return null;
        }

        var (abs, tolerance) = comparison.Kind() switch
        {
            SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression => (comparison.Left, comparison.Right),
            SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression => (comparison.Right, comparison.Left),
            _ => (null!, null!),
        };
        if (abs is null ||
            Unwrap(abs) is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } call ||
            model.GetSymbolInfo(call, token).Symbol is not IMethodSymbol
            {
                Name: "Abs",
                ContainingType: { Name: "Math", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } },
            } method ||
            !IsTolerant(method.Parameters[0].Type) ||
            Unwrap(call.ArgumentList.Arguments[0].Expression) is not BinaryExpressionSyntax subtraction ||
            !subtraction.IsKind(SyntaxKind.SubtractExpression) ||
            model.GetTypeInfo(call, token).ConvertedType is not { } compared || !IsTolerant(compared) ||
            !SymbolEqualityComparer.Default.Equals(compared, model.GetTypeInfo(tolerance, token).ConvertedType))
        {
            return null;
        }

        var type = method.Parameters[0].Type.ToDisplayString(TypeFormat);
        var first = Unwrap(subtraction.Left);
        var second = Unwrap(subtraction.Right);
        if (IsConstant(first, model, token) && !IsConstant(second, model, token))
        {
            (first, second) = (second, first);
        }

        if (Read(first, rewritten, model, render, type, token) is not { } firstValue ||
            Read(second, rewritten, model, render, type, token) is not { } secondValue ||
            Captured(Unwrap(abs), rewritten) is not { } difference ||
            Read(Unwrap(tolerance), rewritten, model, render, compared.ToDisplayString(TypeFormat), token) is not { } toleranceValue)
        {
            return null;
        }

        return $"Tolerance({CopyBuilder.Literal(ConditionRewriter.OneLine(first))}, ({type}){firstValue}, " +
               $"{CopyBuilder.Literal(ConditionRewriter.OneLine(second))}, ({type}){secondValue}, {difference}, {toleranceValue})";
    }

    /// <summary>
    /// The text that reads a part's value on the failure path: its captured local, or the constant
    /// cast to <paramref name="type"/>. Null when the part was not captured.
    /// </summary>
    private static string? Read(ExpressionSyntax part, RewrittenCondition rewritten, SemanticModel model, Func<SyntaxNode, string> render, string type, CancellationToken token) =>
        IsConstant(part, model, token) ? $"(({type})({render(part)}))" : Captured(part, rewritten);

    private static string? Captured(ExpressionSyntax part, RewrittenCondition rewritten) =>
        rewritten.Rows.FirstOrDefault(row => row.Span == part.Span && row.Capture is not null)?.Capture;

    private static bool IsConstant(ExpressionSyntax expression, SemanticModel model, CancellationToken token) =>
        model.GetConstantValue(expression, token).HasValue;

    private static bool IsTolerant(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Decimal;

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
