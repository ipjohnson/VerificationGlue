using CSharpAuthor;
using CSharpAuthor.Collections;

namespace VerificationGlue.Tests.Generator;

/// <summary>
/// An attribute to create for extension points: its type, its constructor arguments and its named
/// arguments, each already written as a C# expression.
/// </summary>
internal sealed record AttributeModel(ITypeDefinition Type, EquatableArray<string> Arguments, EquatableArray<NamedArgumentModel> NamedArguments);

internal sealed record NamedArgumentModel(string Name, string Value);

/// <summary>How the runner supplies one test parameter. A null <see cref="Expression"/> means no provider can.</summary>
internal sealed record ParameterModel(string Name, string TypeName, string? Expression);

internal enum ReturnKind
{
    Void,
    Awaitable,
}

/// <summary>One <c>[VerifyTest]</c> method that the runner can call.</summary>
internal sealed record TestMethodModel(
    string Name,
    string Uid,
    string FilePath,
    int Line,
    bool IsStatic,
    ReturnKind Return,
    string ReturnTypeName,
    EquatableArray<ParameterModel> Parameters,
    bool WillRewrite,
    EquatableArray<AttributeModel> Attributes);

/// <summary>The type that declares test methods, as the registration file names it.</summary>
internal sealed record TestClassModel(
    string MetadataName,
    ITypeDefinition Type,
    string Namespace,
    string TypeName,
    bool CanConstruct,
    EquatableArray<AttributeModel> Attributes);

/// <summary>A test method with its class, before the methods are grouped by class.</summary>
internal sealed record TestEntry(TestClassModel Class, TestMethodModel Method);

/// <summary>A class and its test methods, in source order. One registration file each.</summary>
internal sealed record TestClassFile(TestClassModel Class, EquatableArray<TestMethodModel> Methods);
