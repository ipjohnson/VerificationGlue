using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Tests.Generator;

/// <summary>One <c>[RowSource]</c> attribute: the source to register, or why its member cannot supply rows (VGT0005).</summary>
internal sealed record RowSourceResult(AttributeData Attribute, RowSourceModel? Source, string? Error);

/// <summary>
/// A <c>[RowSource]</c> member that matches the method. <see cref="Read"/> is the C# expression that reads
/// the member, <see cref="ElementType"/> the type of one row, and <see cref="Values"/> a lambda that turns
/// a row into the values of the method's first parameters, each converted to its parameter type.
/// </summary>
internal sealed record RowSourceModel(string MemberName, string Read, string ElementType, string Values, bool IsAsync, bool DeterministicReplay);

/// <summary>
/// Reads the <c>[RowSource]</c> attributes of a test method. The generator and <see cref="TestAnalyzer"/>
/// share it, so the sources the build accepts are the sources that are registered.
/// </summary>
internal static class RowSourceReader
{
    private const string Enumerable = "System.Collections.Generic.IEnumerable<T>";
    private const string AsyncEnumerable = "System.Collections.Generic.IAsyncEnumerable<T>";

    public static bool IsRowSource(AttributeData attribute) => attribute.AttributeClass?.ToDisplayString() == KnownTypes.RowSourceAttribute;

    /// <summary>True for an attribute whose class implements <c>ITestDataSource</c>.</summary>
    public static bool IsDataSource(AttributeData attribute) =>
        attribute.AttributeClass is { } type && type.AllInterfaces.Any(candidate => candidate.ToDisplayString() == KnownTypes.ITestDataSource);

    /// <summary>Each <c>[RowSource]</c> of <paramref name="method"/>, in source order.</summary>
    public static IReadOnlyList<RowSourceResult> Read(IMethodSymbol method, Compilation compilation, TypeNames names)
    {
        var results = new List<RowSourceResult>();
        foreach (var attribute in method.GetAttributes())
        {
            if (!IsRowSource(attribute))
            {
                continue;
            }

            var name = attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
            var deterministic = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "DeterministicReplay").Value.Value is true;
            var error = Check(method, name, deterministic, compilation, names, out var source);
            results.Add(new RowSourceResult(attribute, source, error));
        }

        return results;
    }

    private static string? Check(IMethodSymbol method, string? name, bool deterministic, Compilation compilation, TypeNames names, out RowSourceModel? source)
    {
        source = null;
        if (string.IsNullOrEmpty(name))
        {
            return "names no member";
        }

        var type = method.ContainingType;
        var members = new List<ISymbol>();
        for (var current = type; current is not null && members.Count == 0; current = current.BaseType)
        {
            members.AddRange(current.GetMembers(name!).Where(member => member is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol { IsIndexer: false } or IFieldSymbol));
        }

        if (members.Count == 0)
        {
            return $"names '{name}', which is not a method, property or field of {type.ToDisplayString()}";
        }

        var member = members.FirstOrDefault(candidate => candidate is not IMethodSymbol candidateMethod || (candidateMethod.Parameters.Length == 0 && !candidateMethod.IsGenericMethod));
        if (member is null)
        {
            return $"names the method '{name}', which has parameters or type parameters";
        }

        if (!member.IsStatic)
        {
            return $"names '{name}', which is not static";
        }

        if (member is IPropertySymbol { GetMethod: null })
        {
            return $"names the property '{name}', which has no getter";
        }

        if (!compilation.IsSymbolAccessibleWithin(member, compilation.Assembly) ||
            (member is IPropertySymbol { GetMethod: { } getter } && !compilation.IsSymbolAccessibleWithin(getter, compilation.Assembly)))
        {
            return $"names '{name}', which is private, so the generated registration cannot read it";
        }

        var returnType = member switch
        {
            IMethodSymbol candidate => candidate.ReturnType,
            IPropertySymbol candidate => candidate.Type,
            _ => ((IFieldSymbol)member).Type,
        };

        var isAsync = false;
        var element = ElementType(returnType, Enumerable);
        if (element is null)
        {
            element = ElementType(returnType, AsyncEnumerable);
            isAsync = element is not null;
        }

        if (element is null)
        {
            return $"names '{name}', which returns {returnType.ToDisplayString()}, not IEnumerable<T> or IAsyncEnumerable<T> of the row values";
        }

        var parameters = method.Parameters;
        var cSharp = (CSharpCompilation)compilation;
        IReadOnlyList<ITypeSymbol> values = element is INamedTypeSymbol { IsTupleType: true } tuple &&
            !(parameters.Length > 0 && cSharp.ClassifyConversion(element, parameters[0].Type) is { Exists: true, IsImplicit: true, IsUserDefined: false })
                ? tuple.TupleElements.Select(field => field.Type).ToArray()
                : [element];

        if (values.Count > parameters.Length)
        {
            return $"names '{name}', whose rows have {values.Count} values, but the method has {parameters.Length} parameter{(parameters.Length == 1 ? "" : "s")}";
        }

        var converted = new List<string>();
        for (var i = 0; i < values.Count; i++)
        {
            var conversion = cSharp.ClassifyConversion(values[i], parameters[i].Type);
            if (!conversion.Exists || !conversion.IsImplicit || conversion.IsUserDefined)
            {
                return $"names '{name}', whose rows give {values[i].ToDisplayString()} for the parameter '{parameters[i].Name}' of type {parameters[i].Type.ToDisplayString()}";
            }

            var item = values.Count == 1 && !ReferenceEquals(values[0], element) ? "row.Item1" : values.Count == 1 ? "row" : "row.Item" + (i + 1);
            converted.Add("(object?)(" + names.Write(parameters[i].Type) + ")" + item);
        }

        var read = names.Write(member.ContainingType) + "." + member.Name + (member is IMethodSymbol ? "()" : "");
        source = new RowSourceModel(
            name!,
            read,
            names.Write(element),
            "static row => new object?[] { " + string.Join(", ", converted) + " }",
            isAsync,
            deterministic);
        return null;
    }

    /// <summary>The <c>T</c> of <paramref name="type"/> when it is, or implements, <paramref name="generic"/>.</summary>
    private static ITypeSymbol? ElementType(ITypeSymbol type, string generic)
    {
        foreach (var candidate in new[] { type }.Concat(type.AllInterfaces))
        {
            if (candidate is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named && named.ConstructedFrom.ToDisplayString() == generic)
            {
                return named.TypeArguments[0];
            }
        }

        return null;
    }
}
