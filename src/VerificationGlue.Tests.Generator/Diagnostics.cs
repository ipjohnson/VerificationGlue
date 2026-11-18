using Microsoft.CodeAnalysis;

namespace VerificationGlue.Tests.Generator;

internal static class Diagnostics
{
    private const string Category = "VerificationGlue.Tests";

    private const string HelpLinkBase = "https://github.com/ipjohnson/VerificationGlue/blob/main/docs/diagnostics/";

    public static readonly DiagnosticDescriptor DuplicateUid = new(
        id: "VGT0001",
        title: "Two tests have the same ID",
        messageFormat: "The test ID '{0}' is also the ID of another test. Each test ID must be unique.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGT0001.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor RowMismatch = new(
        id: "VGT0002",
        title: "A [Row] does not match the test method's parameters",
        messageFormat: "The [Row] on '{0}' {1}, so the row is not registered",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGT0002.md");

    public static readonly DiagnosticDescriptor NotRunnable = new(
        id: "VGT0004",
        title: "A [VerifyTest] method cannot run",
        messageFormat: "The [VerifyTest] method '{0}' {1}, so the runner does not register it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGT0004.md");

    public static readonly DiagnosticDescriptor AttributeNotMaterialized = new(
        id: "VGT0006",
        title: "An attribute is not passed to extension points",
        messageFormat: "The attribute '{0}' is not passed to extension points, because {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGT0006.md");

    public static readonly DiagnosticDescriptor SharedContainerWithoutChildOverrides = new(
        id: "VGT0008",
        title: "A test with [Mock] parameters uses a shared container that cannot take overrides",
        messageFormat: "The test '{0}' has [Mock] parameters, but its container {1} is shared with Scope = {2} and {1} does not implement ISupportsChildOverrides, so the system under test would not receive the mocks",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGT0008.md");

    public static readonly DiagnosticDescriptor VersionMismatch = new(
        id: "VGT0007",
        title: "VerificationGlue version does not match VerificationGlue.Tests",
        messageFormat: "The project references VerificationGlue {0}, but VerificationGlue.Tests {1} requires VerificationGlue {1}. Reference the same version of both packages.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGT0007.md");
}
