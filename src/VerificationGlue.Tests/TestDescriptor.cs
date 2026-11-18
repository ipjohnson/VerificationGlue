using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>
/// One registered test. The generator creates a descriptor for each <c>[VerifyTest]</c> method, or one
/// for each of its <c>[Row]</c> attributes, and adds it to <see cref="TestRegistry"/> from a module initializer.
/// A method with <c>[RowSource]</c> or an <see cref="ITestDataSource"/> attribute gets one descriptor that
/// discovery replaces with one descriptor per row.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TestDescriptor
{
    private readonly Func<TestNameContext, string> _formatName;
    private string? _displayName;

    /// <summary>Creates a descriptor. Generated code calls this.</summary>
    /// <param name="uid">
    /// The stable test ID: the containing type, the method name and the parameter types, and for a row
    /// its typed values.
    /// </param>
    /// <param name="formatName">Calls the test's <see cref="ITestNameFormatter"/>.</param>
    /// <param name="arguments">The row values, or an empty array when the test has no rows.</param>
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
    /// <param name="scheduleGroup">The schedule group, from the narrowest <c>[ScheduleGroup]</c>.</param>
    /// <param name="maxParallel">The <c>MaxParallel</c> of that <c>[ScheduleGroup]</c>, where 0 means the processor count.</param>
    /// <param name="runAfter">The groups in the <c>[RunAfter]</c> attributes of the class.</param>
    /// <param name="resources">The <c>[Resource]</c> keys of the class and the method.</param>
    /// <param name="createDataSources">
    /// Creates the method's data sources, or is null when its rows are literal or it has none. Discovery
    /// replaces a descriptor that has them with one descriptor per row.
    /// </param>
    public TestDescriptor(
        string uid,
        Func<TestNameContext, string> formatName,
        object?[] arguments,
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
        Func<TestContext, object?, ValueTask> invoke,
        string scheduleGroup = ScheduleGroupAttribute.DefaultName,
        int maxParallel = 0,
        string[]? runAfter = null,
        string[]? resources = null,
        Func<TestDescriptor, IReadOnlyList<ITestDataSource>>? createDataSources = null)
    {
        CreateDataSources = createDataSources;
        ScheduleGroup = scheduleGroup;
        MaxParallel = maxParallel;
        RunAfter = runAfter ?? [];
        Resources = resources ?? [];
        Uid = uid;
        _formatName = formatName;
        Arguments = arguments;
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

    /// <summary>
    /// The stable test ID: the containing type, the method name and the parameter types, and for a row
    /// its typed values.
    /// </summary>
    public string Uid { get; }

    /// <summary>
    /// The name a test explorer shows, from the test's <see cref="ITestNameFormatter"/>. When the
    /// formatter throws, the name comes from <see cref="DefaultNameFormatter"/>.
    /// </summary>
    public string DisplayName => _displayName ??= FormatName();

    /// <summary>The row values, which go to the first parameters in order. Empty when the test has no rows.</summary>
    public IReadOnlyList<object?> Arguments { get; }

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

    /// <summary>The schedule group, from the narrowest <c>[ScheduleGroup]</c>.</summary>
    public string ScheduleGroup { get; }

    /// <summary>The <c>MaxParallel</c> of the narrowest <c>[ScheduleGroup]</c>, where 0 means the processor count.</summary>
    public int MaxParallel { get; }

    /// <summary>The groups in the <c>[RunAfter]</c> attributes of the class.</summary>
    public IReadOnlyList<string> RunAfter { get; }

    /// <summary>The <c>[Resource]</c> keys of the class and the method, widest first.</summary>
    public IReadOnlyList<string> Resources { get; }

    /// <summary>The full name of <see cref="TestClass"/>, with <c>+</c> between nested types.</summary>
    internal string FullTypeName => Namespace.Length == 0 ? TypeName : Namespace + "." + TypeName;

    /// <summary>Creates the method's data sources, or is null when it has none.</summary>
    internal Func<TestDescriptor, IReadOnlyList<ITestDataSource>>? CreateDataSources { get; }

    /// <summary>Why the method's data sources gave no rows to run. The test fails with it and runs nothing.</summary>
    internal Exception? SourceFailure { get; private init; }

    /// <summary>One row of this test, with its own ID and values.</summary>
    internal TestDescriptor WithRow(string uid, object?[] arguments) => Copy(uid, arguments, null);

    /// <summary>This test under its own ID, failing with <paramref name="failure"/> instead of running.</summary>
    internal TestDescriptor WithSourceFailure(Exception failure) => Copy(Uid, [], failure);

    private TestDescriptor Copy(string uid, object?[] arguments, Exception? failure) =>
        new(
            uid, _formatName, arguments, TestClass, Namespace, TypeName, MethodName, ReturnTypeName, FilePath, Line,
            CreateClassAttributes, CreateMethodAttributes, [.. ConstructorParameters], [.. Parameters], CreateInstance, Invoke,
            ScheduleGroup, MaxParallel, [.. RunAfter], [.. Resources])
        {
            SourceFailure = failure,
        };

    private string FormatName()
    {
        var context = new TestNameContext(FullTypeName, MethodName, Parameters, Arguments);
        try
        {
            return _formatName(context);
        }
        catch (Exception)
        {
            return DefaultNameFormatter.Format(context);
        }
    }
}
