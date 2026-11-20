using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using CSharpAuthor.Collections;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Tests.Generator;

/// <summary>
/// Writes each attribute of a test, its class and its assembly as a <c>new XAttribute(args)</c>
/// expression, so extension points find attributes without reflection. Attributes from <c>System</c>
/// namespaces are left out. An attribute the generated file cannot write is left out too, and
/// <see cref="TestAnalyzer"/> reports it as VGT0006 with the reason from <see cref="WhyNotMaterialized"/>.
/// </summary>
internal static class AttributeMaterializer
{
    public static EquatableArray<AttributeModel> Materialize(ImmutableArray<AttributeData> attributes, Compilation compilation)
    {
        var models = new List<AttributeModel>();
        foreach (var attribute in attributes)
        {
            if (!IsSkipped(attribute) && TryCreate(attribute, compilation, out var model, out _))
            {
                models.Add(model!);
            }
        }

        return models.ToArray();
    }

    /// <summary>True for an attribute from a <c>System</c> namespace, which is never passed to extension points.</summary>
    public static bool IsSkipped(AttributeData attribute)
    {
        var ns = attribute.AttributeClass?.ContainingNamespace;
        if (ns is null || ns.IsGlobalNamespace)
        {
            return false;
        }

        while (!ns.ContainingNamespace.IsGlobalNamespace)
        {
            ns = ns.ContainingNamespace;
        }

        return ns.Name == "System";
    }

    /// <summary>Why the generated file cannot create <paramref name="attribute"/>, or null when it can.</summary>
    public static string? WhyNotMaterialized(AttributeData attribute, Compilation compilation)
    {
        TryCreate(attribute, compilation, out _, out var reason);
        return reason;
    }

    private static bool TryCreate(AttributeData attribute, Compilation compilation, out AttributeModel? model, out string? reason)
    {
        model = null;
        if (attribute.AttributeClass is not { } type || type.TypeKind == TypeKind.Error)
        {
            reason = "its type cannot be resolved. Another source generator may produce it, and this generator cannot see that output";
            return false;
        }

        if (!IsAccessible(type, compilation))
        {
            reason = $"the generated registration cannot access {type.ToDisplayString()}";
            return false;
        }

        if (attribute.AttributeConstructor is not { } constructor || !compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly))
        {
            reason = "the generated registration cannot access its constructor";
            return false;
        }

        var arguments = new List<string>();
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (Write(argument, compilation) is not { } text)
            {
                reason = "an argument cannot be written as C#";
                return false;
            }

            arguments.Add(text);
        }

        var named = new List<NamedArgumentModel>();
        foreach (var pair in attribute.NamedArguments)
        {
            if (!CanSet(type, pair.Key, compilation))
            {
                reason = $"the generated registration cannot set {pair.Key}";
                return false;
            }

            if (Write(pair.Value, compilation) is not { } text)
            {
                reason = "an argument cannot be written as C#";
                return false;
            }

            named.Add(new NamedArgumentModel(pair.Key, text));
        }

        model = new AttributeModel(type.GetTypeDefinition(), arguments.ToArray(), named.ToArray());
        reason = null;
        return true;
    }

    private static bool IsAccessible(ITypeSymbol type, Compilation compilation)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsAccessible(array.ElementType, compilation);
            case INamedTypeSymbol named:
                for (INamedTypeSymbol? containing = named; containing is not null; containing = containing.ContainingType)
                {
                    if (containing.IsFileLocal || containing.TypeKind == TypeKind.Error)
                    {
                        return false;
                    }
                }

                return compilation.IsSymbolAccessibleWithin(named.IsUnboundGenericType ? named.OriginalDefinition : named, compilation.Assembly) &&
                    (named.IsUnboundGenericType || named.TypeArguments.All(argument => IsAccessible(argument, compilation)));
            default:
                return false;
        }
    }

    /// <summary>
    /// True when the generated file can name <paramref name="type"/>: it is accessible, and no part of it
    /// comes from an assembly referenced only through an extern alias, which <see cref="Name"/> would
    /// write with <c>global::</c>.
    /// </summary>
    private static bool CanName(ITypeSymbol type, Compilation compilation) =>
        IsAccessible(type, compilation) && !NamesAliasedType(type, compilation);

    private static bool NamesAliasedType(ITypeSymbol type, Compilation compilation)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return NamesAliasedType(array.ElementType, compilation);
            case INamedTypeSymbol named:
                if (named.ContainingAssembly is { } assembly &&
                    compilation.GetMetadataReference(assembly) is { Properties.Aliases: { IsDefaultOrEmpty: false } aliases } &&
                    !aliases.Contains("global"))
                {
                    return true;
                }

                return (named.ContainingType is { } outer && NamesAliasedType(outer, compilation)) ||
                    (!named.IsUnboundGenericType && named.TypeArguments.Any(argument => NamesAliasedType(argument, compilation)));
            default:
                return false;
        }
    }

    /// <summary>
    /// True when the initializer <c>{ name = ... }</c> in the generated file binds to a settable member.
    /// Like C# member lookup, it passes over members the file cannot access, such as a private member
    /// that hides a public one of a base type.
    /// </summary>
    private static bool CanSet(INamedTypeSymbol type, string name, Compilation compilation)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (!compilation.IsSymbolAccessibleWithin(member, compilation.Assembly))
                {
                    continue;
                }

                return member switch
                {
                    IPropertySymbol { SetMethod: { } setter } => compilation.IsSymbolAccessibleWithin(setter, compilation.Assembly),
                    IFieldSymbol { IsReadOnly: false, IsConst: false } => true,
                    _ => false,
                };
            }
        }

        return false;
    }

    /// <summary>
    /// The default value of <paramref name="parameter"/> as a C# expression of type <c>object?</c>,
    /// or null when it has none or it cannot be written.
    /// </summary>
    public static string? WriteDefault(IParameterSymbol parameter, TypeNames names)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            return null;
        }

        var type = names.Write(parameter.Type);
        var underlying = parameter.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : parameter.Type;
        var value = parameter.ExplicitDefaultValue;
        if (value is null)
        {
            return $"(object?)default({type})";
        }

        var literal = underlying.TypeKind == TypeKind.Enum
            ? $"({names.Write(underlying)})({Convert.ToString(value, CultureInfo.InvariantCulture)})"
            : value is decimal number
                ? number.ToString(CultureInfo.InvariantCulture) + "M"
                : Primitive(value, underlying.SpecialType);
        return literal is null ? null : $"(object?)({type})({literal})";
    }

    /// <summary>A typed constant written as a C# expression of the same type, or null when it cannot be.</summary>
    public static string? Write(TypedConstant constant, Compilation compilation)
    {
        if (constant.Kind == TypedConstantKind.Error || constant.Type is not { } type)
        {
            return null;
        }

        if (constant.IsNull)
        {
            return CanName(type, compilation) || type.SpecialType != SpecialType.None ? $"({Name(type)})null" : null;
        }

        switch (constant.Kind)
        {
            case TypedConstantKind.Array:
                var element = ((IArrayTypeSymbol)type).ElementType;
                if (element.SpecialType == SpecialType.None && !CanName(element, compilation))
                {
                    return null;
                }

                var items = new List<string>();
                foreach (var item in constant.Values)
                {
                    if (Write(item, compilation) is not { } text)
                    {
                        return null;
                    }

                    items.Add(text);
                }

                return $"new {Name(element)}[] {{ {string.Join(", ", items)} }}";

            case TypedConstantKind.Type:
                return constant.Value is ITypeSymbol target && CanName(target, compilation) ? $"typeof({TypeOfName(target)})" : null;

            case TypedConstantKind.Enum:
                return CanName(type, compilation) && constant.Value is { } value
                    ? $"({Name(type)})({Convert.ToString(value, CultureInfo.InvariantCulture)})"
                    : null;

            case TypedConstantKind.Primitive:
                return Primitive(constant.Value!, type.SpecialType);

            default:
                return null;
        }
    }

    /// <summary>A constant of a built-in type written as a C# expression of that type, or null for other types.</summary>
    public static string? Primitive(object value, SpecialType type) => type switch
    {
        SpecialType.System_Boolean => (bool)value ? "true" : "false",
        SpecialType.System_Char => SymbolDisplay.FormatLiteral((char)value, quote: true),
        SpecialType.System_String => SymbolDisplay.FormatLiteral((string)value, quote: true),
        SpecialType.System_SByte => $"(sbyte)({Invariant(value)})",
        SpecialType.System_Byte => $"(byte){Invariant(value)}",
        SpecialType.System_Int16 => $"(short)({Invariant(value)})",
        SpecialType.System_UInt16 => $"(ushort){Invariant(value)}",
        SpecialType.System_Int32 => Invariant(value),
        SpecialType.System_UInt32 => Invariant(value) + "U",
        SpecialType.System_Int64 => Invariant(value) + "L",
        SpecialType.System_UInt64 => Invariant(value) + "UL",
        SpecialType.System_Single => Floating((float)value, "float", "F"),
        SpecialType.System_Double => Floating((double)value, "double", "D"),
        _ => null,
    };

    private static string Floating(double value, string keyword, string suffix) =>
        double.IsNaN(value) ? keyword + ".NaN" :
        double.IsPositiveInfinity(value) ? keyword + ".PositiveInfinity" :
        double.IsNegativeInfinity(value) ? keyword + ".NegativeInfinity" :
        (suffix == "F" ? ((float)value).ToString("R", CultureInfo.InvariantCulture) : value.ToString("R", CultureInfo.InvariantCulture)) + suffix;

    private static string Invariant(object value) => Convert.ToString(value, CultureInfo.InvariantCulture)!;

    private static string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>The name inside <c>typeof</c>, which for an unbound generic type is written <c>List&lt;&gt;</c>.</summary>
    private static string TypeOfName(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { IsUnboundGenericType: true } unbound)
        {
            return Name(type);
        }

        var text = new StringBuilder();
        text.Append(unbound.ContainingType is { } outer ? TypeOfName(outer) + "." : unbound.ContainingNamespace.IsGlobalNamespace ? "global::" : "global::" + unbound.ContainingNamespace.ToDisplayString() + ".");
        return text.Append(unbound.Name).Append('<').Append(',', unbound.Arity - 1).Append('>').ToString();
    }
}
