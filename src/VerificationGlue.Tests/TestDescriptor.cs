using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>
/// One registered test. The generator creates a descriptor for each <c>[VerifyTest]</c> method and
/// adds it to <see cref="TestRegistry"/> from a module initializer.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TestDescriptor
{
    /// <summary>Creates a descriptor. Generated code calls this.</summary>
    /// <param name="uid">The stable test ID: the containing type, the method name and the parameter types.</param>
    /// <param name="displayName">The name a test explorer shows.</param>
    /// <param name="testClass">The type that declares the test method.</param>
    /// <param name="namespace">The namespace of <paramref name="testClass"/>.</param>
    /// <param name="typeName">The type name without the namespace, with <c>+</c> between nested types.</param>
    /// <param name="methodName">The test method's name.</param>
    /// <param name="parameterTypeNames">The full names of the parameter types.</param>
    /// <param name="returnTypeName">The full name of the return type.</param>
    /// <param name="filePath">The source file that declares the method.</param>
    /// <param name="line">The one-based line of the method name.</param>
    /// <param name="createAttributes">Creates the attributes of the assembly, the class and the method, in that order.</param>
    /// <param name="invoke">Runs the test.</param>
    public TestDescriptor(
        string uid,
        string displayName,
        Type testClass,
        string @namespace,
        string typeName,
        string methodName,
        string[] parameterTypeNames,
        string returnTypeName,
        string filePath,
        int line,
        Func<IReadOnlyList<Attribute>> createAttributes,
        Func<TestContext, ValueTask> invoke)
    {
        Uid = uid;
        DisplayName = displayName;
        TestClass = testClass;
        Namespace = @namespace;
        TypeName = typeName;
        MethodName = methodName;
        ParameterTypeNames = parameterTypeNames;
        ReturnTypeName = returnTypeName;
        FilePath = filePath;
        Line = line;
        CreateAttributes = createAttributes;
        Invoke = invoke;
    }

    /// <summary>The stable test ID: the containing type, the method name and the parameter types.</summary>
    public string Uid { get; }

    /// <summary>The name a test explorer shows.</summary>
    public string DisplayName { get; }

    /// <summary>The type that declares the test method.</summary>
    public Type TestClass { get; }

    /// <summary>The namespace of <see cref="TestClass"/>.</summary>
    public string Namespace { get; }

    /// <summary>The type name without the namespace, with <c>+</c> between nested types.</summary>
    public string TypeName { get; }

    /// <summary>The test method's name.</summary>
    public string MethodName { get; }

    /// <summary>The full names of the parameter types.</summary>
    public IReadOnlyList<string> ParameterTypeNames { get; }

    /// <summary>The full name of the return type.</summary>
    public string ReturnTypeName { get; }

    /// <summary>The source file that declares the method.</summary>
    public string FilePath { get; }

    /// <summary>The one-based line of the method name.</summary>
    public int Line { get; }

    /// <summary>Creates the attributes of the assembly, the class and the method, in that order. The runner calls it inside the test.</summary>
    public Func<IReadOnlyList<Attribute>> CreateAttributes { get; }

    /// <summary>Runs the test.</summary>
    public Func<TestContext, ValueTask> Invoke { get; }

    /// <summary>The full name of <see cref="TestClass"/>, with <c>+</c> between nested types.</summary>
    internal string FullTypeName => Namespace.Length == 0 ? TypeName : Namespace + "." + TypeName;
}
