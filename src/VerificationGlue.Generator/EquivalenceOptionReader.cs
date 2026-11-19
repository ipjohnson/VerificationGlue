using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Generator;

internal enum OptionKind
{
    Unordered,
    Ignore,
}

/// <summary>One <c>Unordered</c> or <c>Ignore</c> option of a <c>VerifyEquivalent</c> call.</summary>
/// <param name="Path">The member names from the compared value down to the member, empty for the value itself.</param>
/// <param name="Syntax">The lambda that names the member.</param>
internal sealed record EquivalenceOption(OptionKind Kind, string[] Path, SyntaxNode Syntax);

/// <summary>An option that the comparer does not apply, and the reason that VG0010 gives.</summary>
internal sealed record OptionProblem(SyntaxNode Syntax, string Reason);

/// <summary>
/// Reads the options of a <c>VerifyEquivalent</c> call from its source: a lambda such as
/// <c>o =&gt; o.Unordered(x =&gt; x.Lines).Ignore(x =&gt; x.Id)</c>, whose member lambdas are chains of
/// member accesses on their parameter.
/// </summary>
internal static class EquivalenceOptionReader
{
    public static List<EquivalenceOption> Read(ExpressionSyntax? argument, SemanticModel model, List<OptionProblem> problems)
    {
        var options = new List<EquivalenceOption>();
        if (argument is null)
        {
            return options;
        }

        if (Lambda(argument) is not { } lambda)
        {
            problems.Add(new OptionProblem(argument, "it is not a lambda that calls Unordered and Ignore on its parameter"));
            return options;
        }

        var calls = new List<(OptionKind Kind, ExpressionSyntax Member)>();
        var current = Unwrap(lambda.Body);
        while (current is InvocationExpressionSyntax
               {
                   Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name and ("Unordered" or "Ignore") } access,
                   ArgumentList.Arguments.Count: 1,
               } call &&
               IsOption(model.GetSymbolInfo(call).Symbol))
        {
            calls.Add((name == "Unordered" ? OptionKind.Unordered : OptionKind.Ignore, call.ArgumentList.Arguments[0].Expression));
            current = Unwrap(access.Expression);
        }

        if (current is not IdentifierNameSyntax identifier || identifier.Identifier.ValueText != lambda.Parameter)
        {
            problems.Add(new OptionProblem(argument, "it is not a lambda that calls Unordered and Ignore on its parameter"));
            return options;
        }

        for (var index = calls.Count - 1; index >= 0; index--)
        {
            var (kind, member) = calls[index];
            if (Lambda(member) is { } memberLambda && Names(memberLambda.Body, memberLambda.Parameter) is { } path)
            {
                options.Add(new EquivalenceOption(kind, path.ToArray(), member));
            }
            else
            {
                problems.Add(new OptionProblem(member, "it is not a chain of member accesses on the lambda's parameter, such as 'x => x.Lines'"));
            }
        }

        return options;
    }

    private static bool IsOption(ISymbol? symbol) =>
        symbol is IMethodSymbol { ContainingType: { Name: "EquivalenceOptions", ContainingType: null, ContainingNamespace: { Name: "VerificationGlue", ContainingNamespace.IsGlobalNamespace: true } } };

    /// <summary>The parameter name and the expression body of a lambda with one parameter, or null.</summary>
    private static (string Parameter, ExpressionSyntax Body)? Lambda(ExpressionSyntax expression) => Unwrap(expression) switch
    {
        SimpleLambdaExpressionSyntax { AsyncKeyword.RawKind: 0, ExpressionBody: { } body } simple => (simple.Parameter.Identifier.ValueText, body),
        ParenthesizedLambdaExpressionSyntax { AsyncKeyword.RawKind: 0, ParameterList.Parameters.Count: 1, ExpressionBody: { } body } parenthesized =>
            (parenthesized.ParameterList.Parameters[0].Identifier.ValueText, body),
        _ => null,
    };

    /// <summary>
    /// The member names of <c>x.A.B</c>, <c>x.A!.B</c> or <c>x.A?.B</c> on the parameter, an empty
    /// list for the parameter itself, or null for any other expression.
    /// </summary>
    private static List<string>? Names(ExpressionSyntax expression, string parameter)
    {
        switch (Unwrap(expression))
        {
            case IdentifierNameSyntax identifier when identifier.Identifier.ValueText == parameter:
                return new List<string>();
            case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression, Name: IdentifierNameSyntax name } access:
                return Append(Names(access.Expression, parameter), name.Identifier.ValueText);
            case ConditionalAccessExpressionSyntax conditional:
                return Names(conditional.Expression, parameter) is { } names && Bound(conditional.WhenNotNull) is { } bound ? Concat(names, bound) : null;
            default:
                return null;
        }
    }

    /// <summary>The member names after <c>?</c>, such as <c>.B.C</c> in <c>x.A?.B.C</c>, or null.</summary>
    private static List<string>? Bound(ExpressionSyntax expression)
    {
        switch (Unwrap(expression))
        {
            case MemberBindingExpressionSyntax { Name: IdentifierNameSyntax name }:
                return new List<string> { name.Identifier.ValueText };
            case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression, Name: IdentifierNameSyntax name } access:
                return Append(Bound(access.Expression), name.Identifier.ValueText);
            case ConditionalAccessExpressionSyntax conditional:
                return Bound(conditional.Expression) is { } names && Bound(conditional.WhenNotNull) is { } bound ? Concat(names, bound) : null;
            default:
                return null;
        }
    }

    private static List<string>? Append(List<string>? names, string name)
    {
        names?.Add(name);
        return names;
    }

    private static List<string> Concat(List<string> names, List<string> more)
    {
        names.AddRange(more);
        return names;
    }

    /// <summary>The expression inside parentheses and a <c>!</c> suffix.</summary>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    break;
                case PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed:
                    expression = suppressed.Operand;
                    break;
                default:
                    return expression;
            }
        }
    }
}
