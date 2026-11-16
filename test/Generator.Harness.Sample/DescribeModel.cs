using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Generator.Harness.Sample;

/// <summary>The value the source output step reads. It holds no symbols, so it compares by value.</summary>
internal sealed class DescribeModel(string? ns, string name, EquatableArray<string> properties) : IEquatable<DescribeModel>
{
    public string? Namespace { get; } = ns;

    public string Name { get; } = name;

    public EquatableArray<string> Properties { get; } = properties;

    public string HintName => Namespace is null ? Name : $"{Namespace}.{Name}";

    public bool Equals(DescribeModel? other) =>
        other is not null && Namespace == other.Namespace && Name == other.Name && Properties.Equals(other.Properties);

    public override bool Equals(object? obj) => Equals(obj as DescribeModel);

    public override int GetHashCode() => (Namespace, Name, Properties).GetHashCode();
}

internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private ImmutableArray<T> Items => items.IsDefault ? ImmutableArray<T>.Empty : items;

    public bool Equals(EquatableArray<T> other) => Items.AsSpan().SequenceEqual(other.Items.AsSpan());

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in Items)
        {
            hash = (hash * 31) + item.GetHashCode();
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
