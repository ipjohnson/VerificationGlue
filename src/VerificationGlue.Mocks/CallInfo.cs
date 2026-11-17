using System;
using System.Collections.Generic;

namespace VerificationGlue.Mocks;

/// <summary>
/// The arguments of one mock call, given to a <c>Returns</c> factory and to a <c>When</c> callback.
/// Setting an argument sets the value of a <c>ref</c> or <c>out</c> parameter.
/// </summary>
public sealed class CallInfo
{
    private readonly object?[] _arguments;

    internal CallInfo(object?[] arguments, Type[]? typeArguments)
    {
        _arguments = arguments;
        TypeArguments = typeArguments ?? Type.EmptyTypes;
    }

    /// <summary>The type arguments of a generic method call, or an empty list.</summary>
    public IReadOnlyList<Type> TypeArguments { get; }

    /// <summary>The number of arguments.</summary>
    public int Count => _arguments.Length;

    /// <summary>The argument at <paramref name="index"/>.</summary>
    public object? this[int index]
    {
        get => _arguments[index];
        set => _arguments[index] = value;
    }

    /// <summary>The argument at <paramref name="index"/> as a <typeparamref name="T"/>.</summary>
    public T ArgAt<T>(int index) => (T)_arguments[index]!;
}
