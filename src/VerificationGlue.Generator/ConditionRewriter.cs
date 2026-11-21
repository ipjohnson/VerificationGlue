using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace VerificationGlue.Generator;

/// <summary>A local that holds one captured value, declared before the rewritten <c>if</c>.</summary>
internal sealed record CaptureLocal(string Name, string Declaration);

/// <summary>
/// One line of the failure message: a captured value, or a part whose value is not captured.
/// </summary>
/// <param name="Capture">The local that holds the value, or null when the value is unavailable.</param>
/// <param name="Guard">A condition over captured locals that is true when the part ran, or null when it always ran.</param>
internal sealed record ValueRow(TextSpan Span, string Label, string? Capture, string? Guard);

/// <summary>A part that holds a value but cannot be captured. VG0006 reports it.</summary>
internal sealed record Uncaptured(Location Location, string Text, string Reason);

/// <summary>The condition of one <c>Verify</c> or <c>Check</c> with each part assigned to a local as it runs.</summary>
internal sealed record RewrittenCondition(string Text, IReadOnlyList<CaptureLocal> Locals, IReadOnlyList<ValueRow> Rows, IReadOnlyList<Uncaptured> Uncaptured);

/// <summary>
/// Wraps each part of a condition in an assignment to a typed local, <c>(v = part)</c>. The
/// condition keeps its shape, so each part runs as often and in the same order as before, and
/// <c>&amp;&amp;</c>, <c>||</c>, <c>?:</c> and <c>??</c> still skip what they skipped.
/// </summary>
/// <remarks>
/// Every occurrence of a part gets its own local, so two reads of a variable with a change between
/// them keep their two values. The failure path reads only the locals. A part is left as written
/// and reported as unavailable when capturing it would change what the code does: a
/// <c>ref struct</c> or pointer value, a receiver that is a mutable struct, a part whose type has
/// no name and that declares or uses a variable of the condition, and the members of a property
/// pattern.
/// </remarks>
internal sealed class ConditionRewriter
{
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private readonly SemanticModel _model;
    private readonly Func<SyntaxNode, string> _render;
    private readonly Func<ExpressionSyntax, bool> _keepWhole;
    private readonly string _prefix;
    private readonly CancellationToken _token;
    private readonly List<CaptureLocal> _locals = new();
    private readonly List<ValueRow> _rows = new();
    private readonly List<Uncaptured> _uncaptured = new();
    private TextSpan _condition;

    /// <param name="render">The text of a node as the copy writes it, with the copy's call changes applied.</param>
    /// <param name="keepWhole">True for an invocation or member access that the copy changes as a whole, so its parts are not captured.</param>
    /// <param name="prefix">The prefix of the local names, unique per rewritten call in the method.</param>
    public ConditionRewriter(SemanticModel model, Func<SyntaxNode, string> render, Func<ExpressionSyntax, bool> keepWhole, string prefix, CancellationToken token)
    {
        _model = model;
        _render = render;
        _keepWhole = keepWhole;
        _prefix = prefix;
        _token = token;
    }

    public RewrittenCondition Rewrite(ExpressionSyntax condition)
    {
        _condition = condition.Span;
        var text = Visit(condition, null, capture: false).Text;
        var rows = _rows.OrderBy(row => row.Span.Start).ThenByDescending(row => row.Span.Length).ToArray();
        return new RewrittenCondition(text, _locals.ToArray(), rows, _uncaptured.ToArray());
    }

    /// <summary>The source text on one line, as a message label shows it.</summary>
    public static string OneLine(SyntaxNode node)
    {
        var text = node.ToString();
        if (text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0)
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        var space = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                space = true;
                continue;
            }

            if (space && builder.Length > 0)
            {
                builder.Append(' ');
            }

            space = false;
            builder.Append(character);
        }

        return builder.ToString();
    }

    private (string Text, string? Capture) Visit(ExpressionSyntax expression, string? guard, bool capture)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
            {
                var inner = Visit(parenthesized.Expression, guard, capture);
                return ("(" + inner.Text + ")", inner.Capture);
            }

            case PostfixUnaryExpressionSyntax suppress when suppress.IsKind(SyntaxKind.SuppressNullableWarningExpression):
            {
                var inner = Visit(suppress.Operand, guard, capture);
                return (inner.Text + "!", inner.Capture);
            }
        }

        if (IsConstant(expression))
        {
            return (_render(expression), null);
        }

        var text = Shape(expression, guard);
        return capture ? Capture(expression, text, guard) : (text, null);
    }

    /// <summary>The expression with its parts captured, without capturing the expression itself.</summary>
    private string Shape(ExpressionSyntax expression, string? guard)
    {
        switch (expression)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression):
            {
                if (!IsBuiltInBoolean(binary))
                {
                    break;
                }

                var left = Visit(binary.Left, guard, capture: true);
                var ran = left.Capture ?? (IsConstant(binary.Left) ? _render(binary.Left) : null);
                if (ran is null)
                {
                    break;
                }

                var rightGuard = And(guard, binary.IsKind(SyntaxKind.LogicalOrExpression) ? "!" + Paren(ran) : ran);
                return left.Text + " " + binary.OperatorToken.Text + " " + Visit(binary.Right, rightGuard, capture: true).Text;
            }

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.CoalesceExpression):
            {
                var left = Visit(binary.Left, guard, capture: true);
                if (left.Capture is null)
                {
                    break;
                }

                return left.Text + " ?? " + Visit(binary.Right, And(guard, left.Capture + " is null"), capture: true).Text;
            }

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.IsExpression) || binary.IsKind(SyntaxKind.AsExpression):
                return Visit(binary.Left, guard, capture: true).Text + " " + binary.OperatorToken.Text + " " + _render(binary.Right);

            case BinaryExpressionSyntax binary:
                return Visit(binary.Left, guard, capture: true).Text + " " + binary.OperatorToken.Text + " " + Visit(binary.Right, guard, capture: true).Text;

            case ConditionalExpressionSyntax conditional:
            {
                var test = Visit(conditional.Condition, guard, capture: true);
                var ran = test.Capture ?? (IsConstant(conditional.Condition) ? _render(conditional.Condition) : null);
                if (ran is null)
                {
                    break;
                }

                return test.Text + " ? " + Visit(conditional.WhenTrue, And(guard, ran), capture: true).Text +
                    " : " + Visit(conditional.WhenFalse, And(guard, "!" + Paren(ran)), capture: true).Text;
            }

            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.LogicalNotExpression) || unary.IsKind(SyntaxKind.UnaryMinusExpression) ||
                                                        unary.IsKind(SyntaxKind.UnaryPlusExpression) || unary.IsKind(SyntaxKind.BitwiseNotExpression):
                return unary.OperatorToken.Text + Visit(unary.Operand, guard, capture: true).Text;

            case IsPatternExpressionSyntax pattern:
            {
                var subject = Visit(pattern.Expression, guard, capture: true);
                PatternMembers(pattern.Expression, pattern.Pattern, guard);
                return subject.Text + " is " + _render(pattern.Pattern);
            }

            case CastExpressionSyntax cast:
                return "(" + _render(cast.Type) + ")" + Visit(cast.Expression, guard, capture: true).Text;

            case AwaitExpressionSyntax awaited:
                return "await " + Visit(awaited.Expression, guard, capture: true).Text;

            case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) && !_keepWhole(access):
                return Receiver(access.Expression, access, guard) + "." + _render(access.Name);

            case ElementAccessExpressionSyntax element:
                return Receiver(element.Expression, element, guard) + Arguments(element, element.ArgumentList.Arguments, "[", "]", guard);

            case InvocationExpressionSyntax invocation when !_keepWhole(invocation):
            {
                var callee = invocation.Expression is MemberAccessExpressionSyntax access && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                    ? Receiver(access.Expression, invocation, guard) + "." + _render(access.Name)
                    : _render(invocation.Expression);
                return callee + Arguments(invocation, invocation.ArgumentList.Arguments, "(", ")", guard);
            }
        }

        return _render(expression);
    }

    /// <summary>
    /// The receiver of a member access, element access or call. It is captured only when a copy of
    /// it behaves the same, which holds for a reference type, a read-only struct, or a member that
    /// cannot change the struct.
    /// </summary>
    private string Receiver(ExpressionSyntax receiver, ExpressionSyntax access, string? guard)
    {
        if (_model.GetSymbolInfo(receiver, _token).Symbol is INamespaceOrTypeSymbol || receiver is ThisExpressionSyntax or BaseExpressionSyntax)
        {
            return _render(receiver);
        }

        var type = _model.GetTypeInfo(receiver, _token).Type;
        if (type is null || type.TypeKind == TypeKind.Error)
        {
            return Visit(receiver, guard, capture: false).Text;
        }

        if (CanCopyReceiver(type, _model.GetSymbolInfo(access, _token).Symbol))
        {
            return Visit(receiver, guard, capture: true).Text;
        }

        var text = Visit(receiver, guard, capture: false).Text;
        if (!IsConstant(receiver))
        {
            NotCaptured(receiver, guard, type.IsRefLikeType ? "its type is a ref struct" : "it is a mutable struct, and a copy would take the change");
        }

        return text;
    }

    private string Arguments(ExpressionSyntax call, SeparatedSyntaxList<ArgumentSyntax> arguments, string open, string close, string? guard)
    {
        var parameters = _model.GetSymbolInfo(call, _token).Symbol switch
        {
            IMethodSymbol method => method.Parameters,
            IPropertySymbol indexer => indexer.Parameters,
            _ => default,
        };
        var texts = new List<string>(arguments.Count);
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            var prefix = argument.NameColon is null ? "" : _render(argument.NameColon) + " ";
            if (!argument.RefKindKeyword.IsKind(SyntaxKind.None))
            {
                texts.Add(_render(argument));
                continue;
            }

            texts.Add(prefix + Visit(argument.Expression, guard, capture: ByValue(parameters, argument, index)).Text);
        }

        return open + string.Join(", ", texts) + close;
    }

    /// <summary>
    /// Whether the argument binds to a parameter taken by value. Capturing an argument to an
    /// <c>in</c> parameter would pass a copy in place of the variable.
    /// </summary>
    private static bool ByValue(System.Collections.Immutable.ImmutableArray<IParameterSymbol> parameters, ArgumentSyntax argument, int index)
    {
        if (parameters.IsDefaultOrEmpty)
        {
            return false;
        }

        IParameterSymbol? parameter = null;
        if (argument.NameColon is { } name)
        {
            parameter = parameters.FirstOrDefault(candidate => candidate.Name == name.Name.Identifier.ValueText);
        }
        else if (index < parameters.Length)
        {
            parameter = parameters[index];
        }
        else if (parameters[parameters.Length - 1].IsParams)
        {
            parameter = parameters[parameters.Length - 1];
        }

        return parameter is { RefKind: RefKind.None };
    }

    /// <summary>
    /// The members a property pattern tests. A pattern can only bind a value to a variable that is
    /// assigned when the pattern matches, so the failure path cannot read them. They are reported
    /// as unavailable, and their getters are never called again.
    /// </summary>
    private void PatternMembers(ExpressionSyntax subject, PatternSyntax pattern, string? guard)
    {
        if (pattern is RecursivePatternSyntax { PropertyPatternClause: { } clause })
        {
            foreach (var member in clause.Subpatterns)
            {
                if (member.ExpressionColon is { } name)
                {
                    _rows.Add(new ValueRow(name.Expression.Span, OneLine(subject) + "." + OneLine(name.Expression), null, guard));
                }
            }
        }
    }

    private (string, string?) Capture(ExpressionSyntax expression, string text, string? guard)
    {
        if (!HoldsValue(expression, out var type))
        {
            return (text, null);
        }

        if (type.IsRefLikeType || type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
        {
            NotCaptured(expression, guard, type.IsRefLikeType ? "its type is a ref struct" : "its type is a pointer");
            return (text, null);
        }

        var name = _prefix + _locals.Count;
        if (type.TypeKind == TypeKind.Dynamic || !CanName(type) || HasUnboundPart(expression))
        {
            // A type with no name, such as an anonymous type or a member another generator adds,
            // is declared from the part itself without running it. So is a part that uses such a
            // member, because error recovery can bind a later call to an unrelated extension
            // method: in calc.Calls().Count(), Count() binds to Enumerable.Count.
            if (DeclaresOrUsesConditionVariables(expression))
            {
                NotCaptured(expression, guard, "its type has no name and it uses a variable that the condition declares");
                return (text, null);
            }

            _locals.Add(new CaptureLocal(name, $"var {name} = false ? ({_render(expression)}) : default;"));
        }
        else
        {
            _locals.Add(new CaptureLocal(name, $"{type.ToDisplayString(TypeFormat)} {name} = default!;"));
        }

        _rows.Add(new ValueRow(expression.Span, OneLine(expression), name, guard));
        return ($"({name} = {text})", name);
    }

    /// <summary>
    /// Whether the expression is a value worth a line in the message: not a type, a method group, a
    /// lambda, <c>this</c>, or an expression that takes its type from where it is used.
    /// </summary>
    private bool HoldsValue(ExpressionSyntax expression, out ITypeSymbol type)
    {
        type = null!;
        if (expression is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax or ThisExpressionSyntax or BaseExpressionSyntax or
            ThrowExpressionSyntax or TupleExpressionSyntax or ImplicitObjectCreationExpressionSyntax or CollectionExpressionSyntax or
            StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax or DeclarationExpressionSyntax or
            RefExpressionSyntax or LiteralExpressionSyntax)
        {
            return false;
        }

        var info = _model.GetTypeInfo(expression, _token);
        if (info.Type is not { } natural || natural.SpecialType == SpecialType.System_Void)
        {
            return false;
        }

        if (expression is InterpolatedStringExpressionSyntax && info.ConvertedType?.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        if (natural.TypeKind != TypeKind.Error && _model.GetSymbolInfo(expression, _token).Symbol is { } symbol &&
            symbol is not (ILocalSymbol or IParameterSymbol or IFieldSymbol { IsConst: false } or IPropertySymbol or IMethodSymbol { MethodKind: not MethodKind.Ordinary } or IMethodSymbol { ReturnsVoid: false }))
        {
            return false;
        }

        type = natural;
        return true;
    }

    private static bool CanCopyReceiver(ITypeSymbol type, ISymbol? member)
    {
        if (type.IsRefLikeType)
        {
            return false;
        }

        if (type.IsReferenceType || type.TypeKind == TypeKind.Enum || (type.IsValueType && type.IsReadOnly))
        {
            return true;
        }

        return member switch
        {
            IFieldSymbol => true,
            IPropertySymbol property => property.GetMethod?.IsReadOnly == true,
            IMethodSymbol { ReducedFrom: { } extension } => extension.Parameters[0].RefKind is RefKind.None or RefKind.In or RefKind.RefReadOnlyParameter,
            IMethodSymbol method => method.IsReadOnly,
            _ => false,
        };
    }

    private bool HasUnboundPart(ExpressionSyntax expression) =>
        expression.DescendantNodes().OfType<ExpressionSyntax>().Any(node => _model.GetTypeInfo(node, _token).Type is { TypeKind: TypeKind.Error });

    private bool DeclaresOrUsesConditionVariables(ExpressionSyntax expression)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is DeclarationExpressionSyntax or SingleVariableDesignationSyntax)
            {
                return true;
            }

            if (node is IdentifierNameSyntax identifier && _model.GetSymbolInfo(identifier, _token).Symbol is ILocalSymbol local &&
                local.DeclaringSyntaxReferences.Any(reference => _condition.Contains(reference.Span)))
            {
                return true;
            }
        }

        return false;
    }

    private void NotCaptured(ExpressionSyntax expression, string? guard, string reason)
    {
        _rows.Add(new ValueRow(expression.Span, OneLine(expression), null, guard));
        _uncaptured.Add(new Uncaptured(expression.GetLocation(), OneLine(expression), reason));
    }

    private bool IsConstant(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax || _model.GetConstantValue(expression, _token).HasValue;

    private bool IsBuiltInBoolean(BinaryExpressionSyntax binary) =>
        _model.GetSymbolInfo(binary, _token).Symbol is not IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator } &&
        _model.GetTypeInfo(binary.Left, _token).Type?.SpecialType == SpecialType.System_Boolean &&
        _model.GetTypeInfo(binary.Right, _token).Type?.SpecialType == SpecialType.System_Boolean;

    /// <summary>Whether a local can be declared with the type's name.</summary>
    internal static bool CanName(ITypeSymbol type) => type switch
    {
        IErrorTypeSymbol => false,
        IArrayTypeSymbol array => CanName(array.ElementType),
        INamedTypeSymbol named => !named.IsAnonymousType && named.TypeArguments.All(CanName) &&
                                  (named.ContainingType is null || CanName(named.ContainingType)),
        _ => type.TypeKind != TypeKind.Error,
    };

    private static string And(string? guard, string term) => guard is null ? term : guard + " && " + Paren(term);

    private static string Paren(string text) => text.All(character => char.IsLetterOrDigit(character) || character == '_') ? text : "(" + text + ")";
}
