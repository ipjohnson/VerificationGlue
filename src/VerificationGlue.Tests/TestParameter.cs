using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace VerificationGlue.Tests;

/// <summary>A parameter of a test method or of its class's constructor. The generator creates one per parameter.</summary>
public sealed class TestParameter
{
    private readonly Func<IReadOnlyList<Attribute>> _createAttributes;
    private IReadOnlyList<Attribute>? _attributes;

    /// <summary>Creates a parameter. Generated code calls this.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="type">The parameter type.</param>
    /// <param name="typeName">The parameter type written as the test ID writes it.</param>
    /// <param name="position">The zero-based position.</param>
    /// <param name="isConstructorParameter">True for a parameter of the test class's constructor.</param>
    /// <param name="createAttributes">Creates the parameter's attributes.</param>
    /// <param name="createMock">Calls <c>Mock.Of&lt;T&gt;()</c> for a <c>[Mock]</c> parameter, or is null.</param>
    /// <param name="serviceKey">The key from <c>[FromKeyedServices]</c>, or null.</param>
    /// <param name="hasDefaultValue">True when the parameter has a default value.</param>
    /// <param name="defaultValue">The default value.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TestParameter(
        string name,
        Type type,
        string typeName,
        int position,
        bool isConstructorParameter,
        Func<IReadOnlyList<Attribute>> createAttributes,
        Func<object>? createMock,
        object? serviceKey,
        bool hasDefaultValue,
        object? defaultValue)
    {
        Name = name;
        Type = type;
        TypeName = typeName;
        Position = position;
        IsConstructorParameter = isConstructorParameter;
        _createAttributes = createAttributes;
        CreateMock = createMock;
        ServiceKey = serviceKey;
        HasDefaultValue = hasDefaultValue;
        DefaultValue = defaultValue;
    }

    /// <summary>The parameter name.</summary>
    public string Name { get; }

    /// <summary>The parameter type.</summary>
    public Type Type { get; }

    /// <summary>The parameter type written as the test ID writes it, such as <c>System.Int32</c>.</summary>
    public string TypeName { get; }

    /// <summary>The zero-based position.</summary>
    public int Position { get; }

    /// <summary>True for a parameter of the test class's constructor.</summary>
    public bool IsConstructorParameter { get; }

    /// <summary>True for a <c>[Mock]</c> parameter.</summary>
    public bool IsMock => CreateMock is not null;

    /// <summary>The key from <c>[FromKeyedServices]</c>, or null.</summary>
    public object? ServiceKey { get; }

    /// <summary>True when the parameter has a default value.</summary>
    public bool HasDefaultValue { get; }

    /// <summary>The default value, when <see cref="HasDefaultValue"/> is true.</summary>
    public object? DefaultValue { get; }

    /// <summary>
    /// The parameter's attributes, except attributes from <c>System</c> namespaces. They are created
    /// on first use, inside the test.
    /// </summary>
    public IReadOnlyList<Attribute> Attributes => _attributes ??= _createAttributes();

    internal Func<object>? CreateMock { get; }
}
