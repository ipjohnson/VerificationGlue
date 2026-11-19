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
    Single,
    SingleWhere,
    Is,
    NotNull,
    NotNullValue,
    VerifyLambda,
    CheckLambda,
    VerifyEquivalent,
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
/// <param name="Lambda">For <c>Verify(() =&gt; ...)</c> and <c>Check(() =&gt; ...)</c>, the generated check of the lambda; otherwise null.</param>
/// <param name="Equivalence">For <c>VerifyEquivalent</c>, the comparer of its type; otherwise null.</param>
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
    bool InRunnerCopy,
    LambdaModel? Lambda = null,
    EquivalenceModel? Equivalence = null);

/// <summary>The comparer of one <c>VerifyEquivalent</c> call.</summary>
/// <param name="ParameterType">The type of the <c>actual</c> and <c>expected</c> parameters, with its nullable annotation, as the interceptor declares them.</param>
/// <param name="Root">
/// The <see cref="ComparedType.Key"/> of the type the call compares, or null when the values
/// compare with <c>Equals</c>, because the generated code cannot name the type or does not walk it.
/// </param>
/// <param name="Types">Every type the comparer walks, the root first.</param>
/// <param name="OptionsType">The type of the <c>options</c> parameter as the interceptor declares it, or null for the call without options.</param>
internal sealed record EquivalenceModel(string ParameterType, string? Root, EquatableArray<ComparedType> Types, string? OptionsType = null);

/// <summary>A type whose members, items or entries the generated comparer compares one by one.</summary>
/// <param name="Key">The name of its compare method, which is unique per type.</param>
/// <param name="Name">The fully qualified name of the type, or of the list, set or dictionary interface or array type that the compare method takes.</param>
/// <param name="IsReference">True for a class or a record class, which can be null.</param>
/// <param name="Items">The items of an array, a list or a set, or null for a type whose members are compared.</param>
/// <param name="Entries">The entries of a dictionary, or null for a type that is not one.</param>
internal sealed record ComparedType(string Key, string Name, bool IsReference, EquatableArray<ComparedMember> Members, ComparedItems? Items = null, ComparedEntries? Entries = null);

/// <summary>
/// The items of an array or a list, which the comparer compares in order, or without order when an
/// <c>Unordered</c> option names it, or the items of a set, which it always compares without order.
/// </summary>
/// <param name="Count">The member that gives the number of items: <c>Length</c> for an array, <c>Count</c> for a list or a set.</param>
/// <param name="Type">The <see cref="ComparedType.Key"/> of the item type, or null to compare with <c>Equals</c>.</param>
/// <param name="Nullable">True when the item type is a <c>Nullable&lt;T&gt;</c> of the walked type.</param>
/// <param name="Unordered">True when each expected item matches any one actual item.</param>
/// <param name="Set">True for a set, which has no indexer, and which looks up an item it does not walk with its own <c>Contains</c>.</param>
internal sealed record ComparedItems(string Count, string? Type, bool Nullable, bool Unordered = false, bool Set = false);

/// <summary>The entries of a dictionary, which the comparer matches by key with the dictionaries' own key comparers.</summary>
/// <param name="Type">The <see cref="ComparedType.Key"/> of the value type, or null to compare with <c>Equals</c>.</param>
/// <param name="Nullable">True when the value type is a <c>Nullable&lt;T&gt;</c> of the walked type.</param>
internal sealed record ComparedEntries(string? Type, bool Nullable);

/// <summary>A public field or property that the comparer reads.</summary>
/// <param name="Name">The name of the member, as the path shows it.</param>
/// <param name="Access">The name as C# writes it after a dot, with <c>@</c> for a keyword.</param>
/// <param name="Type">The <see cref="ComparedType.Key"/> of the member's type, or null to compare with <c>Equals</c>.</param>
/// <param name="Nullable">True when the member is a <c>Nullable&lt;T&gt;</c> of the walked type.</param>
internal sealed record ComparedMember(string Name, string Access, string? Type, bool Nullable);

/// <summary>
/// The generated check of one <c>Verify(() =&gt; ...)</c> or <c>Check(() =&gt; ...)</c> call. It goes in
/// the namespace of the call with the using directives of its file, so the copied body binds as it
/// does in the lambda.
/// </summary>
/// <param name="Expression">The source text of the lambda's body, which the message shows.</param>
/// <param name="Namespace">The namespace that contains the call, or null for the global namespace.</param>
/// <param name="FileUsings">The extern alias and using directives of the compilation unit.</param>
/// <param name="NamespaceUsings">The using directives of the namespaces around the call.</param>
/// <param name="Members">The members of the check: its closure readers, accessors and the method itself. Null when the call keeps its tier 2 form.</param>
/// <param name="Extensions">
/// Extension methods that reach a method the check cannot call, named like that method. Two calls that
/// reach the same method write the same text, so a namespace declares each one once.
/// </param>
internal sealed record LambdaModel(
    string Expression,
    string? Namespace,
    EquatableArray<string> FileUsings,
    EquatableArray<string> NamespaceUsings,
    string? Members,
    EquatableArray<string> Extensions);

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
