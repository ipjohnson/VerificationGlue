using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using CSharpAuthor;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;

namespace VerificationGlue.Mocks.Generator;

/// <summary>
/// Turns a type symbol into a CSharpAuthor type definition. It fixes three things CSharpAuthor's
/// own conversion gets wrong for a mock:
/// <list type="bullet">
/// <item>A type whose assembly is referenced only through an extern alias is written <c>alias::</c>, not <c>global::</c>.</item>
/// <item>A one-element tuple is written <c>ValueTuple&lt;T&gt;</c>. CSharpAuthor writes <c>(T, T)</c>.</item>
/// <item>An annotated value type, which is what <c>T?</c> becomes when T is closed over <c>int</c>, is written <c>int</c>, not <c>int?</c>.</item>
/// </list>
/// </summary>
internal sealed class TypeConversion
{
    private readonly Dictionary<IAssemblySymbol, string> _aliases = new(SymbolEqualityComparer.Default);
    private readonly Compilation _compilation;

    public TypeConversion(Compilation compilation)
    {
        _compilation = compilation;
        foreach (var reference in compilation.References)
        {
            var aliases = reference.Properties.Aliases;
            if (aliases.IsDefaultOrEmpty || aliases.Contains("global"))
            {
                continue;
            }

            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
            {
                _aliases[assembly] = aliases[0];
            }
        }
    }

    /// <summary>The aliases used by the types converted so far, in the order first used.</summary>
    public List<string> UsedAliases { get; } = new();

    public ITypeDefinition Convert(ITypeSymbol symbol) => ConvertNormalized(Normalize(symbol));

    private ITypeDefinition ConvertNormalized(ITypeSymbol symbol)
    {
        if (!NeedsOwnConversion(symbol))
        {
            return symbol.GetTypeDefinition();
        }

        switch (symbol)
        {
            case INamedTypeSymbol { IsTupleType: true, TupleElements.Length: 1 } single:
                return Annotate(
                    new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "System", "ValueTuple", new[] { Convert(single.TupleElements[0].Type) }),
                    symbol);

            case IArrayTypeSymbol array:
                var element = Convert(array.ElementType);
                var annotated = array.NullableAnnotation == NullableAnnotation.Annotated;
                return array.Rank == 1 && !annotated && !element.IsNullable && array.ElementType is not IArrayTypeSymbol
                    ? element.MakeArray()
                    : new ArrayTypeDefinition(element, array.Rank, annotated);

            case INamedTypeSymbol { IsTupleType: true } tuple:
                var elements = new List<TupleElementDefinition>();
                foreach (var field in tuple.TupleElements)
                {
                    elements.Add(new TupleElementDefinition(Convert(field.Type), field.IsImplicitlyDeclared ? null : field.Name));
                }

                return Annotate(new TupleTypeDefinition(elements), symbol);

            case INamedTypeSymbol named when named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T:
                return new NullableValueTypeDefinition(Convert(named.TypeArguments[0]));

            case INamedTypeSymbol named:
                ITypeDefinition inner;
                if (named.TypeArguments.Length > 0 && named.ContainingType is null)
                {
                    var arguments = new List<ITypeDefinition>();
                    foreach (var argument in named.TypeArguments)
                    {
                        arguments.Add(Convert(argument));
                    }

                    inner = Annotate(
                        new GenericTypeDefinition(Kind(named), named.ContainingNamespace.GetFullNamespace(), named.Name, arguments),
                        symbol);
                }
                else
                {
                    inner = named.GetTypeDefinition();
                }

                if (_aliases.TryGetValue(named.ContainingAssembly, out var alias))
                {
                    if (!UsedAliases.Contains(alias))
                    {
                        UsedAliases.Add(alias);
                    }

                    return new AliasedTypeDefinition(alias, inner);
                }

                return inner;

            default:
                return symbol.GetTypeDefinition();
        }
    }

    private bool NeedsOwnConversion(ITypeSymbol symbol)
    {
        switch (symbol)
        {
            case IArrayTypeSymbol array:
                return NeedsOwnConversion(array.ElementType);
            case IPointerTypeSymbol pointer:
                return NeedsOwnConversion(pointer.PointedAtType);
            case INamedTypeSymbol named:
                if (named is { IsTupleType: true, TupleElements.Length: 1 } ||
                    (named.ContainingAssembly is { } assembly && _aliases.ContainsKey(assembly)))
                {
                    return true;
                }

                foreach (var argument in named.TypeArguments)
                {
                    if (NeedsOwnConversion(argument))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// Drops the annotation from every value type that is not <c>Nullable&lt;T&gt;</c>. Roslyn keeps
    /// the annotation of <c>T?</c> when T is substituted with a value type, but the type it means is
    /// the value type itself.
    /// </summary>
    private ITypeSymbol Normalize(ITypeSymbol symbol)
    {
        var result = symbol;
        switch (symbol)
        {
            case IArrayTypeSymbol array:
                var element = Normalize(array.ElementType);
                if (!ReferenceEquals(element, array.ElementType))
                {
                    result = _compilation.CreateArrayTypeSymbol(element, array.Rank, array.NullableAnnotation);
                }

                break;

            case INamedTypeSymbol { IsTupleType: true } tuple:
                var elements = tuple.TupleElements.Select(e => Normalize(e.Type)).ToImmutableArray();
                if (elements.Where((type, i) => !ReferenceEquals(type, tuple.TupleElements[i].Type)).Any())
                {
                    result = _compilation.CreateTupleTypeSymbol(
                        elements,
                        tuple.TupleElements.Select(e => e.IsImplicitlyDeclared ? null : e.Name).ToImmutableArray(),
                        elementNullableAnnotations: elements.Select(e => e.NullableAnnotation).ToImmutableArray())
                        .WithNullableAnnotation(tuple.NullableAnnotation);
                }

                break;

            case INamedTypeSymbol { TypeArguments.Length: > 0, IsUnboundGenericType: false } named:
                var arguments = named.TypeArguments.Select(Normalize).ToImmutableArray();
                if (arguments.Where((type, i) => !ReferenceEquals(type, named.TypeArguments[i])).Any())
                {
                    result = named.ConstructedFrom
                        .Construct(arguments, arguments.Select(a => a.NullableAnnotation).ToImmutableArray())
                        .WithNullableAnnotation(named.NullableAnnotation);
                }

                break;
        }

        return result is { IsValueType: true, NullableAnnotation: NullableAnnotation.Annotated, TypeKind: not TypeKind.TypeParameter } &&
            result.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
            ? result.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            : result;
    }

    private static ITypeDefinition Annotate(ITypeDefinition type, ITypeSymbol symbol) =>
        symbol.NullableAnnotation == NullableAnnotation.Annotated ? type.MakeNullable() : type;

    private static TypeDefinitionEnum Kind(ITypeSymbol symbol) => symbol.TypeKind switch
    {
        TypeKind.Interface => TypeDefinitionEnum.InterfaceDefinition,
        TypeKind.Enum => TypeDefinitionEnum.EnumDefinition,
        _ => TypeDefinitionEnum.ClassDefinition,
    };
}

/// <summary>A type written as <c>alias::Namespace.Name</c>.</summary>
internal sealed class AliasedTypeDefinition : ITypeDefinition
{
    private readonly ITypeDefinition _inner;

    public AliasedTypeDefinition(string alias, ITypeDefinition inner)
    {
        Alias = alias;
        _inner = inner;
    }

    public string Alias { get; }

    public TypeDefinitionEnum TypeDefinitionEnum => _inner.TypeDefinitionEnum;

    public bool IsNullable => _inner.IsNullable;

    public bool IsArray => _inner.IsArray;

    public IReadOnlyList<bool> NullableAnnotations => _inner.NullableAnnotations;

    public IReadOnlyList<int> ArrayRanks => _inner.ArrayRanks;

    public string Name => _inner.Name;

    public string Namespace => _inner.Namespace;

    public ITypeDefinition? ContainingType => _inner.ContainingType;

    public IEnumerable<string> KnownNamespaces => _inner.KnownNamespaces;

    public IReadOnlyList<ITypeDefinition> TypeArguments => _inner.TypeArguments;

    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        var start = builder.Length;
        _inner.WriteTypeName(builder, TypeOutputMode.Global);
        const string Global = "global::";
        if (builder.Length - start >= Global.Length && builder.ToString(start, Global.Length) == Global)
        {
            builder.Remove(start, Global.Length).Insert(start, Alias + "::");
        }
    }

    public ITypeDefinition MakeNullable(bool nullable = true) => new AliasedTypeDefinition(Alias, _inner.MakeNullable(nullable));

    public ITypeDefinition MakeArray() => new AliasedTypeDefinition(Alias, _inner.MakeArray());

    public ITypeDefinition MakeArray(int rank) => new AliasedTypeDefinition(Alias, _inner.MakeArray(rank));

    public int CompareTo(ITypeDefinition? other) => string.CompareOrdinal(ToString(), other?.ToString());

    public override bool Equals(object? obj) => obj is AliasedTypeDefinition other && other.Alias == Alias && other._inner.Equals(_inner);

    public override int GetHashCode() => (Alias.GetHashCode() * 397) ^ _inner.GetHashCode();

    public override string ToString()
    {
        var builder = new StringBuilder();
        WriteTypeName(builder, TypeOutputMode.Global);
        return builder.ToString();
    }
}
