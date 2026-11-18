using CSharpAuthor.Collections;

namespace VerificationGlue.Generator;

internal enum CallKind
{
    Verify,
    VerifyWithMessage,
    Check,
    Throws,
    ThrowsAsync,
    ThrowsExactly,
    ThrowsExactlyAsync,
}

/// <summary>
/// One call to a <c>VerifySyntax</c> method.
/// </summary>
/// <param name="Line">The 1-based line of the call in its source file, used in the interceptor name.</param>
/// <param name="Column">The 1-based column of the call in its source file, used in the interceptor name.</param>
/// <param name="MappedPath">The file a <c>#line</c> directive maps the call to, or the source file.</param>
/// <param name="MappedLine">The 1-based line that the message reports.</param>
/// <param name="Message">The message for the shape of the call, or null when the shape has none.</param>
/// <param name="InRunnerCopy">
/// True when the call is in a method that tier 3 rewrites and whose attribute carries
/// <c>[VerifyRewrite]</c>. The runner always calls that method's copy, so with tier 3 enabled the
/// call gets no interceptor.
/// </param>
internal sealed record CallModel(
    string FilePath,
    int Line,
    int Column,
    string MappedPath,
    int MappedLine,
    int LocationVersion,
    string LocationData,
    CallKind Kind,
    string? Message,
    bool InRunnerCopy);

/// <summary>The calls of one source file. <see cref="ProjectDirectory"/> makes the reported paths relative.</summary>
internal sealed record FileCalls(string FilePath, string ProjectDirectory, EquatableArray<CallModel> Calls);

/// <summary>One type around a copied method, written as <c>partial</c>.</summary>
/// <param name="Keyword">The declaration keyword, such as <c>class</c> or <c>record struct</c>, with <c>ref</c> when the type has it.</param>
internal sealed record TypeShell(string Keyword, string Name);

/// <summary>A namespace around a copied method with the using directives declared in it.</summary>
/// <param name="Name">The namespace name, or null for the compilation unit.</param>
/// <param name="FileScoped">True for <c>namespace N;</c>.</param>
internal sealed record NamespaceShell(string? Name, bool FileScoped, EquatableArray<string> Usings);

/// <summary>
/// A tier 3 copy of one method. <see cref="Body"/> is finished C# with <c>#line</c> directives,
/// except that each path to show in a message is written between <see cref="CopyEmitter.PathMarker"/>
/// characters, because the path is made relative to the project directory when the file is written.
/// </summary>
internal sealed record MethodModel(
    string HintName,
    EquatableArray<NamespaceShell> Namespaces,
    EquatableArray<TypeShell> Types,
    string Signature,
    string Body);

internal enum RedirectKind
{
    Static,
    Instance,
    Extension,
}

internal sealed record Parameter(string Type, string Name, string RefKind);

/// <summary>A direct call from user source to a method that tier 3 rewrites, sent to its copy by an interceptor.</summary>
internal sealed record Redirect(
    string FilePath,
    int Line,
    int Column,
    int LocationVersion,
    string LocationData,
    RedirectKind Kind,
    string ContainingType,
    string Name,
    string? ReturnType,
    EquatableArray<Parameter> Parameters);

internal sealed record FileRedirects(string FilePath, EquatableArray<Redirect> Redirects);
