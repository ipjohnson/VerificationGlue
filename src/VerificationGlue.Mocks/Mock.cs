using System;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace VerificationGlue.Mocks;

/// <summary>Creates mocks.</summary>
public static class Mock
{
    /// <summary>
    /// Creates a mock of <typeparamref name="T"/>. The generator makes the mock for each
    /// <typeparamref name="T"/> named at a call site, in a <c>[Mock]</c> attribute or in
    /// <c>[assembly: GenerateMock(typeof(T))]</c>.
    /// </summary>
    public static T Of<T>()
        where T : class =>
        MockFactory<T>.Create is { } create
            ? create()
            : throw new InvalidOperationException(MockRegistry.Missing(typeof(T)));
}

/// <summary>The generated factory for mocks of <typeparamref name="T"/>, set by <see cref="MockRegistry.Register{T}"/>.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class MockFactory<T>
    where T : class
{
    /// <summary>Creates a mock, or is null when no mock of <typeparamref name="T"/> was generated.</summary>
    public static Func<T>? Create { get; internal set; }
}

/// <summary>
/// The generated mocks of this process, filled by a generated module initializer. It serves callers
/// that only have a <see cref="Type"/>, such as a test framework's parameter hook.
/// </summary>
public static class MockRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object>> s_factories = new();

    /// <summary>Registers the factory of a generated mock. Generated code calls this.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register<T>(Func<T> factory)
        where T : class
    {
        MockFactory<T>.Create = factory;
        s_factories[typeof(T)] = factory;
    }

    /// <summary>Creates a mock of <paramref name="type"/>.</summary>
    public static object Create(Type type) =>
        s_factories.TryGetValue(type, out var factory) ? factory() : throw new InvalidOperationException(Missing(type));

    internal static string Missing(Type type) =>
        $"No mock was generated for {type}. Name the type at a call site, such as Mock.Of<{type.Name}>(), " +
        $"or add [assembly: GenerateMock(typeof({type.Name}))] when the call is inside a generic helper.";
}

/// <summary>Marks a test parameter, property or field that receives a generated mock.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MockAttribute : Attribute;

/// <summary>Generates a mock of a type that no call site names, such as one a generic helper creates with <c>Mock.Of&lt;T&gt;()</c>.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GenerateMockAttribute(Type type) : Attribute
{
    /// <summary>The type to mock.</summary>
    public Type Type { get; } = type;
}
