using Microsoft.CodeAnalysis;

namespace VerificationGlue.Mocks.Generator;

internal static class MockDiagnostics
{
    private const string Category = "VerificationGlue.Mocks";

    private const string HelpLinkBase = "https://github.com/ipjohnson/VerificationGlue/blob/main/docs/diagnostics/";

    public static readonly DiagnosticDescriptor TypeCannotBeMocked = new(
        id: "VGM0001",
        title: "Type cannot be mocked",
        messageFormat: "'{0}' cannot be mocked: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGM0001.md");

    public static readonly DiagnosticDescriptor MemberCannotBeMocked = new(
        id: "VGM0003",
        title: "Member cannot be mocked",
        messageFormat: "'{0}.{1}' cannot be mocked: {2}. The generated member throws NotSupportedException.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGM0003.md");

    public static DiagnosticDescriptor Find(string id) =>
        id == TypeCannotBeMocked.Id ? TypeCannotBeMocked : MemberCannotBeMocked;
}
