using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Tests.Generator;

/// <summary>
/// One <c>[Row]</c> attribute: the row to register, or why it does not match the method (VGT0002).
/// </summary>
internal sealed record RowResult(AttributeData Attribute, RowModel? Row, string? Error);

/// <summary>
/// Reads the <c>[Row]</c> attributes of a test method. The generator and <see cref="TestAnalyzer"/>
/// share it, so the rows the build accepts are the rows that are registered.
/// </summary>
internal static class RowReader
{
    public static bool IsRow(AttributeData attribute) => attribute.AttributeClass?.ToDisplayString() == KnownTypes.RowAttribute;

    /// <summary>
    /// Each <c>[Row]</c> of <paramref name="method"/> in source order. A row's test ID is the method's ID
    /// followed by its values converted to their parameter types, so it does not depend on the row's
    /// position. The second and later copies of an identical row get <c>#2</c>, <c>#3</c> and so on.
    /// </summary>
    public static IReadOnlyList<RowResult> Read(IMethodSymbol method, Compilation compilation)
    {
        var results = new List<RowResult>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var methodUid = TestDiscovery.Uid(method);
        foreach (var attribute in method.GetAttributes())
        {
            if (!IsRow(attribute) || attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Kind != TypedConstantKind.Array)
            {
                continue;
            }

            var array = attribute.ConstructorArguments[0];
            IReadOnlyList<TypedConstant?> values = array.IsNull ? [null] : array.Values.Select(value => (TypedConstant?)value).ToArray();
            var error = Check(method, values, compilation, out var keys, out var arguments);
            if (error is not null)
            {
                results.Add(new RowResult(attribute, null, error));
                continue;
            }

            var key = "#row(" + string.Join(",", keys) + ")";
            var occurrence = occurrences.TryGetValue(key, out var count) ? count + 1 : 1;
            occurrences[key] = occurrence;
            var uid = methodUid + key + (occurrence > 1 ? "#" + occurrence.ToString(CultureInfo.InvariantCulture) : "");
            results.Add(new RowResult(attribute, new RowModel(uid, arguments.ToArray()), null));
        }

        return results;
    }

    private static string? Check(IMethodSymbol method, IReadOnlyList<TypedConstant?> values, Compilation compilation, out List<string> keys, out List<string> arguments)
    {
        keys = [];
        arguments = [];
        if (values.Count == 0)
        {
            return "has no values";
        }

        if (values.Count > method.Parameters.Length)
        {
            return $"has {values.Count} values, but the method has {method.Parameters.Length} parameter{(method.Parameters.Length == 1 ? "" : "s")}";
        }

        for (var i = 0; i < values.Count; i++)
        {
            var parameter = method.Parameters[i];
            var target = parameter.Type;
            var underlying = target is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : target;
            var value = values[i];
            if (value is null || value.Value.IsNull)
            {
                if (target.IsValueType && SymbolEqualityComparer.Default.Equals(underlying, target))
                {
                    return $"gives null for the parameter '{parameter.Name}' of type {Display(target)}";
                }

                keys.Add("null");
                arguments.Add("null");
                continue;
            }

            var constant = value.Value;
            if (constant.Kind == TypedConstantKind.Error || constant.Type is not { } source)
            {
                return $"has a value that is not a constant";
            }

            if (Numeric(constant, underlying) is { } converted)
            {
                keys.Add(TestDiscovery.TypeName(underlying) + ":" + Canonical(converted));
                arguments.Add("(object?)(" + Literal(converted, underlying.SpecialType) + ")");
                continue;
            }

            var conversion = ((CSharpCompilation)compilation).ClassifyConversion(source, target);
            if (!conversion.Exists || !conversion.IsImplicit || conversion.IsUserDefined)
            {
                return $"gives {Shown(constant)} ({Display(source)}) for the parameter '{parameter.Name}' of type {Display(target)}";
            }

            if (AttributeMaterializer.Write(constant, compilation) is not { } text)
            {
                return $"gives {Shown(constant)}, which the generated registration cannot write";
            }

            keys.Add(Key(constant));
            arguments.Add("(object?)(" + text + ")");
        }

        return null;
    }

    /// <summary>
    /// A number or character converted to the numeric <paramref name="target"/>, when C# converts the
    /// constant implicitly: a widening conversion, or an <c>int</c> that fits a smaller integer type.
    /// A <c>decimal</c> is returned as a <see cref="decimal"/>, an <c>nint</c> as a <see cref="long"/> and an
    /// <c>nuint</c> as a <see cref="ulong"/>. Null for anything else.
    /// </summary>
    private static object? Numeric(TypedConstant constant, ITypeSymbol target)
    {
        if (constant.Kind != TypedConstantKind.Primitive || constant.Type is not { } source || !IsNumeric(source.SpecialType))
        {
            return null;
        }

        var value = constant.Value!;
        if (target.SpecialType is SpecialType.System_Decimal or SpecialType.System_IntPtr or SpecialType.System_UIntPtr)
        {
            return Native(source.SpecialType, value, target.SpecialType);
        }

        if (!IsNumeric(target.SpecialType))
        {
            return null;
        }

        if (source.SpecialType == target.SpecialType)
        {
            return value;
        }

        if (source.SpecialType == SpecialType.System_Int32 && target.SpecialType is SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64)
        {
            var number = (int)value;
            var fits = target.SpecialType switch
            {
                SpecialType.System_SByte => number is >= sbyte.MinValue and <= sbyte.MaxValue,
                SpecialType.System_Byte => number is >= byte.MinValue and <= byte.MaxValue,
                SpecialType.System_Int16 => number is >= short.MinValue and <= short.MaxValue,
                SpecialType.System_UInt16 => number is >= ushort.MinValue and <= ushort.MaxValue,
                _ => number >= 0,
            };
            return fits ? Convert.ChangeType(value, ClrType(target.SpecialType), CultureInfo.InvariantCulture) : null;
        }

        if (target.SpecialType == SpecialType.System_Char)
        {
            return null;
        }

        var widening = source.SpecialType switch
        {
            SpecialType.System_SByte => target.SpecialType is SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single or SpecialType.System_Double,
            SpecialType.System_Byte or SpecialType.System_Char => target.SpecialType is not SpecialType.System_SByte and not SpecialType.System_Byte,
            SpecialType.System_Int16 => target.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single or SpecialType.System_Double,
            SpecialType.System_UInt16 => target.SpecialType is not SpecialType.System_SByte and not SpecialType.System_Byte and not SpecialType.System_Int16,
            SpecialType.System_Int32 => target.SpecialType is SpecialType.System_Int64 or SpecialType.System_Single or SpecialType.System_Double,
            SpecialType.System_UInt32 => target.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double,
            SpecialType.System_Int64 or SpecialType.System_UInt64 => target.SpecialType is SpecialType.System_Single or SpecialType.System_Double,
            SpecialType.System_Single => target.SpecialType == SpecialType.System_Double,
            _ => false,
        };

        return widening ? Convert.ChangeType(value, ClrType(target.SpecialType), CultureInfo.InvariantCulture) : null;
    }

    private static object? Native(SpecialType source, object value, SpecialType target)
    {
        if (source is SpecialType.System_Single or SpecialType.System_Double)
        {
            return null;
        }

        if (source == SpecialType.System_Char)
        {
            value = (int)(char)value;
        }

        return target switch
        {
            SpecialType.System_Decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            SpecialType.System_IntPtr when source is SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_Char =>
                Convert.ToInt64(value, CultureInfo.InvariantCulture),
            SpecialType.System_UIntPtr when source is SpecialType.System_Byte or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_Char || (source == SpecialType.System_Int32 && (int)value >= 0) =>
                Convert.ToUInt64(value, CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    private static string Literal(object value, SpecialType type) => type switch
    {
        SpecialType.System_Decimal => Canonical(value) + "m",
        SpecialType.System_IntPtr => "(nint)(" + Canonical(value) + ")",
        SpecialType.System_UIntPtr => "(nuint)" + Canonical(value),
        _ => AttributeMaterializer.Primitive(value, type)!,
    };

    private static bool IsNumeric(SpecialType type) => type is
        SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
        SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
        SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Char;

    private static Type ClrType(SpecialType type) => type switch
    {
        SpecialType.System_SByte => typeof(sbyte),
        SpecialType.System_Byte => typeof(byte),
        SpecialType.System_Int16 => typeof(short),
        SpecialType.System_UInt16 => typeof(ushort),
        SpecialType.System_Int32 => typeof(int),
        SpecialType.System_UInt32 => typeof(uint),
        SpecialType.System_Int64 => typeof(long),
        SpecialType.System_UInt64 => typeof(ulong),
        SpecialType.System_Single => typeof(float),
        SpecialType.System_Double => typeof(double),
        _ => typeof(char),
    };

    /// <summary>A value with its type, as a row's test ID writes it, such as <c>System.String:"a,b"</c>.</summary>
    private static string Key(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return "null";
        }

        var type = TestDiscovery.TypeName(constant.Type!);
        switch (constant.Kind)
        {
            case TypedConstantKind.Array:
                return type + ":[" + string.Join(",", constant.Values.Select(Key)) + "]";
            case TypedConstantKind.Type:
                return type + ":typeof(" + TestDiscovery.TypeName((ITypeSymbol)constant.Value!) + ")";
            default:
                return type + ":" + Canonical(constant.Value!);
        }
    }

    private static string Canonical(object value) => value switch
    {
        string text => SymbolDisplay.FormatLiteral(text, quote: true),
        char character => SymbolDisplay.FormatLiteral(character, quote: true),
        bool flag => flag ? "true" : "false",
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
    };

    private static string Shown(TypedConstant constant) =>
        constant.Kind == TypedConstantKind.Primitive && constant.Value is { } value ? Canonical(value) : constant.ToCSharpString();

    private static string Display(ITypeSymbol type) => type.ToDisplayString();
}
