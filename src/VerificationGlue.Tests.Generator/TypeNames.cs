using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace VerificationGlue.Tests.Generator;

/// <summary>
/// Writes types for the generated registration as <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/>
/// does, except that a type whose assembly is referenced only through an extern alias is written
/// <c>alias::Namespace.Name</c>. <see cref="Aliases"/> holds the aliases written so far, which the
/// file must declare.
/// </summary>
internal sealed class TypeNames
{
    private readonly Compilation _compilation;
    private readonly SortedSet<string> _aliases = new(StringComparer.Ordinal);

    public TypeNames(Compilation compilation) => _compilation = compilation;

    public string[] Aliases => _aliases.ToArray();

    /// <summary>The type as C#. <c>dynamic</c> is written <c>object</c>, which <c>typeof</c> accepts.</summary>
    public string Write(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Dynamic)
        {
            return "object";
        }

        var parts = type.ToDisplayParts(SymbolDisplayFormat.FullyQualifiedFormat);
        var text = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Kind == SymbolDisplayPartKind.Keyword && parts[i].ToString() == "global" &&
                i + 1 < parts.Length && parts[i + 1].ToString() == "::" &&
                Alias(FirstType(parts, i + 2)) is { } alias)
            {
                _aliases.Add(alias);
                text.Append(alias);
                continue;
            }

            text.Append(parts[i].ToString());
        }

        return text.ToString();
    }

    /// <summary>The outermost type named after the namespace that starts at <paramref name="start"/>.</summary>
    private static INamedTypeSymbol? FirstType(ImmutableArray<SymbolDisplayPart> parts, int start)
    {
        for (var i = start; i < parts.Length; i++)
        {
            if (parts[i].Symbol is INamedTypeSymbol named)
            {
                return named;
            }

            if (parts[i].Kind is not (SymbolDisplayPartKind.NamespaceName or SymbolDisplayPartKind.Punctuation))
            {
                return null;
            }
        }

        return null;
    }

    private string? Alias(INamedTypeSymbol? type) =>
        type?.ContainingAssembly is { } assembly &&
        _compilation.GetMetadataReference(assembly) is { Properties.Aliases: { IsDefaultOrEmpty: false } aliases } &&
        !aliases.Contains("global")
            ? aliases[0]
            : null;
}
