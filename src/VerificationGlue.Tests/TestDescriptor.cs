using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    /// <param name="returnTypeName">The full name of the return type.</param>
    /// <param name="filePath">The source file that declares the method.</param>
    /// <param name="line">The one-based line of the method name.</param>
    /// <param name="createClassAttributes">Creates the attributes of the class.</param>
    /// <param name="createMethodAttributes">Creates the attributes of the method.</param>
    /// <param name="constructorParameters">The parameters of the constructor that <paramref name="createInstance"/> calls.</param>
    /// <param name="parameters">The parameters of the test method.</param>
    /// <param name="createInstance">Creates the test class, or is null for a static test.</param>
    /// <param name="invoke">Runs the test on the instance.</param>
    public TestDescriptor(
        string uid,
        string displayName,
        Type testClass,
        string @namespace,
        string typeName,
        string methodName,
        string returnTypeName,
        string filePath,
        int line,
        Func<IReadOnlyList<Attribute>> createClassAttributes,
        Func<IReadOnlyList<Attribute>> createMethodAttributes,
        TestParameter[] constructorParameters,
        TestParameter[] parameters,
        Func<TestContext, object>? createInstance,
        Func<TestContext, object?, ValueTask> invoke)
    {
        Uid = uid;
        DisplayName = displayName;
        TestClass = testClass;
        Namespace = @namespace;
        TypeName = typeName;
        MethodName = methodName;
        ReturnTypeName = returnTypeName;
        FilePath = filePath;
        Line = line;
        CreateClassAttributes = createClassAttributes;
        CreateMethodAttributes = createMethodAttributes;
        ConstructorParameters = constructorParameters;
        Parameters = parameters;
        CreateInstance = createInstance;
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
    public IReadOnlyList<string> ParameterTypeNames => Parameters.Select(parameter => parameter.TypeName).ToArray();

    /// <summary>The full name of the return type.</summary>
    public string ReturnTypeName { get; }

    /// <summary>The source file that declares the method.</summary>
    public string FilePath { get; }

    /// <summary>The one-based line of the method name.</summary>
    public int Line { get; }

    /// <summary>Creates the attributes of the class. The runner calls it inside the class scope.</summary>
    public Func<IReadOnlyList<Attribute>> CreateClassAttributes { get; }

    /// <summary>Creates the attributes of the method. The runner calls it inside the test.</summary>
    public Func<IReadOnlyList<Attribute>> CreateMethodAttributes { get; }

    /// <summary>The parameters of the constructor that <see cref="CreateInstance"/> calls.</summary>
    public IReadOnlyList<TestParameter> ConstructorParameters { get; }

    /// <summary>The parameters of the test method.</summary>
    public IReadOnlyList<TestParameter> Parameters { get; }

    /// <summary>Creates the test class when the container does not supply it, or is null for a static test.</summary>
    public Func<TestContext, object>? CreateInstance { get; }

    /// <summary>Runs the test on the instance, which is null for a static test.</summary>
    public Func<TestContext, object?, ValueTask> Invoke { get; }

    /// <summary>The full name of <see cref="TestClass"/>, with <c>+</c> between nested types.</summary>
    internal string FullTypeName => Namespace.Length == 0 ? TypeName : Namespace + "." + TypeName;
}
