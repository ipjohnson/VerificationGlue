using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpAuthor;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Mocks.Generator;

/// <summary>Builds the <see cref="MockModel"/> for one type.</summary>
internal sealed class MockModelBuilder
{
    private static readonly SymbolDisplayFormat MessageFormat = SymbolDisplayFormat.CSharpErrorMessageFormat;

    private static readonly string[] Locals = ["__args", "__result"];

    /// <summary>An indexer parameter must not be named like the setter's implicit parameter.</summary>
    private static readonly string[] AccessorLocals = ["value"];

    private readonly Compilation _compilation;
    private readonly TypeConversion _types;
    private readonly List<MockMemberModel> _members = new();
    private readonly List<DiagnosticModel> _diagnostics = new();
    private string _typeName = "";
    private int _nextId;
    private bool _needsUnsafe;
    private bool _obsolete;

    private MockModelBuilder(Compilation compilation)
    {
        _compilation = compilation;
        _types = new TypeConversion(compilation);
    }

    public static MockModel Build(INamedTypeSymbol type, Compilation compilation) =>
        new MockModelBuilder(compilation).Run(type);

    private MockModel Run(INamedTypeSymbol type)
    {
        if (type.IsUnboundGenericType)
        {
            type = type.OriginalDefinition;
        }

        _typeName = type.ToDisplayString(MessageFormat);
        var classTypeParameters = new List<ITypeParameterSymbol>();
        var isOpen = SymbolEqualityComparer.Default.Equals(type, type.OriginalDefinition);
        if (isOpen)
        {
            for (var container = type; container != null; container = container.ContainingType)
            {
                classTypeParameters.InsertRange(0, container.TypeParameters);
            }
        }

        var typeDefinition = _types.Convert(type);
        var mockName = MockName(type);

        var refusal = Refusal(type, isOpen);
        if (refusal == null)
        {
            foreach (var contract in new[] { type }.Concat(type.AllInterfaces))
            {
                refusal = AddMembers(contract);
                if (refusal != null)
                {
                    break;
                }
            }
        }

        if (refusal == null && _needsUnsafe && _compilation.Options is CSharpCompilationOptions { AllowUnsafe: false })
        {
            refusal = "it has members with pointer types, which need AllowUnsafeBlocks in the project";
        }

        if (refusal != null)
        {
            return new MockModel(
                _typeName,
                mockName,
                typeDefinition,
                default,
                default,
                default,
                new[] { new DiagnosticModel(MockDiagnostics.TypeCannotBeMocked.Id, new[] { _typeName, refusal }) });
        }

        var typeParameters = classTypeParameters.Select(ClassTypeParameter).ToArray();
        Track(type);
        foreach (var contract in type.AllInterfaces)
        {
            Track(contract);
        }

        return new MockModel(
            _typeName,
            mockName,
            typeDefinition,
            typeParameters,
            _types.UsedAliases.ToArray(),
            _members.ToArray(),
            _diagnostics.ToArray(),
            _obsolete);
    }

    /// <summary>Notes whether a type in the mock's signatures is obsolete.</summary>
    private void Track(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                Track(array.ElementType);
                break;
            case IPointerTypeSymbol pointer:
                Track(pointer.PointedAtType);
                break;
            case INamedTypeSymbol named:
                for (var container = named; container != null; container = container.ContainingType)
                {
                    _obsolete |= container.GetAttributes().Any(attribute => attribute.AttributeClass is
                    {
                        Name: "ObsoleteAttribute",
                        ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true },
                    });
                }

                foreach (var argument in named.TypeArguments)
                {
                    Track(argument);
                }

                break;
        }
    }

    private string? Refusal(INamedTypeSymbol type, bool isOpen)
    {
        if (type.TypeKind == TypeKind.Error)
        {
            return "the type could not be found";
        }

        if (type.TypeKind != TypeKind.Interface)
        {
            return type.TypeKind switch
            {
                TypeKind.Struct => "it is a struct",
                TypeKind.Class when type.IsSealed || type.IsStatic => "it is a sealed or static class",
                TypeKind.Class => "class mocks are not supported yet",
                _ => "only interfaces can be mocked",
            };
        }

        if (!_compilation.IsSymbolAccessibleWithin(type, _compilation.Assembly))
        {
            return "it is not accessible from this assembly";
        }

        if (!isOpen && ContainsTypeParameter(type))
        {
            return "its type arguments are type parameters that are not known at compile time";
        }

        return null;
    }

    private string? AddMembers(INamedTypeSymbol contract)
    {
        var contractDefinition = _types.Convert(contract);

        foreach (var member in contract.GetMembers())
        {
            if (member.IsStatic)
            {
                if (member.IsAbstract)
                {
                    return $"'{member.Name}' is a static abstract member";
                }

                continue;
            }

            if (!member.IsAbstract && !member.IsVirtual)
            {
                continue;
            }

            if (!_compilation.IsSymbolAccessibleWithin(member, _compilation.Assembly))
            {
                if (member.IsAbstract)
                {
                    return $"'{member.Name}' is not accessible from this assembly, so it cannot be implemented";
                }

                continue;
            }

            var refusal = member switch
            {
                IMethodSymbol { MethodKind: MethodKind.Ordinary } method => AddMethod(contractDefinition, method),
                IPropertySymbol { ExplicitInterfaceImplementations.Length: 0 } property => AddProperty(contractDefinition, property),
                IEventSymbol { ExplicitInterfaceImplementations.Length: 0 } @event => AddEvent(contractDefinition, @event),
                _ => null,
            };

            if (refusal != null)
            {
                return refusal;
            }
        }

        return null;
    }

    private string? AddMethod(ITypeDefinition contract, IMethodSymbol method)
    {
        if (CannotName(method.Name) is { } refusal)
        {
            return refusal;
        }

        if (method.IsVararg)
        {
            return $"'{method.Name}' takes __arglist";
        }

        if (MissingType(method.ReturnType, method.Parameters) is { } missing)
        {
            return missing;
        }

        var typeParameters = method.TypeParameters.Select(MethodTypeParameter).ToArray();
        var reserved = new HashSet<string>(method.TypeParameters.Select(t => t.Name).Concat(Locals));
        var parameters = Parameters(method.Parameters, reserved);
        var unsupported = method.ReturnsVoid ? null : ValueProblem(method.ReturnType, "it returns");
        unsupported ??= ParameterProblem(method.Parameters);

        var refReturn = method.ReturnsByRefReadonly ? RefReturn.RefReadonly : method.ReturnsByRef ? RefReturn.Ref : RefReturn.None;
        var (returnKind, awaited) = Classify(method.ReturnType, refReturn);
        var isUnsafe = HasPointer(method.ReturnType) || method.Parameters.Any(p => HasPointer(p.Type));

        Add(new MockMemberModel(
            MemberKind.Method,
            _nextId++,
            method.Name,
            contract,
            method.ReturnsVoid ? null : _types.Convert(method.ReturnType),
            returnKind,
            refReturn,
            awaited,
            parameters,
            typeParameters,
            false,
            false,
            false,
            isUnsafe,
            false,
            unsupported,
            "M:" + method.Name + "`" + method.Arity + "(" + ParameterKeys(method.Parameters) + ")",
            returnKind == ReturnKind.Value && unsupported == null ? MockReturn(method.ReturnType) : null));
        return null;
    }

    private string? AddProperty(ITypeDefinition contract, IPropertySymbol property)
    {
        if (!property.IsIndexer && CannotName(property.Name) is { } refusal)
        {
            return refusal;
        }

        if (MissingType(property.Type, property.Parameters) is { } missing)
        {
            return missing;
        }

        var refReturn = property.ReturnsByRefReadonly ? RefReturn.RefReadonly : property.ReturnsByRef ? RefReturn.Ref : RefReturn.None;
        var (returnKind, awaited) = Classify(property.Type, refReturn);
        var unsupported = ValueProblem(property.Type, "its type is");
        unsupported ??= ParameterProblem(property.Parameters);
        var isUnsafe = HasPointer(property.Type) || property.Parameters.Any(p => HasPointer(p.Type));

        Add(new MockMemberModel(
            property.IsIndexer ? MemberKind.Indexer : MemberKind.Property,
            _nextId,
            property.MetadataName,
            contract,
            _types.Convert(property.Type),
            returnKind,
            refReturn,
            awaited,
            Parameters(property.Parameters, new HashSet<string>(AccessorLocals)),
            default,
            property.GetMethod != null,
            property.SetMethod != null,
            property.SetMethod is { IsInitOnly: true },
            isUnsafe,
            false,
            unsupported,
            property.IsIndexer ? "I:" + ParameterKeys(property.Parameters) : "P:" + property.MetadataName,
            returnKind == ReturnKind.Value && unsupported == null && property.GetMethod != null ? MockReturn(property.Type) : null));
        _nextId += 2;
        return null;
    }

    private string? AddEvent(ITypeDefinition contract, IEventSymbol @event)
    {
        if (CannotName(@event.Name) is { } refusal)
        {
            return refusal;
        }

        if (@event.Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invoke } || @event.Type.TypeKind == TypeKind.Error)
        {
            return $"the type of event '{@event.Name}' could not be found";
        }

        Track(@event.Type);
        var canRaise = ParameterProblem(invoke.Parameters) == null;
        var parameters = invoke.Parameters
            .Select((p, i) => new ParameterModel("__a" + i, _types.Convert(p.Type), Modifier(p.RefKind), false, false))
            .ToArray();

        Add(new MockMemberModel(
            MemberKind.Event,
            _nextId,
            @event.Name,
            contract,
            _types.Convert(@event.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)),
            ReturnKind.Void,
            RefReturn.None,
            null,
            parameters,
            default,
            false,
            false,
            false,
            false,
            canRaise,
            null,
            "E:" + @event.Name));
        _nextId += 2;
        return null;
    }

    private void Add(MockMemberModel member)
    {
        _needsUnsafe |= member.IsUnsafe;
        _members.Add(member);
        if (member.Unsupported != null)
        {
            _diagnostics.Add(new DiagnosticModel(
                MockDiagnostics.MemberCannotBeMocked.Id,
                new[] { _typeName, member.Kind == MemberKind.Indexer ? "this[]" : member.Name, member.Unsupported }));
        }
    }

    private EquatableArray<ParameterModel> Parameters(IEnumerable<IParameterSymbol> parameters, HashSet<string> reserved)
    {
        var result = new List<ParameterModel>();
        var index = 0;
        foreach (var parameter in parameters)
        {
            var name = SyntaxFacts.IsValidIdentifier(parameter.Name) ? parameter.Name : "arg" + index;
            while (!reserved.Add(name))
            {
                name += "_";
            }

            result.Add(new ParameterModel(
                name,
                _types.Convert(parameter.Type),
                Modifier(parameter.RefKind),
                parameter.IsParams,
                parameter.ScopedKind != ScopedKind.None && parameter.RefKind != RefKind.Out && !parameter.IsParams));
            index++;
        }

        return result.ToArray();
    }

    private static ParameterModifier Modifier(RefKind refKind) => refKind switch
    {
        RefKind.Ref => ParameterModifier.Ref,
        RefKind.Out => ParameterModifier.Out,
        RefKind.In => ParameterModifier.In,
        RefKind.RefReadOnlyParameter => ParameterModifier.RefReadOnly,
        _ => ParameterModifier.None,
    };

    /// <summary>
    /// The constraints C# allows on an explicit implementation. Without one, <c>T?</c> in the
    /// signature means <c>Nullable&lt;T&gt;</c>, even when a base class constraint makes T a
    /// reference type. <c>class</c> and <c>default</c> make it mean the annotated T.
    /// </summary>
    private static TypeParameterModel MethodTypeParameter(ITypeParameterSymbol parameter)
    {
        var primary = parameter.IsReferenceType ? "class" : parameter.IsValueType ? "struct" : "default";
        return new TypeParameterModel(parameter.Name, primary, default, false);
    }

    /// <summary>
    /// The mock class repeats the interface's constraints but not <c>allows ref struct</c>, so the
    /// class can box its type arguments into the argument array.
    /// </summary>
    private TypeParameterModel ClassTypeParameter(ITypeParameterSymbol parameter)
    {
        string? primary = parameter.HasReferenceTypeConstraint
            ? parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class"
            : parameter.HasUnmanagedTypeConstraint ? "unmanaged"
            : parameter.HasValueTypeConstraint ? "struct"
            : parameter.HasNotNullConstraint ? "notnull"
            : null;
        foreach (var constraint in parameter.ConstraintTypes)
        {
            Track(constraint);
        }

        var types = parameter.ConstraintTypes.Select(_types.Convert).ToArray();
        return new TypeParameterModel(parameter.Name, primary, types, parameter.HasConstructorConstraint && !parameter.HasValueTypeConstraint);
    }

    private (ReturnKind, ITypeDefinition?) Classify(ITypeSymbol type, RefReturn refReturn)
    {
        if (type.SpecialType == SpecialType.System_Void)
        {
            return (ReturnKind.Void, null);
        }

        if (refReturn != RefReturn.None)
        {
            return (ReturnKind.Ref, null);
        }

        if (type is INamedTypeSymbol
            {
                ContainingNamespace: { Name: "Tasks", ContainingNamespace: { Name: "Threading", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } },
            } named)
        {
            switch (named.MetadataName)
            {
                case "Task":
                    return (ReturnKind.Task, null);
                case "Task`1":
                    return (ReturnKind.TaskOfT, _types.Convert(named.TypeArguments[0]));
                case "ValueTask":
                    return (ReturnKind.ValueTask, null);
                case "ValueTask`1":
                    return (ReturnKind.ValueTaskOfT, _types.Convert(named.TypeArguments[0]));
            }
        }

        return (ReturnKind.Value, null);
    }

    /// <summary>The return type as a recursive mock target: a closed, accessible interface outside the <c>System</c> namespaces.</summary>
    private MockReturnModel? MockReturn(ITypeSymbol type)
    {
        if (MockDiscovery.Closed(type) is not { TypeKind: TypeKind.Interface } named ||
            IsInSystem(named.ContainingNamespace) ||
            named.ContainingAssembly is not { } assembly ||
            !_compilation.IsSymbolAccessibleWithin(named, _compilation.Assembly) ||
            DocumentationCommentId.CreateReferenceId(named) is not { Length: > 0 } id)
        {
            return null;
        }

        return new MockReturnModel(named.ToDisplayString(MessageFormat), MockName(named), id, assembly.Identity.GetDisplayName());
    }

    private static bool IsInSystem(INamespaceSymbol? ns)
    {
        while (ns is { ContainingNamespace.IsGlobalNamespace: false })
        {
            ns = ns.ContainingNamespace;
        }

        return ns is { Name: "System" };
    }

    private string? ValueProblem(ITypeSymbol type, string what)
    {
        if (IsRefLike(type))
        {
            return $"{what} the ref struct '{type.ToDisplayString(MessageFormat)}'";
        }

        if (HasPointer(type))
        {
            return $"{what} the pointer type '{type.ToDisplayString(MessageFormat)}'";
        }

        return null;
    }

    private string? ParameterProblem(IEnumerable<IParameterSymbol> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (ValueProblem(parameter.Type, $"parameter '{parameter.Name}' is") is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    /// <summary>
    /// A ref struct cannot be boxed into the argument array. A type parameter that allows ref
    /// structs counts only when it belongs to a method, because the mock class drops the
    /// anti-constraint from its own type parameters.
    /// </summary>
    private static bool IsRefLike(ITypeSymbol type) =>
        type.IsRefLikeType ||
        type is ITypeParameterSymbol { AllowsRefLikeType: true, TypeParameterKind: TypeParameterKind.Method };

    private static bool HasPointer(ITypeSymbol type) => type switch
    {
        IPointerTypeSymbol or IFunctionPointerTypeSymbol => true,
        IArrayTypeSymbol array => HasPointer(array.ElementType),
        _ => false,
    };

    private string? MissingType(ITypeSymbol type, IEnumerable<IParameterSymbol> parameters)
    {
        foreach (var candidate in new[] { type }.Concat(parameters.Select(p => p.Type)))
        {
            Track(candidate);

            if (ContainsError(candidate))
            {
                return $"the type '{candidate.ToDisplayString(MessageFormat)}' could not be found";
            }
        }

        return null;
    }

    private static string? CannotName(string name) =>
        SyntaxFacts.IsValidIdentifier(name) ? null : $"the member name '{name}' cannot be written in C#";

    private static bool ContainsError(ITypeSymbol type) => type switch
    {
        { TypeKind: TypeKind.Error } => true,
        IArrayTypeSymbol array => ContainsError(array.ElementType),
        IPointerTypeSymbol pointer => ContainsError(pointer.PointedAtType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsError),
        _ => false,
    };

    private static bool ContainsTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
        IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsTypeParameter) ||
            (named.ContainingType != null && ContainsTypeParameter(named.ContainingType)),
        _ => false,
    };

    private static string ParameterKeys(IEnumerable<IParameterSymbol> parameters) =>
        string.Join(",", parameters.Select(parameter => TypeKey(parameter.Type)));

    /// <summary>
    /// The type as C# compares it in a signature: no nullable reference annotations, no tuple element
    /// names, <c>dynamic</c> as <c>object</c>, and a method type parameter by its position.
    /// </summary>
    private static string TypeKey(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method } parameter => "!!" + parameter.Ordinal,
        ITypeParameterSymbol parameter => "!" + parameter.Name,
        IArrayTypeSymbol array => TypeKey(array.ElementType) + "[" + array.Rank + "]",
        IPointerTypeSymbol pointer => TypeKey(pointer.PointedAtType) + "*",
        IDynamicTypeSymbol => "System.Object",
        INamedTypeSymbol named => NamedKey(named.TupleUnderlyingType ?? named),
        _ => type.ToDisplayString(),
    };

    private static string NamedKey(INamedTypeSymbol type)
    {
        var container = type.ContainingType != null
            ? NamedKey(type.ContainingType) + "+"
            : type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".";
        var arguments = type.TypeArguments.Length == 0 ? "" : "<" + string.Join(",", type.TypeArguments.Select(TypeKey)) + ">";
        return container + type.MetadataName + arguments;
    }

    private string MockName(INamedTypeSymbol type)
    {
        var builder = new StringBuilder();
        AppendName(builder, type);
        var name = new StringBuilder();
        foreach (var character in builder.ToString())
        {
            name.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        var alias = _types.Convert(type) is AliasedTypeDefinition aliased ? aliased.Alias + "_" : "";
        return alias + name + "_Mock";
    }

    private static void AppendName(StringBuilder builder, ITypeSymbol type)
    {
        switch (type)
        {
            case INamedTypeSymbol named:
                if (named.ContainingType != null)
                {
                    AppendName(builder, named.ContainingType);
                    builder.Append('_');
                }
                else if (!named.ContainingNamespace.IsGlobalNamespace)
                {
                    builder.Append(named.ContainingNamespace.ToDisplayString()).Append('_');
                }

                builder.Append(named.MetadataName);
                if (!SymbolEqualityComparer.Default.Equals(named, named.OriginalDefinition))
                {
                    foreach (var argument in named.TypeArguments)
                    {
                        builder.Append('_');
                        AppendName(builder, argument);
                    }
                }

                break;
            case IArrayTypeSymbol array:
                AppendName(builder, array.ElementType);
                builder.Append("Array");
                break;
            default:
                builder.Append(type.Name);
                break;
        }
    }
}
