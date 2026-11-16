using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace Glue.Mocks.Generator
{
    /// <summary>An array with value equality, so a model holding one can be cached by the pipeline.</summary>
    public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> _items;

        public EquatableArray(ImmutableArray<T> items) => _items = items;

        public ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

        public int Length => Items.Length;

        public T this[int index] => Items[index];

        public bool Equals(EquatableArray<T> other) => Items.SequenceEqual(other.Items);

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            var hash = 17;

            foreach (var item in Items)
            {
                hash = unchecked(hash * 31 + item.GetHashCode());
            }

            return hash;
        }

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class EquatableArray
    {
        public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> items)
            where T : IEquatable<T> => new EquatableArray<T>(items.ToImmutableArray());
    }

    public enum ReturnKind
    {
        Void,
        Value,
        Task,
        TaskOfT,
        ValueTask,
        ValueTaskOfT,
    }

    public enum MemberKind
    {
        Method,
        Property,
        Indexer,
        Event,
    }

    public sealed record ParameterModel(string Name, string Type, int RefKind);

    public sealed record MemberModel(
        MemberKind Kind,
        string Name,
        string ContainingType,
        string ReturnType,
        ReturnKind ReturnKind,
        string? AwaitedType,
        EquatableArray<ParameterModel> Parameters,
        EquatableArray<string> TypeParameters,
        bool HasGetter,
        bool HasSetter,
        bool IsOverride,
        string? Unsupported
    );

    /// <summary>
    /// Everything the emitter needs for one mocked type, as strings and enums only. It holds no
    /// ISymbol, SyntaxNode or Location, so equality is by value and nothing roots a Compilation.
    /// </summary>
    public sealed record MockModel(
        string Type,
        string MockName,
        bool IsClass,
        EquatableArray<MemberModel> Members,
        string? Refusal
    );

    public sealed record InterceptModel(string Type, string MockName, int Version, string Data);

    public sealed record ArgInterceptModel(string Kind, int Position, int Version, string Data);

    public sealed record Discovery(MockModel? Mock, InterceptModel? Intercept);
}
