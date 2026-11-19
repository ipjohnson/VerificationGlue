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
using Microsoft.CodeAnalysis.Text;

namespace VerificationGlue.Generator;

/// <summary>
/// Writes the check of one <c>Verify(() =&gt; ...)</c> or <c>Check(() =&gt; ...)</c> call. The check is a
/// static method that reads each variable the lambda captured from the delegate's target, then runs
/// the lambda's body once with each part assigned to a local, as tier 3 does. The delegate itself
/// does not run.
/// </summary>
/// <remarks>
/// The body is copied with these changes: <c>this</c> and the members it reaches without a receiver
/// go through <c>__this</c>; static members of the containing types are qualified; and a member
/// the check cannot reach, such as a private field, goes through an <c>[UnsafeAccessor]</c>. When
/// the body uses something the copy cannot express, such as a local function, a private nested
/// type or a write to a captured variable, the call keeps its tier 2 form.
/// </remarks>
internal sealed class LambdaBuilder
{
    private const string Runtime = "global::VerificationGlue.VerifyFailure";
    private const string Reader = "global::VerificationGlue.CapturedVariable";

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private readonly SemanticModel _model;
    private readonly ParenthesizedLambdaExpressionSyntax _lambda;
    private readonly ExpressionSyntax _body;
    private readonly INamedTypeSymbol? _containingType;
    private readonly ISymbol? _enclosing;
    private readonly string _site;
    private readonly CancellationToken _token;
    private readonly SourceText _text;
    private readonly List<Edit> _edits = new();
    private readonly HashSet<ExpressionSyntax> _whole = new();
    private readonly List<ISymbol> _captured = new();
    private readonly List<string> _accessors = new();
    private readonly List<string> _extensions = new();
    private readonly Dictionary<ISymbol, string> _accessorNames = new(SymbolEqualityComparer.Default);
    private bool _usesThis;

    private LambdaBuilder(SemanticModel model, ParenthesizedLambdaExpressionSyntax lambda, ExpressionSyntax body, string site, CancellationToken token)
    {
        _model = model;
        _lambda = lambda;
        _body = body;
        _site = site;
        _token = token;
        _text = lambda.SyntaxTree.GetText(token);
        _enclosing = model.GetEnclosingSymbol(lambda.SpanStart, token);
        _containingType = _enclosing?.ContainingType;
    }

    /// <summary>The model of the call, with no check when the argument is not a lambda the check can copy.</summary>
    public static LambdaModel Build(InvocationExpressionSyntax call, CallKind kind, SemanticModel model, string site, CancellationToken token)
    {
        var argument = VerifyCalls.Argument(call, VerifyCalls.ConditionParameter);
        var body = VerifyCalls.LambdaBody(argument);
        var expression = body?.ToString() ?? argument?.ToString() ?? "";

        var root = (CompilationUnitSyntax)call.SyntaxTree.GetRoot(token);
        var namespaceUsings = new List<string>();
        string? ns = null;
        foreach (var declaration in call.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse())
        {
            ns = ns is null ? declaration.Name.ToString() : ns + "." + declaration.Name;
            namespaceUsings.AddRange(CopyBuilder.Usings(declaration.Externs, declaration.Usings));
        }

        string? members = null;
        var extensions = Array.Empty<string>();
        if (body is not null && argument is ParenthesizedLambdaExpressionSyntax lambda && call.ArgumentList.Arguments.Count == 1)
        {
            try
            {
                var builder = new LambdaBuilder(model, lambda, body, site, token);
                members = builder.Members(kind, call);
                extensions = builder._extensions.ToArray();
            }
            catch (UnsupportedException)
            {
                members = null;
            }
        }

        return new LambdaModel(expression, ns, CopyBuilder.Usings(root.Externs, root.Usings), new EquatableArray<string>(namespaceUsings.ToArray()), members, new EquatableArray<string>(extensions));
    }

    /// <summary>The name of the check method of a call site.</summary>
    public static string MethodName(CallKind kind, string site) => (kind == CallKind.CheckLambda ? "Check_" : "Verify_") + site;

    private string Members(CallKind kind, InvocationExpressionSyntax call)
    {
        if (_lambda.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            throw new UnsupportedException();
        }

        // The copy assigns the captured values to its own locals, so a write would not reach the variable.
        var flow = _model.AnalyzeDataFlow(_body);
        if (flow is not { Succeeded: true } || flow.WrittenInside.Any(IsCaptured))
        {
            throw new UnsupportedException();
        }

        CollectEdits();
        var rewritten = new ConditionRewriter(_model, node => Render(node.Span), _whole.Contains, "__vg_", _token).Rewrite(_body);

        var path = call.SyntaxTree.FilePath;
        var mapped = call.GetLocation().GetMappedLineSpan();
        var linePath = mapped.HasMappedPath ? mapped.Path : path;
        var line = (mapped.StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture);
        var prefix = "__vg_" + _site + "_";
        var text = new StringBuilder();

        var thisType = _usesThis || _captured.Any(IsPrimaryConstructorParameter) ? Name(_containingType) : null;
        if (thisType is not null)
        {
            text.Append("    private static readonly ").Append(Reader).Append(' ').Append(prefix).Append("this = ").Append(Reader).Append(".This();\n");
        }

        var declarations = new StringBuilder();
        var reads = new StringBuilder();
        if (thisType is not null)
        {
            declarations.Append("        ").Append(thisType).Append(" __this;\n");
            reads.Append("            __this = ").Append(prefix).Append("this.Read<").Append(thisType).Append(">(__target);\n");
        }

        for (var index = 0; index < _captured.Count; index++)
        {
            var symbol = _captured[index];
            var local = Escape(symbol.Name);
            if (symbol is ILocalSymbol { IsConst: true } constant)
            {
                declarations.Append("        const ").Append(Name(constant.Type)).Append(' ').Append(local).Append(" = ").Append(Constant(constant.Type, constant.ConstantValue)).Append(";\n");
                continue;
            }

            var type = Name(symbol is ILocalSymbol variable ? variable.Type : ((IParameterSymbol)symbol).Type);
            var primary = IsPrimaryConstructorParameter(symbol);
            var reader = prefix + index.ToString(CultureInfo.InvariantCulture);
            text.Append("    private static readonly ").Append(Reader).Append(' ').Append(reader).Append(" = new(")
                .Append(CopyBuilder.Literal(primary ? "<" + symbol.Name + ">P" : symbol.Name)).Append(");\n");
            declarations.Append("        ").Append(type).Append(' ').Append(local).Append(";\n");
            reads.Append("            ").Append(local).Append(" = ").Append(reader).Append(".Read<").Append(type).Append(">(").Append(primary ? "__this" : "__target").Append(");\n");
        }

        foreach (var accessor in _accessors)
        {
            text.Append(accessor);
        }

        // The closure types are nested in the containing type. Keeping its members keeps their fields under trimming.
        if (Nameable(_containingType))
        {
            text.Append("    [global::System.Diagnostics.CodeAnalysis.DynamicDependency(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof(")
                .Append(_containingType!.ToDisplayString(TypeFormat)).Append("))]\n");
        }

        var check = kind == CallKind.CheckLambda;
        text.Append("    public static bool ").Append(MethodName(kind, _site)).Append("(object? __target, string __path)\n    {\n")
            .Append(declarations);
        if (reads.Length > 0)
        {
            text.Append("        try\n        {\n").Append(reads).Append("        }\n")
                .Append("        catch (global::System.Exception)\n        {\n            return false;\n        }\n\n");
        }

        text.Append("#line hidden\n");
        foreach (var local in rewritten.Locals)
        {
            text.Append("        ").Append(local.Declaration).Append('\n');
        }

        text.Append("#line ").Append(line).Append(' ').Append(CopyBuilder.Literal(linePath)).Append('\n')
            .Append("        if (!(").Append(rewritten.Text).Append("))\n")
            .Append("#line hidden\n        {\n            ").Append(check ? "" : "throw ")
            .Append(Runtime).Append(check ? ".StartCheck(" : ".Start(").Append(CopyBuilder.Literal(_body.ToString())).Append(", __path, ").Append(line).Append(')');
        foreach (var row in rewritten.Rows)
        {
            text.Append("\n                .").Append(row.Capture is null ? "NotCaptured(" : "Value(").Append(CopyBuilder.Literal(row.Label));
            if (row.Capture is not null)
            {
                text.Append(", ").Append(row.Capture);
            }

            if (row.Guard is not null)
            {
                text.Append(", evaluated: ").Append(row.Guard);
            }

            text.Append(')');
        }

        text.Append(check ? ".Report();\n" : ".Build();\n").Append("        }\n\n        return true;\n#line default\n    }\n");
        return text.ToString();
    }

    private void CollectEdits()
    {
        foreach (var node in _body.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case ThisExpressionSyntax:
                    _usesThis = true;
                    _edits.Add(new Edit(node.Span, () => "__this"));
                    break;
                case BaseExpressionSyntax:
                    throw new UnsupportedException();
                case ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax when _model.GetSymbolInfo(node, _token).Symbol is { } constructor && !Accessible(constructor):
                    throw new UnsupportedException();
                case SimpleNameSyntax name:
                    Name(name);
                    break;
            }

            if (node is ExpressionSyntax expression && _model.GetTypeInfo(expression, _token).Type is { } type && !Nameable(type) && !IsAnonymous(type))
            {
                throw new UnsupportedException();
            }
        }

        // A qualified name such as global::N.T in a hole would read its first ':' as the start of a format specifier.
        foreach (var hole in _body.DescendantNodes().OfType<InterpolationSyntax>())
        {
            var span = hole.Expression.Span;
            if (hole.Expression is not ParenthesizedExpressionSyntax && _edits.Any(edit => span.Contains(edit.Span)))
            {
                _edits.Add(new Edit(span, () => "(" + Render(span) + ")", wraps: true));
            }
        }

        _edits.Sort((left, right) => left.Span.Start != right.Span.Start
            ? left.Span.Start.CompareTo(right.Span.Start)
            : left.Span.Length != right.Span.Length
                ? right.Span.Length.CompareTo(left.Span.Length)
                : right.Wraps.CompareTo(left.Wraps));
    }

    /// <summary>The change for one simple name in the body, if it needs one.</summary>
    private void Name(SimpleNameSyntax name)
    {
        if (name.Parent is NameColonSyntax or NameEqualsSyntax || name.Parent is QualifiedNameSyntax qualified && qualified.Right == name ||
            name.Parent is AliasQualifiedNameSyntax || name.IsVar || IsInNameof(name))
        {
            return;
        }

        var info = _model.GetSymbolInfo(name, _token);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        if (symbol is null)
        {
            return;
        }

        // A member a property pattern or an object initializer names belongs to the object, not to this.
        if (name.FirstAncestorOrSelf<BaseExpressionColonSyntax>() is not null ||
            name.Parent is AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } assignment && assignment.Left == name)
        {
            if (!Accessible(symbol))
            {
                throw new UnsupportedException();
            }

            return;
        }

        var receiver = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : null;
        if (name.Parent is MemberBindingExpressionSyntax)
        {
            if (!Accessible(symbol))
            {
                throw new UnsupportedException();
            }

            return;
        }

        switch (symbol)
        {
            case ILocalSymbol or IParameterSymbol when receiver is null && IsCaptured(symbol):
                if (!_captured.Contains(symbol, SymbolEqualityComparer.Default))
                {
                    _captured.Add(symbol);
                }

                return;
            case IMethodSymbol { MethodKind: MethodKind.LocalFunction }:
                throw new UnsupportedException();
            case INamedTypeSymbol type:
                if (!Nameable(type))
                {
                    throw new UnsupportedException();
                }

                if (receiver is null && name.Parent is not QualifiedNameSyntax)
                {
                    _edits.Add(new Edit(name.Span, () => type.ToDisplayString(TypeFormat)));
                }

                return;
            case IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol when symbol.ContainingType is not null:
                Member(name, symbol, receiver);
                return;
        }
    }

    private void Member(SimpleNameSyntax name, ISymbol member, MemberAccessExpressionSyntax? access)
    {
        var invocation = (access is null ? name.Parent : access.Parent) is InvocationExpressionSyntax call && call.Expression == ((ExpressionSyntax?)access ?? name) ? call : null;
        if (Accessible(member))
        {
            if (access is null && member is not IMethodSymbol { MethodKind: MethodKind.ReducedExtension })
            {
                // A member reached without a receiver: an instance member of this, or a static member of a containing type.
                if (member.IsStatic)
                {
                    _edits.Add(new Edit(name.Span, () => Name(member.ContainingType) + "." + Render(name.Span)));
                }
                else
                {
                    _usesThis = true;
                    _edits.Add(new Edit(name.Span, () => "__this." + Render(name.Span)));
                }
            }

            return;
        }

        if (member is IFieldSymbol { IsConst: true } constant)
        {
            ExpressionSyntax target = (ExpressionSyntax?)access ?? name;
            _edits.Add(new Edit(target.Span, () => Constant(constant.Type, constant.ConstantValue)));
            _whole.Add(target);
            return;
        }

        if (invocation is not null && member is IMethodSymbol { IsStatic: false } instanceMethod)
        {
            // An extension method of the same name reaches it, so the call keeps its shape and its parts.
            Extension(instanceMethod);
            if (access is null)
            {
                _usesThis = true;
                _edits.Add(new Edit(name.Span, () => "__this." + Render(name.Span)));
            }

            return;
        }

        if (invocation is not null && invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null))
        {
            throw new UnsupportedException();
        }

        var accessor = Accessor(member, invocation is not null);
        var self = member.IsStatic ? "null" : access is null ? This() : null;
        if (invocation is not null)
        {
            _whole.Add(invocation);
            _edits.Add(new Edit(invocation.Span, () =>
            {
                var arguments = invocation.ArgumentList.Arguments.Select(argument => Render(argument.Expression.Span));
                return accessor + "(" + string.Join(", ", new[] { self ?? Render(access!.Expression.Span) }.Concat(arguments)) + ")";
            }));
            return;
        }

        if (member is IMethodSymbol)
        {
            // A method group of a member the check cannot reach.
            throw new UnsupportedException();
        }

        ExpressionSyntax whole = (ExpressionSyntax?)access ?? name;
        _whole.Add(whole);
        _edits.Add(new Edit(whole.Span, () => accessor + "(" + (self ?? Render(access!.Expression.Span)) + ")"));
    }

    private string This()
    {
        _usesThis = true;
        return "__this";
    }

    /// <summary>Declares an <c>[UnsafeAccessor]</c> for a field, property getter or method the check cannot reach.</summary>
    private string Accessor(ISymbol member, bool invoked)
    {
        if (_accessorNames.TryGetValue(member, out var existing))
        {
            return existing;
        }

        var owner = member.ContainingType;
        if (owner.IsValueType || owner.IsGenericType || !Nameable(owner))
        {
            throw new UnsupportedException();
        }

        var name = "__vg_" + _site + "_access" + _accessorNames.Count.ToString(CultureInfo.InvariantCulture);
        var self = Name(owner) + (member.IsStatic ? "? self" : " self");
        var kind = "global::System.Runtime.CompilerServices.UnsafeAccessorKind.";
        string declaration;
        switch (member)
        {
            case IFieldSymbol field when !invoked:
                declaration = Attribute(kind + (field.IsStatic ? "StaticField" : "Field"), field.Name) +
                    $"    private static extern ref {Name(field.Type)} {name}({self});\n";
                break;
            case IPropertySymbol { IsIndexer: false, GetMethod: { } getter } property when !invoked && property.RefKind == RefKind.None:
                declaration = Attribute(kind + (property.IsStatic ? "StaticMethod" : "Method"), getter.Name) +
                    $"    private static extern {Name(property.Type)} {name}({self});\n";
                break;
            case IMethodSymbol { IsGenericMethod: false, MethodKind: MethodKind.Ordinary, ReturnsVoid: false, RefKind: RefKind.None } method when invoked &&
                method.Parameters.All(parameter => parameter.RefKind == RefKind.None && !parameter.IsParams && !parameter.IsOptional):
                declaration = Attribute(kind + (method.IsStatic ? "StaticMethod" : "Method"), method.Name) +
                    $"    private static extern {Name(method.ReturnType)} {name}({self}" +
                    string.Concat(method.Parameters.Select(parameter => ", " + Name(parameter.Type) + " " + Escape(parameter.Name))) + ");\n";
                break;
            default:
                throw new UnsupportedException();
        }

        _accessorNames[member] = name;
        _accessors.Add(declaration);
        return name;
    }

    /// <summary>
    /// Declares an extension method named like <paramref name="method"/> that calls it through an
    /// <c>[UnsafeAccessor]</c>. Member lookup skips the inaccessible method, so a call on an instance
    /// binds to the extension.
    /// </summary>
    private void Extension(IMethodSymbol method)
    {
        var owner = method.ContainingType;
        if (owner.IsValueType || owner.IsGenericType || !Nameable(owner) || method.IsGenericMethod || method.ReturnsVoid ||
            method.RefKind != RefKind.None || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.IsParams || parameter.IsOptional))
        {
            throw new UnsupportedException();
        }

        var returns = Name(method.ReturnType);
        var parameters = string.Concat(method.Parameters.Select(parameter => ", " + Name(parameter.Type) + " " + Escape(parameter.Name)));
        var arguments = string.Concat(method.Parameters.Select(parameter => ", " + Escape(parameter.Name)));
        var accessor = "__vg_method_" + Hash(method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + parameters);
        var text = Attribute("global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method", method.Name) +
            $"    private static extern {returns} {accessor}({Name(owner)} self{parameters});\n" +
            $"    public static {returns} {Escape(method.Name)}(this {Name(owner)} self{parameters}) => {accessor}(self{arguments});\n";
        if (!_extensions.Contains(text))
        {
            _extensions.Add(text);
        }
    }

    private static string Hash(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in text)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }
    }

    private static string Attribute(string kind, string name) =>
        $"    [global::System.Runtime.CompilerServices.UnsafeAccessor({kind}, Name = {CopyBuilder.Literal(name)})]\n";

    /// <summary>Whether the symbol is a local or parameter declared outside the lambda, which the closure holds.</summary>
    private bool IsCaptured(ISymbol symbol) =>
        symbol is ILocalSymbol or IParameterSymbol &&
        !symbol.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree == _lambda.SyntaxTree && _lambda.Span.Contains(reference.Span));

    /// <summary>
    /// A primary constructor parameter used outside the constructor. The compiler keeps it in a
    /// field named <c>&lt;name&gt;P</c> of the instance.
    /// </summary>
    private bool IsPrimaryConstructorParameter(ISymbol symbol) =>
        symbol is IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } constructor } &&
        constructor.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(_token) is TypeDeclarationSyntax) &&
        !SymbolEqualityComparer.Default.Equals(constructor, _enclosing);

    private static bool IsInNameof(SyntaxNode node) =>
        node.Ancestors().Any(ancestor => ancestor is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } });

    private bool Accessible(ISymbol symbol) => _model.Compilation.IsSymbolAccessibleWithin(symbol, _model.Compilation.Assembly);

    private static bool IsAnonymous(ITypeSymbol type) => type is INamedTypeSymbol { IsAnonymousType: true };

    /// <summary>Whether code outside the containing type can write the type's name.</summary>
    private bool Nameable(ITypeSymbol? type) => type switch
    {
        null or IErrorTypeSymbol or ITypeParameterSymbol or IPointerTypeSymbol or IFunctionPointerTypeSymbol => false,
        IArrayTypeSymbol array => Nameable(array.ElementType),
        INamedTypeSymbol { IsAnonymousType: true } => false,
        INamedTypeSymbol named => Accessible(named) && named.TypeArguments.All(Nameable),
        _ => type.TypeKind != TypeKind.Dynamic,
    };

    private string Name(ITypeSymbol? type) => Nameable(type) ? type!.ToDisplayString(TypeFormat) : throw new UnsupportedException();

    private string Constant(ITypeSymbol type, object? value)
    {
        var literal = value is null ? "null" : SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false);
        if (value is float)
        {
            literal += "F";
        }
        else if (value is decimal)
        {
            literal += "M";
        }
        else if (value is long)
        {
            literal += "L";
        }
        else if (value is ulong)
        {
            literal += "UL";
        }
        else if (value is uint)
        {
            literal += "U";
        }

        return type.SpecialType is SpecialType.System_String or SpecialType.System_Boolean or SpecialType.System_Char
            ? literal
            : "((" + Name(type) + ")(" + literal + "))";
    }

    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None && SyntaxFacts.GetContextualKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    private string Render(TextSpan span)
    {
        var text = new StringBuilder();
        var position = span.Start;
        foreach (var edit in _edits)
        {
            if (edit.Span.Start < position || edit.Span.Start < span.Start || edit.Span.End > span.End || edit.Span == span && edit.Rendering)
            {
                continue;
            }

            text.Append(_text.ToString(TextSpan.FromBounds(position, edit.Span.Start)));
            edit.Rendering = true;
            text.Append(edit.Text());
            edit.Rendering = false;
            position = edit.Span.End;
        }

        return text.Append(_text.ToString(TextSpan.FromBounds(position, span.End))).ToString();
    }

    private sealed class Edit
    {
        public Edit(TextSpan span, Func<string> text, bool wraps = false)
        {
            Span = span;
            Text = text;
            Wraps = wraps;
        }

        public TextSpan Span { get; }

        public Func<string> Text { get; }

        /// <summary>True when the edit wraps the text of its span, so it renders before another edit of the same span.</summary>
        public bool Wraps { get; }

        /// <summary>True while the edit's own text renders its span, so the span renders without it.</summary>
        public bool Rendering { get; set; }
    }

    private sealed class UnsupportedException : Exception
    {
    }
}
