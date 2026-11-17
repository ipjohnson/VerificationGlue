using System.Collections.Generic;
using System.Linq;
using CSharpAuthor;
using CSharpAuthor.Collections;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ColdBuild.Generator;

/// <summary>
/// Wraps each sub-expression of a condition in an assignment to a typed local, <c>(v = expr)</c>.
/// The expression keeps its shape, so evaluation order and short-circuiting do not change.
/// </summary>
internal sealed class Rewriter(SemanticModel model, int site)
{
    private readonly List<Capture> captures = [];

    public VerifyRewrite Rewrite(ExpressionSyntax condition) =>
        new(Visit(condition, [], top: true).Text, Shapes.OneLine(condition), new EquatableArray<Capture>(captures.ToArray()));

    private (string Text, string? Capture) Visit(ExpressionSyntax expression, GuardTerm[] guard, bool top = false)
    {
        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            var inner = Visit(parenthesized.Expression, guard, top);
            return ("(" + inner.Text + ")", inner.Capture);
        }

        if (expression is LiteralExpressionSyntax || model.GetConstantValue(expression).HasValue)
        {
            return (expression.ToString(), null);
        }

        string text;
        switch (expression)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression):
            {
                var left = Visit(binary.Left, guard);
                var rightGuard = left.Capture is null
                    ? guard
                    : [.. guard, new GuardTerm(left.Capture, binary.IsKind(SyntaxKind.LogicalOrExpression))];
                text = left.Text + " " + binary.OperatorToken.Text + " " + Visit(binary.Right, rightGuard).Text;
                break;
            }

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.IsExpression) || binary.IsKind(SyntaxKind.AsExpression):
                text = Visit(binary.Left, guard).Text + " " + binary.OperatorToken.Text + " " + binary.Right;
                break;
            case BinaryExpressionSyntax binary when !binary.IsKind(SyntaxKind.CoalesceExpression):
                text = Visit(binary.Left, guard).Text + " " + binary.OperatorToken.Text + " " + Visit(binary.Right, guard).Text;
                break;
            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.LogicalNotExpression):
                text = "!" + Visit(unary.Operand, guard).Text;
                break;
            case IsPatternExpressionSyntax pattern:
                text = Visit(pattern.Expression, guard).Text + " is " + pattern.Pattern;
                break;
            case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                text = CanHoistReceiver(access.Expression) ? Visit(access.Expression, guard).Text + "." + access.Name : expression.ToString();
                break;
            case ElementAccessExpressionSyntax element:
                text = (CanHoistReceiver(element.Expression) ? Visit(element.Expression, guard).Text : element.Expression.ToString()) +
                    "[" + string.Join(", ", element.ArgumentList.Arguments.Select(argument => Visit(argument.Expression, guard).Text)) + "]";
                break;
            case InvocationExpressionSyntax invocation:
            {
                var callee = invocation.Expression is MemberAccessExpressionSyntax access &&
                             access.IsKind(SyntaxKind.SimpleMemberAccessExpression) && CanHoistReceiver(access.Expression)
                    ? Visit(access.Expression, guard).Text + "." + access.Name
                    : invocation.Expression.ToString();
                var arguments = invocation.ArgumentList.Arguments.Select(argument =>
                    argument.RefKindKeyword.IsKind(SyntaxKind.None) ? argument.NameColon + Visit(argument.Expression, guard).Text : argument.ToString());
                text = callee + "(" + string.Join(", ", arguments) + ")";
                break;
            }

            default:
                text = expression.ToString();
                break;
        }

        return top ? (text, null) : Capture(expression, text, guard);
    }

    private bool CanHoistReceiver(ExpressionSyntax receiver)
    {
        if (model.GetSymbolInfo(receiver).Symbol is INamespaceSymbol or ITypeSymbol)
        {
            return false;
        }

        // Copying a mutable struct into a local would send a mutating call to the copy.
        var type = model.GetTypeInfo(receiver).Type;
        return type is not null && (!type.IsValueType || type.IsReadOnly);
    }

    private (string, string?) Capture(ExpressionSyntax expression, string text, GuardTerm[] guard)
    {
        var type = model.GetTypeInfo(expression).Type;
        if (type is null || type.SpecialType == SpecialType.System_Void || type.IsAnonymousType || type.IsRefLikeType ||
            type.TypeKind is TypeKind.Error or TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Dynamic or TypeKind.TypeParameter ||
            expression is InterpolatedStringExpressionSyntax or TupleExpressionSyntax or LambdaExpressionSyntax or BaseExpressionSyntax ||
            (expression is IdentifierNameSyntax && model.GetSymbolInfo(expression).Symbol is not (ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol)))
        {
            return (text, null);
        }

        var name = $"__v{site}_{captures.Count}";
        captures.Add(new Capture(name, type.GetTypeDefinition(), Shapes.OneLine(expression), new EquatableArray<GuardTerm>(guard)));
        return ($"({name} = {text})", name);
    }
}
