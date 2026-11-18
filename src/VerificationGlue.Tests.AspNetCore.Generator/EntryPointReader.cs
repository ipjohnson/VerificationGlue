using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Tests.AspNetCore.Generator;

/// <summary>
/// Reads the entry point of an app assembly from a compilation that holds only the app's reference,
/// with <see cref="MetadataImportOptions.All"/> so that private methods are visible. The compilation
/// has no core library, so the types of the signature are matched by name.
/// </summary>
internal static class EntryPointReader
{
    private const string NoEntryPoint = "it has no static method named Main, <Main> or <Main>$ with an entry point signature";

    /// <summary>The name of the assembly that <paramref name="reference"/> holds, or null when it cannot be read.</summary>
    public static string? AssemblyName(MetadataReference reference)
    {
        switch (reference)
        {
            case CompilationReference compilation:
                return compilation.Compilation.AssemblyName;
            case PortableExecutableReference executable:
                try
                {
                    return executable.GetMetadata() is AssemblyMetadata assembly && assembly.GetModules() is { Length: > 0 } modules
                        ? Name(modules[0].GetMetadataReader())
                        : null;
                }
                catch (Exception failure) when (failure is BadImageFormatException or System.IO.IOException or InvalidOperationException)
                {
                    return null;
                }

            default:
                return null;
        }

        static string? Name(MetadataReader reader) =>
            reader.IsAssembly ? reader.GetString(reader.GetAssemblyDefinition().Name) : null;
    }

    public static AppModel Read(string assemblyName, MetadataReference reference)
    {
        var compilation = CSharpCompilation.Create(
            "VerificationGlue.Tests.AspNetCore.EntryProbe",
            references: [reference],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
        if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
        {
            return new AppModel(assemblyName, null, "the generator could not read it");
        }

        var candidates = Types(assembly.GlobalNamespace)
            .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>())
            .Select(method => (Method: method, Return: ReturnOf(method)))
            .Where(candidate => candidate.Return is not null && IsEntryPoint(candidate.Method))
            .ToList();
        if (candidates.Count == 0)
        {
            return new AppModel(assemblyName, null, NoEntryPoint);
        }

        var types = candidates.Select(candidate => MetadataName(candidate.Method.ContainingType)).Distinct(StringComparer.Ordinal).ToList();
        if (types.Count > 1)
        {
            return new AppModel(
                assemblyName,
                null,
                "more than one type in it has an entry point: " + string.Join(", ", candidates.Select(candidate => Describe(candidate.Method, candidate.Return!.Value))));
        }

        // Main and <Main>$ are the methods the user wrote. <Main> is the method the compiler adds
        // for an async entry point, and the only one a reference assembly keeps for that shape.
        var entry = candidates.FirstOrDefault(candidate => candidate.Method.Name is "Main" or "<Main>$");
        if (entry.Method is null)
        {
            entry = candidates[0];
        }

        return new AppModel(
            assemblyName,
            new EntryPointModel(types[0], entry.Method.Name, entry.Return!.Value, entry.Method.Parameters.Length == 1),
            null);
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceOrTypeSymbol nested)
            {
                if (nested is INamedTypeSymbol type)
                {
                    yield return type;
                }

                foreach (var inner in Types(nested))
                {
                    yield return inner;
                }
            }
        }
    }

    private static bool IsEntryPoint(IMethodSymbol method) =>
        method.IsStatic
        && method.MethodKind == MethodKind.Ordinary
        && method.Name is "Main" or "<Main>" or "<Main>$"
        && !method.IsGenericMethod
        && (method.Parameters.Length == 0
            || (method.Parameters.Length == 1 && method.Parameters[0].Type is IArrayTypeSymbol { Rank: 1 } array && FullName(array.ElementType) == "System.String"));

    private static EntryReturn? ReturnOf(IMethodSymbol method) => method.ReturnType switch
    {
        var type when FullName(type) == "System.Void" => EntryReturn.Void,
        var type when FullName(type) == "System.Int32" => EntryReturn.Int,
        var type when FullName(type) == "System.Threading.Tasks.Task" => EntryReturn.Task,
        INamedTypeSymbol { TypeArguments.Length: 1 } type when FullName(type) == "System.Threading.Tasks.Task`1"
            && FullName(type.TypeArguments[0]) == "System.Int32" => EntryReturn.TaskOfInt,
        _ => null,
    };

    private static string FullName(ITypeSymbol type) =>
        type.ContainingNamespace is null || type.ContainingNamespace.IsGlobalNamespace
            ? type.MetadataName
            : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;

    /// <summary>The name <c>UnsafeAccessorType</c> takes: the namespace, and a <c>+</c> before each nested type.</summary>
    private static string MetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            name = outer.MetadataName + "+" + name;
        }

        return type.ContainingNamespace.IsGlobalNamespace ? name : type.ContainingNamespace.ToDisplayString() + "." + name;
    }

    private static string Describe(IMethodSymbol method, EntryReturn kind) =>
        $"{MetadataName(method.ContainingType)}.{method.Name}({(method.Parameters.Length == 0 ? "" : "string[]")}) returning {EntryEmitter.ReturnType(kind)}";
}
