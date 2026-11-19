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
/// so nothing in the condition runs again. The one exception is the predicate of <c>All</c>, which
/// the line runs again to find the first item that fails. A shape whose values were not all
/// captured adds no line.
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

        if (StringEquality(condition, rewritten, model, token) is { } difference)
        {
            lines.Add(difference);
        }

        if (Collections(condition, rewritten, model, token) is { } collections)
        {
            lines.Add(collections);
        }

        lines.AddRange(Counts(condition, rewritten, model, token));
        if (Search(condition, rewritten, model, render, token) is { } search)
        {
            lines.Add(search);
        }

        return lines;
    }

    /// <summary>
    /// A comparison with <c>a.Count</c>, <c>a.Length</c> or <c>Enumerable.Count(a)</c> on either
    /// side, which lists the items of each such <c>a</c>.
    /// </summary>
    private static IEnumerable<string> Counts(ExpressionSyntax condition, RewrittenCondition rewritten, SemanticModel model, CancellationToken token)
    {
        if (Unwrap(condition) is not BinaryExpressionSyntax comparison ||
            comparison.Kind() is not (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or SyntaxKind.LessThanExpression or
                SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression))
        {
            yield break;
        }

        foreach (var side in new[] { comparison.Left, comparison.Right })
        {
            if (Counted(Unwrap(side), rewritten, model, token) is { } line)
            {
                yield return line;
            }
        }
    }

    private static string? Counted(ExpressionSyntax part, RewrittenCondition rewritten, SemanticModel model, CancellationToken token)
    {
        if (part is MemberAccessExpressionSyntax access && access.IsKind(SyntaxKind.SimpleMemberAccessExpression) &&
            model.GetSymbolInfo(access, token).Symbol is IPropertySymbol
            {
                Name: "Count" or "Length", IsStatic: false, IsIndexer: false, Type.SpecialType: SpecialType.System_Int32 or SpecialType.System_Int64,
            })
        {
            return ListItems(access.Expression, ItemType(model.GetTypeInfo(access.Expression, token).Type), rewritten, model, token);
        }

        if (part is InvocationExpressionSyntax invocation &&
            model.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol { Name: "Count", TypeArguments.Length: 1 } method &&
            IsEnumerable(method.ReducedFrom ?? method) && Operands(invocation, method) is { Others.Count: 0 } operands)
        {
            return ListItems(operands.Source, method.TypeArguments[0], rewritten, model, token);
        }

        return null;
    }

    /// <summary>
    /// <c>a.Any()</c>, <c>a.Any(predicate)</c> and <c>a.Contains(x)</c>, which list the items of
    /// <c>a</c>, and <c>a.All(predicate)</c>, which names the first item that fails. <c>Contains</c>
    /// is matched in <c>Enumerable</c>, in <c>MemoryExtensions</c> and as an instance method that
    /// takes the item type.
    /// </summary>
    private static string? Search(ExpressionSyntax condition, RewrittenCondition rewritten, SemanticModel model, Func<SyntaxNode, string> render, CancellationToken token)
    {
        if (Unwrap(condition) is not InvocationExpressionSyntax invocation ||
            model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method ||
            Operands(invocation, method) is not { } operands)
        {
            return null;
        }

        var definition = method.ReducedFrom ?? method;
        var (source, rest) = operands;
        if (IsEnumerable(definition) && method.TypeArguments.Length == 1)
        {
            var item = method.TypeArguments[0];
            switch (method.Name, rest.Count)
            {
                case ("Any", 0 or 1) or ("Contains", 1):
                    return ListItems(source, item, rewritten, model, token);
                case ("All", 1):
                    source = Unwrap(source);
                    return ConditionRewriter.CanName(item) && IsSource(source, item, model, token) &&
                           Captured(source, rewritten) is { } value && Predicate(Unwrap(rest[0]), condition, rewritten, model, render, token) is { } predicate
                        ? $"FirstFailure<{item.ToDisplayString(TypeFormat)}>({value}, {predicate})"
                        : null;
                default:
                    return null;
            }
        }

        if (method.Name != "Contains" || rest.Count != 1)
        {
            return null;
        }

        if (IsMemoryExtensions(definition) && method.TypeArguments.Length == 1)
        {
            return ListItems(source, method.TypeArguments[0], rewritten, model, token);
        }

        return method is { IsStatic: false, ReducedFrom: null, Parameters.Length: 1 } &&
               ItemType(model.GetTypeInfo(source, token).Type) is { } held && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, held)
            ? ListItems(source, held, rewritten, model, token)
            : null;
    }

    /// <summary>
    /// The text that gives the predicate of <c>All</c> on the failure path: a lambda or a method
    /// group written again, or the captured local of any other delegate. Null for a lambda that
    /// reads a variable the condition declares outside it, which the failure path cannot read.
    /// </summary>
    private static string? Predicate(ExpressionSyntax predicate, ExpressionSyntax condition, RewrittenCondition rewritten, SemanticModel model, Func<SyntaxNode, string> render, CancellationToken token)
    {
        switch (predicate)
        {
            case AnonymousFunctionExpressionSyntax:
                foreach (var identifier in predicate.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    if (model.GetSymbolInfo(identifier, token).Symbol is ILocalSymbol local &&
                        local.DeclaringSyntaxReferences.Any(reference => condition.Span.Contains(reference.Span) && !predicate.Span.Contains(reference.Span)))
                    {
                        return null;
                    }
                }

                return render(predicate);
            case IdentifierNameSyntax when model.GetSymbolInfo(predicate, token).Symbol is IMethodSymbol:
            case MemberAccessExpressionSyntax when model.GetSymbolInfo(predicate, token).Symbol is IMethodSymbol { IsStatic: true }:
                return render(predicate);
            default:
                return Captured(predicate, rewritten);
        }
    }

    /// <summary>The <c>ListItems</c> call for a sequence whose captured value is a sequence of <paramref name="item"/>.</summary>
    private static string? ListItems(ExpressionSyntax source, ITypeSymbol? item, RewrittenCondition rewritten, SemanticModel model, CancellationToken token)
    {
        source = Unwrap(source);
        if (item is null || !ConditionRewriter.CanName(item) || !IsSource(source, item, model, token) || Captured(source, rewritten) is not { } value)
        {
            return null;
        }

        return $"ListItems<{item.ToDisplayString(TypeFormat)}>({CopyBuilder.Literal(ConditionRewriter.OneLine(source))}, {value})";
    }

    /// <summary>A sequence of <paramref name="item"/> that is not a string, whose items are its characters.</summary>
    private static bool IsSource(ExpressionSyntax source, ITypeSymbol item, SemanticModel model, CancellationToken token) =>
        model.GetTypeInfo(source, token).Type?.SpecialType != SpecialType.System_String && IsSequenceOf(source, item, model, token);

    /// <summary>The <c>T</c> of the one <c>IEnumerable&lt;T&gt;</c> the type is or implements, or null.</summary>
    private static ITypeSymbol? ItemType(ITypeSymbol? type)
    {
        if (type is IArrayTypeSymbol { IsSZArray: true } array)
        {
            return array.ElementType;
        }

        if (type is null)
        {
            return null;
        }

        var items = (type is INamedTypeSymbol named ? type.AllInterfaces.Add(named) : type.AllInterfaces)
            .Where(candidate => candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            .Select(candidate => candidate.TypeArguments[0])
            .Distinct<ITypeSymbol>(SymbolEqualityComparer.Default)
            .ToList();
        return items.Count == 1 ? items[0] : null;
    }

    /// <summary>
    /// The sequence a call reads and its other arguments: the receiver for an instance or extension
    /// call, and the first argument for a static call. Null when an argument is named or passed by ref.
    /// </summary>
    private static (ExpressionSyntax Source, IReadOnlyList<ExpressionSyntax> Others)? Operands(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Any(argument => argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None)))
        {
            return null;
        }

        if (!method.IsStatic && invocation.Expression is MemberAccessExpressionSyntax access && access.IsKind(SyntaxKind.SimpleMemberAccessExpression))
        {
            return (access.Expression, arguments.Select(argument => argument.Expression).ToArray());
        }

        return method.IsStatic && arguments.Count > 0 ? (arguments[0].Expression, arguments.Skip(1).Select(argument => argument.Expression).ToArray()) : null;
    }

    private static bool IsEnumerable(IMethodSymbol method) =>
        method.ContainingType is { Name: "Enumerable", ContainingNamespace: { Name: "Linq", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } };

    private static bool IsMemoryExtensions(IMethodSymbol method) =>
        method.ContainingType is { Name: "MemoryExtensions", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };

    /// <summary>
    /// <c>a.SequenceEqual(b)</c> from <c>Enumerable</c> or <c>MemoryExtensions</c>, without a
    /// comparer, in extension or static form, and <c>a.SetEquals(b)</c> on any set.
    /// </summary>
    private static string? Collections(ExpressionSyntax condition, RewrittenCondition rewritten, SemanticModel model, CancellationToken token)
    {
        if (Unwrap(condition) is not InvocationExpressionSyntax invocation ||
            model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method ||
            Operands(invocation, method) is not { Others.Count: 1 } operands)
        {
            return null;
        }

        var (first, second) = (operands.Source, operands.Others[0]);
        var definition = method.ReducedFrom ?? method;
        string call;
        ITypeSymbol item;
        if (method.Name == "SequenceEqual" && definition.Parameters.Length == 2 && method.TypeArguments.Length == 1 &&
            (IsEnumerable(definition) || IsMemoryExtensions(definition)))
        {
            call = "SequenceDifference";
            item = method.TypeArguments[0];
        }
        else if (method is { Name: "SetEquals", IsStatic: false, MethodKind: MethodKind.Ordinary, Parameters.Length: 1 } &&
                 method.Parameters[0].Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } other)
        {
            call = "SetDifference";
            item = other.TypeArguments[0];
        }
        else
        {
            return null;
        }

        first = Unwrap(first);
        second = Unwrap(second);
        if (!ConditionRewriter.CanName(item) ||
            !IsSequenceOf(first, item, model, token) || !IsSequenceOf(second, item, model, token) ||
            Captured(first, rewritten) is not { } firstValue || Captured(second, rewritten) is not { } secondValue)
        {
            return null;
        }

        return $"{call}<{item.ToDisplayString(TypeFormat)}>({CopyBuilder.Literal(ConditionRewriter.OneLine(first))}, {firstValue}, " +
               $"{CopyBuilder.Literal(ConditionRewriter.OneLine(second))}, {secondValue})";
    }

    /// <summary>Whether the part's own type converts to <c>IEnumerable&lt;item&gt;</c>, as the captured local must.</summary>
    private static bool IsSequenceOf(ExpressionSyntax part, ITypeSymbol item, SemanticModel model, CancellationToken token)
    {
        var sequence = model.Compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T).Construct(item);
        return model.GetTypeInfo(part, token).Type is { } type && model.Compilation.ClassifyConversion(type, sequence) is { IsImplicit: true, IsUserDefined: false };
    }

    /// <summary><c>a == b</c> on two strings, with the string equality operator.</summary>
    private static string? StringEquality(ExpressionSyntax condition, RewrittenCondition rewritten, SemanticModel model, CancellationToken token)
    {
        if (Unwrap(condition) is not BinaryExpressionSyntax equality ||
            !equality.IsKind(SyntaxKind.EqualsExpression) ||
            model.GetSymbolInfo(equality, token).Symbol is not IMethodSymbol { ContainingType.SpecialType: SpecialType.System_String } ||
            ReadString(Unwrap(equality.Left), rewritten, model, token) is not { } left ||
            ReadString(Unwrap(equality.Right), rewritten, model, token) is not { } right)
        {
            return null;
        }

        return $"StringDifference({left}, {right})";
    }

    /// <summary>
    /// The text that reads a string operand on the failure path: its captured local, or its constant
    /// value as a literal. Null when the operand was not captured or is the constant <c>null</c>.
    /// </summary>
    private static string? ReadString(ExpressionSyntax part, RewrittenCondition rewritten, SemanticModel model, CancellationToken token)
    {
        if (model.GetConstantValue(part, token) is { HasValue: true } constant)
        {
            return constant.Value is string text ? CopyBuilder.Literal(text) : null;
        }

        return model.GetTypeInfo(part, token).Type?.SpecialType == SpecialType.System_String ? Captured(part, rewritten) : null;
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
