using System.Collections.Generic;
using System.Text;
using CSharpAuthor;

namespace VerificationGlue.Mocks.Generator;

/// <summary>
/// A type written after a keyword prefix, such as <c>unsafe int*</c>, <c>ref readonly T</c> or
/// <c>scoped ref Span&lt;int&gt;</c>. CSharpAuthor has no <c>unsafe</c> modifier, no ref returns
/// and no <c>scoped</c> parameters, and each of these keywords comes right before the type in an
/// explicit implementation.
/// </summary>
internal sealed class PrefixedTypeDefinition : ITypeDefinition
{
    private readonly ITypeDefinition _inner;

    public PrefixedTypeDefinition(string prefix, ITypeDefinition inner)
    {
        Prefix = prefix;
        _inner = inner;
    }

    public string Prefix { get; }

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
        builder.Append(Prefix);
        _inner.WriteTypeName(builder, typeOutputMode);
    }

    public ITypeDefinition MakeNullable(bool nullable = true) => new PrefixedTypeDefinition(Prefix, _inner.MakeNullable(nullable));

    public ITypeDefinition MakeArray() => new PrefixedTypeDefinition(Prefix, _inner.MakeArray());

    public ITypeDefinition MakeArray(int rank) => new PrefixedTypeDefinition(Prefix, _inner.MakeArray(rank));

    public int CompareTo(ITypeDefinition? other) => string.CompareOrdinal(ToString(), other?.ToString());

    public override bool Equals(object? obj) => obj is PrefixedTypeDefinition other && other.Prefix == Prefix && other._inner.Equals(_inner);

    public override int GetHashCode() => (Prefix.GetHashCode() * 397) ^ _inner.GetHashCode();

    public override string ToString()
    {
        var builder = new StringBuilder();
        WriteTypeName(builder, TypeOutputMode.Global);
        return builder.ToString();
    }
}

/// <summary>
/// Lines written as they are, at the current indent. Used for a property with a setter and no
/// getter, which <c>PropertyDefinition</c> cannot write: it always writes a getter.
/// </summary>
internal sealed class RawLines : IOutputComponent
{
    private readonly List<string> _lines = new();

    public void Add(string line) => _lines.Add(line);

    public void AddUsingNamespace(string ns)
    {
    }

    public void WriteOutput(IOutputContext outputContext)
    {
        outputContext.WriteLine();
        foreach (var line in _lines)
        {
            outputContext.WriteIndentedLine(line);
        }
    }
}
