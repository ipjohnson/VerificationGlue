using Microsoft.CodeAnalysis;

namespace VerificationGlue.Tests.AspNetCore.Generator;

internal static class Diagnostics
{
    private const string Category = "VerificationGlue.Tests.AspNetCore";

    private const string HelpLinkBase = "https://github.com/ipjohnson/VerificationGlue/blob/main/docs/diagnostics/";

    public static readonly DiagnosticDescriptor EntryPointNotFound = new(
        id: "VGA0001",
        title: "[WebAppEntry] names an assembly whose entry point the generator cannot find",
        messageFormat: "[WebAppEntry] names the assembly '{0}', but {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGA0001.md");

    public static readonly DiagnosticDescriptor InvalidEntryClass = new(
        id: "VGA0002",
        title: "[WebAppEntry] is on a class the generator cannot add to",
        messageFormat: "The class '{0}' has [WebAppEntry], but it {1}. The generator adds the entry type only to a partial class that is not generic and not nested.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGA0002.md");

    public static readonly DiagnosticDescriptor ConfigurationOnSharedApp = new(
        id: "VGA0003",
        title: "A configuration attribute on a test method cannot reach a shared app",
        messageFormat: "The test '{0}' has [{1}], but its app [WebApp<{2}>] is shared with Scope = {3} and starts before the test, so it does not read the test's attributes. Move [{1}] to {4}, or give the app test scope.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGA0003.md");
}
