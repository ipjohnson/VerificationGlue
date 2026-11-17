using CSharpAuthor;
using CSharpAuthor.Collections;

namespace ColdBuild.Generator;

/// <summary>
/// One <c>Verify</c> call. <see cref="InRunnerCopy"/> is true when the call sits directly in a method
/// that tier 3 rewrites and whose attribute carries <c>[VerifyRewrite]</c>.
/// </summary>
internal sealed record CallSite(
    string FilePath,
    int Line,
    int LocationVersion,
    string LocationData,
    string Expression,
    string Message,
    bool InRunnerCopy);

internal sealed record FileCalls(string FilePath, EquatableArray<CallSite> Calls);

internal sealed record TypeShell(string Keyword, string Name);

internal sealed record Parameter(ITypeDefinition Type, string Name, string RefKind);

internal sealed record GuardTerm(string Capture, bool Negated);

internal sealed record Capture(string Name, ITypeDefinition Type, string Label, EquatableArray<GuardTerm> Guard);

/// <summary>A rewritten <c>Verify</c> statement: the condition with each capture assigned in place.</summary>
internal sealed record VerifyRewrite(string Condition, string Expression, EquatableArray<Capture> Captures);

/// <summary>A top-level body statement. <see cref="Text"/> already calls copies where it calls a rewritten method.</summary>
internal sealed record Statement(int Line, string Text, VerifyRewrite? Verify);

internal sealed record MethodModel(
    string FilePath,
    int Line,
    EquatableArray<string> Usings,
    string? Namespace,
    EquatableArray<TypeShell> Types,
    string Name,
    bool IsStatic,
    bool IsAsync,
    ITypeDefinition? ReturnType,
    EquatableArray<Parameter> Parameters,
    EquatableArray<Statement> Statements);

internal sealed record Redirect(
    string FilePath,
    int LocationVersion,
    string LocationData,
    ITypeDefinition ContainingType,
    bool IsStatic,
    string Name,
    ITypeDefinition? ReturnType,
    EquatableArray<Parameter> Parameters);

internal sealed record FileRedirects(string FilePath, EquatableArray<Redirect> Redirects);
