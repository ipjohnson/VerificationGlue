using CSharpAuthor.Collections;

namespace VerificationGlue.Generator;

internal enum CallKind
{
    Verify,
    VerifyWithMessage,
    Throws,
    ThrowsAsync,
}

/// <summary>
/// One call to a <c>VerifySyntax</c> method.
/// </summary>
/// <param name="Line">The 1-based line of the call in its source file, used in the interceptor name.</param>
/// <param name="Column">The 1-based column of the call in its source file, used in the interceptor name.</param>
/// <param name="MappedPath">The file a <c>#line</c> directive maps the call to, or the source file.</param>
/// <param name="MappedLine">The 1-based line that the message reports.</param>
/// <param name="Message">The message for the shape of the call, or null when the shape has none.</param>
internal sealed record CallModel(
    string FilePath,
    int Line,
    int Column,
    string MappedPath,
    int MappedLine,
    int LocationVersion,
    string LocationData,
    CallKind Kind,
    string? Message);

/// <summary>The calls of one source file. <see cref="ProjectDirectory"/> makes the reported paths relative.</summary>
internal sealed record FileCalls(string FilePath, string ProjectDirectory, EquatableArray<CallModel> Calls);
