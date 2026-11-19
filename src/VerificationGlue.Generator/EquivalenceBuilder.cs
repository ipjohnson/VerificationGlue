using System.Collections.Generic;
using System.Linq;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Generator;

/// <summary>
/// Finds the types and members that the comparer of a <c>VerifyEquivalent</c> call walks. A class,
/// a struct or a record walks its public instance fields and properties, its base types' included.
/// An array or a list compares its count and then its items in order. Everything else compares with
/// <c>Equals</c>: primitives, strings, enums, a type that implements <c>IEquatable&lt;T&gt;</c> and
/// is not a record, any other collection, such as a dictionary or a set, an interface, a type in a
/// <c>System</c> or <c>Microsoft</c> namespace, a type with no member to walk, and a type the
/// generated code cannot name.
/// </summary>
internal sealed class EquivalenceBuilder
{
    private static readonly SymbolDisplayFormat KeyFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    private readonly Compilation _compilation;
    private readonly List<ComparedType?> _types = new();
    private readonly Dictionary<string, int> _indexes = new();

    private EquivalenceBuilder(Compilation compilation) => _compilation = compilation;

    public static EquivalenceModel Build(IMethodSymbol method, Compilation compilation)
    {
        var builder = new EquivalenceBuilder(compilation);
        var root = builder.Visit(method.TypeArguments[0]);
        return new EquivalenceModel(
            method.Parameters[0].Type.ToDisplayString(TypeNames.Format),
            root,
            new EquatableArray<ComparedType>(builder._types.Where(type => type is not null).Select(type => type!).ToArray()));
    }

    /// <summary>The key of the comparer for <paramref name="type"/>, or null when it compares with <c>Equals</c>.</summary>
    private string? Visit(ITypeSymbol type)
    {
        if (Sequence(type) is { } sequence)
        {
            return VisitItems(sequence.Parameter, sequence.Item, sequence.Count);
        }

        if (type is not INamedTypeSymbol named || !Walks(named))
        {
            return null;
        }

        var name = named.ToDisplayString(KeyFormat);
        var key = Key(named.Name, name);
        if (_indexes.ContainsKey(key))
        {
            return key;
        }

        var index = _types.Count;
        _indexes.Add(key, index);
        _types.Add(null);

        var members = new List<ComparedMember>();
        var seen = new HashSet<string>();
        for (var current = named; current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.DeclaredAccessibility == Accessibility.Private || member.IsImplicitlyDeclared || !seen.Add(member.Name) || MemberType(member) is not { } memberType)
                {
                    continue;
                }

                var access = SyntaxFacts.GetKeywordKind(member.Name) == SyntaxKind.None ? member.Name : "@" + member.Name;
                var (memberKey, isNullable) = Compared(memberType);
                members.Add(new ComparedMember(member.Name, access, memberKey, isNullable));
            }
        }

        // A type with nothing to walk, such as one that keeps its state in private fields, compares
        // with Equals. Nothing refers to its key yet, because a reference back to it needs a member.
        if (members.Count == 0)
        {
            _indexes.Remove(key);
            _types.RemoveAt(index);
            return null;
        }

        _types[index] = new ComparedType(key, name, named.IsReferenceType, new EquatableArray<ComparedMember>(members.ToArray()));
        return key;
    }

    /// <summary>The comparer key of a member or item type, and whether it is a <c>Nullable&lt;T&gt;</c> of a walked struct.</summary>
    private (string? Key, bool Nullable) Compared(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable && Visit(nullable.TypeArguments[0]) is { } inner
            ? (inner, true)
            : (Visit(type), false);

    /// <summary>The key of the comparer that compares the items of an array or a list in order.</summary>
    private string VisitItems(ITypeSymbol parameter, ITypeSymbol item, string count)
    {
        var name = parameter.ToDisplayString(KeyFormat);
        var key = Key((item.Name.Length == 0 ? "Item" : item.Name) + (parameter is IArrayTypeSymbol ? "Array" : "List"), name);
        if (_indexes.ContainsKey(key))
        {
            return key;
        }

        var index = _types.Count;
        _indexes.Add(key, index);
        _types.Add(null);

        var (itemKey, isNullable) = Compared(item);
        _types[index] = new ComparedType(key, name, true, new EquatableArray<ComparedMember>([]), new ComparedItems(count, itemKey, isNullable));
        return key;
    }

    /// <summary>
    /// The parameter type and the item type of the comparer for a one-dimensional array, or for a
    /// class or interface that implements <c>IReadOnlyList&lt;T&gt;</c> or <c>IList&lt;T&gt;</c>, or
    /// null. A dictionary and a set compare with <c>Equals</c>, even one that is also a list.
    /// </summary>
    private (ITypeSymbol Parameter, ITypeSymbol Item, string Count)? Sequence(ITypeSymbol type)
    {
        if (!Nameable(type))
        {
            return null;
        }

        if (type is IArrayTypeSymbol { IsSZArray: true } array)
        {
            return (array, array.ElementType, "Length");
        }

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Interface } named ||
            named.SpecialType != SpecialType.None ||
            Interfaces(named).Any(face => face is { Name: "IDictionary" or "IReadOnlyDictionary" or "ISet" or "IReadOnlySet" } && InCollections(face)))
        {
            return null;
        }

        var list = List(named, SpecialType.System_Collections_Generic_IReadOnlyList_T) ?? List(named, SpecialType.System_Collections_Generic_IList_T);
        return list is null ? null : (list, list.TypeArguments[0], "Count");
    }

    /// <summary>The one list interface of <paramref name="special"/> that the type is or implements, or null.</summary>
    private static INamedTypeSymbol? List(INamedTypeSymbol type, SpecialType special)
    {
        var lists = Interfaces(type).Where(face => face.OriginalDefinition.SpecialType == special).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
        return lists.Count == 1 ? lists[0] : null;
    }

    private static IEnumerable<INamedTypeSymbol> Interfaces(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Interface ? type.AllInterfaces.Prepend(type) : type.AllInterfaces;

    private static bool InCollections(INamedTypeSymbol type) =>
        type.ContainingNamespace is { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } ||
        type.ContainingNamespace is { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };

    /// <summary>The type of a public instance field or property that the comparer can read, or null.</summary>
    private static ITypeSymbol? MemberType(ISymbol member)
    {
        var type = member switch
        {
            IPropertySymbol
            {
                IsStatic: false,
                IsIndexer: false,
                DeclaredAccessibility: Accessibility.Public,
                GetMethod.DeclaredAccessibility: Accessibility.Public,
                RefKind: RefKind.None,
            } property => property.Type,
            IFieldSymbol
            {
                IsStatic: false,
                IsConst: false,
                DeclaredAccessibility: Accessibility.Public,
                RefKind: RefKind.None,
                IsFixedSizeBuffer: false,
            } field => field.Type,
            _ => null,
        };

        if (type is null or IPointerTypeSymbol or IFunctionPointerTypeSymbol || type.IsRefLikeType || IsObsoleteError(member))
        {
            return null;
        }

        return type;
    }

    private static bool IsObsoleteError(ISymbol member) =>
        member.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { Name: "ObsoleteAttribute", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } &&
            attribute.ConstructorArguments.Length == 2 && attribute.ConstructorArguments[1].Value is true);

    private bool Walks(INamedTypeSymbol type)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) ||
            type.SpecialType != SpecialType.None ||
            type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
            type.IsTupleType ||
            type.IsRefLikeType ||
            IsFramework(type) ||
            !Nameable(type) ||
            type.AllInterfaces.Any(face => face.SpecialType == SpecialType.System_Collections_IEnumerable))
        {
            return false;
        }

        return type.IsRecord || !type.AllInterfaces.Any(face =>
            face is { Name: "IEquatable", Arity: 1, ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } &&
            SymbolEqualityComparer.Default.Equals(face.TypeArguments[0], type));
    }

    private static bool IsFramework(INamedTypeSymbol type)
    {
        var outer = type.ContainingNamespace;
        while (outer is { ContainingNamespace.IsGlobalNamespace: false })
        {
            outer = outer.ContainingNamespace;
        }

        return outer is { Name: "System" or "Microsoft" };
    }

    /// <summary>Whether code in the generated <c>Equivalence</c> class can name the type.</summary>
    private bool Nameable(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => Nameable(array.ElementType),
        INamedTypeSymbol named =>
            named.TypeKind != TypeKind.Error &&
            !named.IsAnonymousType &&
            !named.IsFileLocal &&
            _compilation.IsSymbolAccessibleWithin(named, _compilation.Assembly) &&
            named.TypeArguments.All(Nameable) &&
            (named.ContainingType is null || Nameable(named.ContainingType)),
        _ => false,
    };

    /// <summary>A method name part that is unique per type: the simple name and a hash of the full name.</summary>
    private static string Key(string simpleName, string fullName)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in fullName)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return $"{simpleName}_{hash:x8}";
        }
    }
}
