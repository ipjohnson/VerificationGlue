using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using VerificationGlue.Shared;

namespace VerificationGlue.Generator;

/// <summary>
/// Writes the tier 3 copy of one method. The body is the original text with these changes:
/// each <c>Verify</c> or <c>Check</c> statement becomes an <c>if</c> that captures values; a
/// <c>Verify</c> or <c>Check</c> call that cannot be rewritten, such as one in a lambda, and every
/// other <c>VerifySyntax</c> call, such as <c>Throws</c> or <c>Single</c>, calls its tier 2 form; a direct call to a method that tier 3 rewrites
/// calls that method's copy; and a call that leaves out a <c>[CallerMemberName]</c> argument passes
/// the original method name. <c>#line</c> directives map the text to the original file.
/// </summary>
internal sealed class CopyBuilder
{
    private const string Runtime = "global::VerificationGlue.VerifyFailure";

    private readonly SemanticModel _model;
    private readonly CancellationToken _token;
    private readonly SourceText _text;
    private readonly List<Edit> _edits = new();
    private readonly HashSet<InvocationExpressionSyntax> _wholeEdits = new();
    private readonly string _methodName;
    private int _sites;
    private string? _linePath;

    private CopyBuilder(SemanticModel model, MethodDeclarationSyntax declaration, CancellationToken token)
    {
        _model = model;
        _token = token;
        _text = declaration.SyntaxTree.GetText(token);
        _methodName = declaration.Identifier.ValueText;
    }

    /// <summary>The copy of <paramref name="method"/>, or null when it has no body to copy.</summary>
    public static MethodModel? Build(MethodDeclarationSyntax declaration, IMethodSymbol method, SemanticModel model, CancellationToken token)
    {
        SyntaxNode? body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody;
        if (body is null)
        {
            return null;
        }

        var builder = new CopyBuilder(model, declaration, token);
        builder.CollectEdits(body, method);

        var path = declaration.SyntaxTree.FilePath;
        var types = new List<TypeShell>();
        var namespaces = new List<NamespaceShell>();
        var root = (CompilationUnitSyntax)declaration.SyntaxTree.GetRoot(token);
        namespaces.Add(new NamespaceShell(null, false, Usings(root.Externs, root.Usings)));
        foreach (var ancestor in declaration.Ancestors().Reverse())
        {
            switch (ancestor)
            {
                case BaseNamespaceDeclarationSyntax ns:
                    namespaces.Add(new NamespaceShell(ns.Name.ToString(), ns is FileScopedNamespaceDeclarationSyntax, Usings(ns.Externs, ns.Usings)));
                    break;
                case TypeDeclarationSyntax type:
                    types.Add(new TypeShell(Keyword(type), type.Identifier.ValueText));
                    break;
            }
        }

        var signature = builder.Signature(declaration, method);
        var bodyText = builder.Body(declaration, body, method);
        var line = declaration.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var hint = InterceptorEmitter.HintName(path).Replace(".Interceptors.g.cs", "") + "." +
                   string.Join(".", types.Select(type => type.Name)) + "." + method.Name + "." + line.ToString(CultureInfo.InvariantCulture) + ".Verified.g.cs";
        return new MethodModel(hint, new EquatableArray<NamespaceShell>(namespaces.ToArray()), new EquatableArray<TypeShell>(types.ToArray()), signature, bodyText);
    }

    /// <summary>A C# string literal.</summary>
    public static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    /// <summary>Where a path goes in the body, to be replaced by the path the message shows.</summary>
    public static string PathPlaceholder(string path) => CopyEmitter.PathMarker + path + CopyEmitter.PathMarker;

    internal static EquatableArray<string> Usings(SyntaxList<ExternAliasDirectiveSyntax> externs, SyntaxList<UsingDirectiveSyntax> usings) =>
        new(externs.Select(directive => directive.ToString())
            .Concat(usings.Where(directive => directive.GlobalKeyword.IsKind(SyntaxKind.None)).Select(directive => directive.ToString()))
            .ToArray());

    private static string Keyword(TypeDeclarationSyntax type)
    {
        var keyword = type is RecordDeclarationSyntax record
            ? "record " + (record.ClassOrStructKeyword.IsKind(SyntaxKind.None) ? "class" : record.ClassOrStructKeyword.ValueText)
            : type.Keyword.ValueText;
        if (type.Modifiers.Any(SyntaxKind.RefKeyword))
        {
            keyword = "ref " + keyword;
        }

        if (type.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            keyword = "readonly " + keyword;
        }

        if (type.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            keyword = "static " + keyword;
        }

        return keyword;
    }

    private string Signature(MethodDeclarationSyntax declaration, IMethodSymbol method)
    {
        var visible = method.DeclaredAccessibility == Accessibility.Public;
        for (var type = method.ContainingType; type is not null && visible; type = type.ContainingType)
        {
            visible = type.DeclaredAccessibility == Accessibility.Public;
        }

        var modifiers = new List<string> { visible ? "public" : "internal" };
        foreach (var kind in new[] { SyntaxKind.StaticKeyword, SyntaxKind.UnsafeKeyword, SyntaxKind.AsyncKeyword })
        {
            if (declaration.Modifiers.Any(kind))
            {
                modifiers.Add(SyntaxFacts.GetText(kind));
            }
        }

        return $"[global::VerificationGlue.VerifiedCopy({VerifyRewriteRules.ContractVersion})]\n" +
               "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]\n" +
               string.Join(" ", modifiers) + " " + declaration.ReturnType + " " + method.Name + VerifyRewriteRules.Suffix + declaration.ParameterList;
    }

    private string Body(MethodDeclarationSyntax declaration, SyntaxNode body, IMethodSymbol method)
    {
        var text = new StringBuilder();
        if (body is BlockSyntax)
        {
            text.Append(LineDirective(body.GetLocation().GetMappedLineSpan().StartLinePosition, declaration.SyntaxTree.FilePath, body.GetLocation()));
            text.Append(Render(body.Span));
        }
        else
        {
            var expression = ((ArrowExpressionClauseSyntax)body).Expression;
            text.Append("{\n");
            text.Append(LineDirective(expression.GetLocation().GetMappedLineSpan().StartLinePosition, declaration.SyntaxTree.FilePath, expression.GetLocation()));
            text.Append(ReturnsNothing(method) ? "" : "return ").Append(Render(expression.Span)).Append(";\n#line default\n}");
            return text.ToString();
        }

        return text.Append("\n#line default").ToString();
    }

    /// <summary>
    /// A <c>#line</c> directive for the next line, then spaces up to the original column when
    /// <paramref name="indent"/> is true. The file name is written only when it changes, because a
    /// directive without one keeps the last.
    /// </summary>
    private string LineDirective(LinePosition position, string fallbackPath, Location location, bool indent = true)
    {
        var mapped = location.GetMappedLineSpan();
        var path = mapped.HasMappedPath ? mapped.Path : fallbackPath;
        var file = path == _linePath ? "" : " " + Literal(path);
        _linePath = path;
        return $"\n#line {(position.Line + 1).ToString(CultureInfo.InvariantCulture)}{file}\n" + (indent ? new string(' ', position.Character) : "");
    }

    private static bool ReturnsNothing(IMethodSymbol method) =>
        method.ReturnsVoid ||
        (method.IsAsync && method.ReturnType is INamedTypeSymbol { Arity: 0, Name: "Task" or "ValueTask", ContainingNamespace: { Name: "Tasks" } });

    private void CollectEdits(SyntaxNode body, IMethodSymbol method)
    {
        foreach (var invocation in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (_model.GetSymbolInfo(invocation, _token).Symbol is not IMethodSymbol target)
            {
                continue;
            }

            if (VerifyCalls.KindOf(target) is { } kind)
            {
                AddVerifyEdit(invocation, target, kind, body, method);
                continue;
            }

            if (VerifyRewriteRules.WillRewrite(target) && CalledName(invocation) is { } name)
            {
                _edits.Add(new Edit(name.Span, () => name.ValueText + VerifyRewriteRules.Suffix));
            }

            if (target.Parameters.Length > invocation.ArgumentList.Arguments.Count &&
                target.Parameters.Any(parameter => parameter.IsOptional && IsCallerMemberName(parameter)) &&
                _model.GetOperation(invocation, _token) is IInvocationOperation operation)
            {
                foreach (var argument in operation.Arguments)
                {
                    if (argument.ArgumentKind == ArgumentKind.DefaultValue && argument.Parameter is { } parameter && IsCallerMemberName(parameter))
                    {
                        var close = invocation.ArgumentList.CloseParenToken.SpanStart;
                        var separator = invocation.ArgumentList.Arguments.Count > 0 ? ", " : "";
                        var parameterName = SyntaxFacts.GetKeywordKind(parameter.Name) == SyntaxKind.None ? parameter.Name : "@" + parameter.Name;
                        _edits.Add(new Edit(new TextSpan(close, 0), () => $"{separator}{parameterName}: {Literal(_methodName)}"));
                        _wholeEdits.Add(invocation);
                    }
                }
            }
        }

        _edits.Sort((left, right) => left.Span.Start != right.Span.Start
            ? left.Span.Start.CompareTo(right.Span.Start)
            : right.Span.Length.CompareTo(left.Span.Length));
    }

    private static bool IsCallerMemberName(IParameterSymbol parameter) =>
        parameter.GetAttributes().Any(attribute => attribute.AttributeClass is
        {
            Name: "CallerMemberNameAttribute",
            ContainingNamespace: { Name: "CompilerServices", ContainingNamespace: { Name: "Runtime", ContainingNamespace.Name: "System" } },
        });

    private static SyntaxToken? CalledName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier,
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier,
        MemberBindingExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier,
        _ => null,
    };

    private void AddVerifyEdit(InvocationExpressionSyntax invocation, IMethodSymbol target, CallKind kind, SyntaxNode body, IMethodSymbol method)
    {
        _wholeEdits.Add(invocation);
        if (Tier3Statement(invocation, kind, body, method, out var condition) is { } statement)
        {
            var site = _sites++;
            var span = statement is ArrowExpressionClauseSyntax arrow ? arrow.Expression.Span : statement.Span;
            _edits.Add(new Edit(span, () => Tier3(invocation, kind, condition, statement, site)));
            return;
        }

        _edits.Add(new Edit(invocation.Span, () => Tier2(invocation, target, kind)));
    }

    /// <summary>
    /// The statement that tier 3 replaces for a call in a copied body, or null when the call keeps
    /// its tier 2 form. A lambda's body is rewritten in place of the call unless it declares a
    /// variable, which the method's scope could already hold. The call must be a <c>Verify</c> or <c>Check</c> statement outside lambdas
    /// and local functions, with its source text left to the compiler and, for a message, an
    /// interpolated string.
    /// </summary>
    public static SyntaxNode? Tier3Statement(InvocationExpressionSyntax invocation, CallKind kind, SyntaxNode body, IMethodSymbol method, out ExpressionSyntax condition)
    {
        condition = null!;
        if (kind is CallKind.VerifyLambda or CallKind.CheckLambda)
        {
            if (VerifyCalls.RewritableStatement(invocation, body) is { } lambdaStatement &&
                (lambdaStatement is not ArrowExpressionClauseSyntax || ReturnsNothing(method)) &&
                invocation.ArgumentList.Arguments.Count == 1 &&
                VerifyCalls.LambdaBody(VerifyCalls.Argument(invocation, VerifyCalls.ConditionParameter)) is { } lambdaBody &&
                !lambdaBody.DescendantNodesAndSelf().Any(node => node is SingleVariableDesignationSyntax or QueryExpressionSyntax))
            {
                condition = lambdaBody;
                return lambdaStatement;
            }

            return null;
        }

        if (kind is CallKind.Verify or CallKind.VerifyWithMessage or CallKind.Check &&
            VerifyCalls.RewritableStatement(invocation, body) is { } statement &&
            (statement is not ArrowExpressionClauseSyntax || ReturnsNothing(method)) &&
            VerifyCalls.Argument(invocation, VerifyCalls.ConditionParameter) is { } found &&
            VerifyCalls.Argument(invocation, VerifyCalls.ExpressionParameter, kind == CallKind.VerifyWithMessage ? 2 : 1) is null &&
            (kind != CallKind.VerifyWithMessage || VerifyCalls.Argument(invocation, VerifyCalls.MessageParameter, 1) is InterpolatedStringExpressionSyntax))
        {
            condition = found;
            return statement;
        }

        return null;
    }

    /// <summary>The <c>if</c> that replaces a <c>Verify</c> or <c>Check</c> statement.</summary>
    private string Tier3(InvocationExpressionSyntax call, CallKind kind, ExpressionSyntax condition, SyntaxNode statement, int site)
    {
        var rewritten = new ConditionRewriter(_model, RenderForCondition, node => node is InvocationExpressionSyntax invocation && _wholeEdits.Contains(invocation), $"__vg{site.ToString(CultureInfo.InvariantCulture)}_", _token).Rewrite(condition);
        var path = call.SyntaxTree.FilePath;
        var start = statement.GetLocation().GetMappedLineSpan();
        var callLine = call.GetLocation().GetMappedLineSpan();
        var mappedPath = callLine.HasMappedPath ? callLine.Path : path;
        var embedded = statement is ExpressionStatementSyntax && statement.Parent is not (BlockSyntax or SwitchSectionSyntax);
        var indent = new string(' ', start.StartLinePosition.Character);
        var next = "\n" + indent + "        .";

        var failure = new StringBuilder(Runtime)
            .Append(VerifyCalls.IsCheck(kind) ? ".StartCheck(" : ".Start(")
            .Append(Literal(condition.ToString())).Append(", ").Append(PathPlaceholder(mappedPath)).Append(", ")
            .Append((callLine.StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture)).Append(')');
        foreach (var explanation in Explanations.For(condition, rewritten, _model, RenderForCondition, _token))
        {
            failure.Append(next).Append(explanation);
        }

        foreach (var row in rewritten.Rows)
        {
            failure.Append(next).Append(row.Capture is null ? "NotCaptured(" : "Value(").Append(Literal(row.Label));
            if (row.Capture is not null)
            {
                failure.Append(", ").Append(row.Capture);
            }

            if (row.Guard is not null)
            {
                failure.Append(", evaluated: ").Append(row.Guard);
            }

            failure.Append(')');
        }

        if (kind == CallKind.VerifyWithMessage && VerifyCalls.Argument(call, VerifyCalls.MessageParameter, 1) is { } message)
        {
            failure.Append(next).Append("Message(global::System.String.Create(global::System.Globalization.CultureInfo.InvariantCulture, ")
                .Append(Render(message.Span)).Append("))");
        }

        if (VerifyCalls.ChainedName(call) is { } chained && call.Parent!.Parent is InvocationExpressionSyntax chain)
        {
            failure.Append(next).Append(chained).Append('(').Append(string.Join(", ", chain.ArgumentList.Arguments.Select(argument => Render(argument.Expression.Span)))).Append(')');
        }

        var text = new StringBuilder();
        text.Append(embedded ? "{" : "").Append("\n#line hidden\n");
        foreach (var local in rewritten.Locals)
        {
            text.Append(indent).Append(local.Declaration).Append('\n');
        }

        text.Append(LineDirective(start.StartLinePosition, path, statement.GetLocation()).TrimStart('\n'))
            .Append("if (!(").Append(rewritten.Text).Append("))\n")
            .Append("#line hidden\n").Append(indent).Append("{\n")
            .Append(indent).Append("    ").Append(VerifyCalls.IsCheck(kind) ? "" : "throw ")
            .Append(failure)
            .Append(VerifyCalls.IsCheck(kind) ? ".Report();" : ".Build();")
            .Append('\n').Append(indent).Append("}\n").Append(embedded ? "}\n" : "");
        text.Append(LineDirective(start.EndLinePosition, path, statement.GetLocation(), indent: false).TrimStart('\n'));
        return text.ToString();
    }

    /// <summary>The tier 2 form of a call that tier 3 cannot rewrite, with its message fixed at compile time.</summary>
    private string Tier2(InvocationExpressionSyntax call, IMethodSymbol target, CallKind kind)
    {
        var mapped = call.GetLocation().GetMappedLineSpan();
        var where = PathPlaceholder(mapped.HasMappedPath ? mapped.Path : call.SyntaxTree.FilePath) + ", " +
                    (mapped.StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture);
        switch (kind)
        {
            case CallKind.Throws or CallKind.ThrowsAsync or CallKind.ThrowsExactly or CallKind.ThrowsExactlyAsync:
            {
                var action = VerifyCalls.Argument(call, VerifyCalls.ActionParameter)!;
                var expression = VerifyCalls.Argument(call, VerifyCalls.ExpressionParameter, 1);
                var typeArguments = call.Expression switch
                {
                    GenericNameSyntax generic => Render(generic.TypeArgumentList.Span),
                    MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => Render(generic.TypeArgumentList.Span),
                    _ => "<" + target.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">",
                };
                var message = Shapes.Throws(action, target.TypeArguments[0], kind is CallKind.ThrowsExactly or CallKind.ThrowsExactlyAsync);
                return $"{Runtime}.{kind}{typeArguments}({Render(action.Span)}, " +
                       $"{(expression is null ? Literal(action.ToString()) : Render(expression.Span))}, {LiteralOrNull(message)}, {where})";
            }

            case CallKind.VerifyEquivalent or CallKind.CheckEquivalent:
            {
                var actual = VerifyCalls.Argument(call, VerifyCalls.ActualParameter, 0)!;
                var expected = VerifyCalls.Argument(call, VerifyCalls.ExpectedParameter, 1)!;
                var options = VerifyCalls.Options(call, target);
                var first = target.Parameters.Length == 5 ? 3 : 2;
                var actualExpression = VerifyCalls.Argument(call, VerifyCalls.ActualExpressionParameter, first);
                var expectedExpression = VerifyCalls.Argument(call, VerifyCalls.ExpectedExpressionParameter, first + 1);
                return EquivalenceEmitter.Call(
                    EquivalenceBuilder.Build(target, _model.Compilation, options, _model),
                    kind,
                    Render(actual.Span),
                    Render(expected.Span),
                    actualExpression is null ? Literal(actual.ToString()) : Render(actualExpression.Span),
                    expectedExpression is null ? Literal(expected.ToString()) : Render(expectedExpression.Span),
                    PathPlaceholder(mapped.HasMappedPath ? mapped.Path : call.SyntaxTree.FilePath),
                    (mapped.StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture));
            }

            case CallKind.Single or CallKind.SingleWhere or CallKind.Is or CallKind.NotNull or CallKind.NotNullValue:
            {
                var single = kind is CallKind.Single or CallKind.SingleWhere;
                var subject = VerifyCalls.Argument(call, single ? VerifyCalls.ItemsParameter : VerifyCalls.ValueParameter)!;
                var expression = VerifyCalls.Argument(call, VerifyCalls.ExpressionParameter, kind == CallKind.SingleWhere ? 2 : 1);
                var typeArguments = call.Expression switch
                {
                    GenericNameSyntax generic => Render(generic.TypeArgumentList.Span),
                    MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => Render(generic.TypeArgumentList.Span),
                    _ => "",
                };
                var arguments = Render(subject.Span) + ", ";
                var expressions = expression is null ? Literal(subject.ToString()) : Render(expression.Span);
                if (kind == CallKind.SingleWhere)
                {
                    var predicate = VerifyCalls.Argument(call, VerifyCalls.PredicateParameter, 1)!;
                    var predicateExpression = VerifyCalls.Argument(call, VerifyCalls.PredicateExpressionParameter, 3);
                    arguments += Render(predicate.Span) + ", ";
                    expressions += ", " + (predicateExpression is null ? Literal(predicate.ToString()) : Render(predicateExpression.Span));
                }

                var name = single ? "Single" : kind == CallKind.Is ? "Is" : "NotNull";
                return $"{Runtime}.{name}{typeArguments}({arguments}{expressions}, {where})";
            }

            default:
            {
                var condition = VerifyCalls.Argument(call, VerifyCalls.ConditionParameter)!;
                var withMessage = kind == CallKind.VerifyWithMessage;
                var expression = VerifyCalls.Argument(call, VerifyCalls.ExpressionParameter, withMessage ? 2 : 1);
                var userMessage = withMessage ? VerifyCalls.Argument(call, VerifyCalls.MessageParameter, 1) : null;
                var shown = kind is CallKind.VerifyLambda or CallKind.CheckLambda && VerifyCalls.LambdaBody(condition) is { } body ? body : condition;
                var message = Shapes.Condition(shown, _model, _token);
                return $"{Runtime}.{(VerifyCalls.IsCheck(kind) ? "Check" : "Verify")}({Render(condition.Span)}, " +
                       (userMessage is null ? "" : Render(userMessage.Span) + ", ") +
                       $"{(expression is null ? Literal(shown.ToString()) : Render(expression.Span))}, {LiteralOrNull(message)}, {where})";
            }
        }
    }

    private static string LiteralOrNull(string? text) => text is null ? "null" : Literal(text);

    private string RenderForCondition(SyntaxNode node) => Render(node.Span);

    private string Render(TextSpan span)
    {
        var text = new StringBuilder();
        var position = span.Start;
        foreach (var edit in _edits)
        {
            // An insertion at the end of the span belongs to the node that encloses it.
            if (edit.Span.Start < position || edit.Span.Start < span.Start || edit.Span.End > span.End ||
                (edit.Span.Length == 0 && edit.Span.Start == span.End))
            {
                continue;
            }

            text.Append(_text.ToString(TextSpan.FromBounds(position, edit.Span.Start)));
            text.Append(edit.Text());
            position = edit.Span.End;
            if (edit.Span.Length == 0)
            {
                position = edit.Span.Start;
            }
        }

        return text.Append(_text.ToString(TextSpan.FromBounds(position, span.End))).ToString();
    }

    private sealed class Edit
    {
        public Edit(TextSpan span, Func<string> text)
        {
            Span = span;
            Text = text;
        }

        public TextSpan Span { get; }

        public Func<string> Text { get; }
    }
}
