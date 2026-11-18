using System;

namespace VerificationGlue.Tests;

/// <summary>
/// Tags tests with a name and a value, such as <c>[Trait("Category", "Slow")]</c>. A test has the traits
/// of its class and its method. <c>--vg-filter trait:Category=Slow</c> selects it, and the test node carries
/// each trait as a <c>TestMetadataProperty</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class TraitAttribute(string name, string value) : Attribute
{
    /// <summary>The trait's name.</summary>
    public string Name { get; } = name;

    /// <summary>The trait's value.</summary>
    public string Value { get; } = value;
}
