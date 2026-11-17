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

    public static readonly DiagnosticDescriptor MatcherWithoutPosition = new(
        id: "VGM0002",
        title: "Arg matcher is not an argument of a mock call",
        messageFormat: "'{0}' is not passed directly as an argument of a mock member, so its argument position is unknown and it waits for the next mock call",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGM0002.md");

    // VGM0002 has two severities, and release tracking records one per ID. The release file lists the warning.
#pragma warning disable RS2001
    public static readonly DiagnosticDescriptor MatcherInTypedSurface = new(
        id: "VGM0002",
        title: "NSubstitute-style Arg matcher passed to a typed Setup() or Calls() member",
        messageFormat: "'{0}' is an NSubstitute-style matcher, so this Arg<T> parameter matches only the default value and the matcher waits for the next mock call. Use '{1}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGM0002.md");
#pragma warning restore RS2001

    public static readonly DiagnosticDescriptor MemberCannotBeMocked = new(
        id: "VGM0003",
        title: "Member cannot be mocked",
        messageFormat: "'{0}.{1}' cannot be mocked: {2}. The generated member throws NotSupportedException.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGM0003.md");

    public static readonly DiagnosticDescriptor TypeNotDiscovered = new(
        id: "VGM0004",
        title: "Mock.Of<T>() reaches a type with no generated mock",
        messageFormat: "'{0}' calls Mock.Of<{1}>() with {1} = '{2}', but no mock of '{2}' is generated. Add [assembly: GenerateMock(typeof({2}))].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGM0004.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor SetupWithoutMockCall = new(
        id: "VGM0005",
        title: "Setup is not on a mock call",
        messageFormat: "'{0}' is not a call to a mock member, so {1} would apply to whichever mock call came before it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGM0005.md");

    public static DiagnosticDescriptor Find(string id) =>
        id == TypeCannotBeMocked.Id ? TypeCannotBeMocked : MemberCannotBeMocked;
}
