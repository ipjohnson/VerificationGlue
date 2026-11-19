using Microsoft.CodeAnalysis;

namespace VerificationGlue.Generator;

internal static class Diagnostics
{
    private const string Category = "VerificationGlue";

    private const string HelpLinkBase = "https://github.com/ipjohnson/VerificationGlue/blob/main/docs/diagnostics/";

    public static readonly DiagnosticDescriptor BecauseNotRewritten = new(
        id: "VG0001",
        title: "Because or WithMessage outside a method that tier 3 rewrites",
        messageFormat: "'.{0}' adds text only in a method that tier 3 rewrites. Here Verify throws before '.{0}' runs, so the text never reaches the message.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0001.md");

    public static readonly DiagnosticDescriptor ConstantCondition = new(
        id: "VG0002",
        title: "Verify condition is a constant",
        messageFormat: "The condition '{0}' is always {1}, so this Verify call checks nothing",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0002.md");

    public static readonly DiagnosticDescriptor SideEffect = new(
        id: "VG0003",
        title: "Verify condition has a side effect",
        messageFormat: "'{0}' changes state inside a Verify condition. Move it to a statement before the Verify call.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0003.md");

    public static readonly DiagnosticDescriptor FloatingPointEquality = new(
        id: "VG0004",
        title: "Verify condition compares floating-point values exactly",
        messageFormat: "'{0}' compares {1} values with '{2}'. Compare the difference with a tolerance instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0004.md");

    public static readonly DiagnosticDescriptor TypeNotPartial = new(
        id: "VG0005",
        title: "A [Verify] method is in a type that is not partial",
        messageFormat: "'{0}' cannot get a tier 3 copy because '{1}' is not partial. Add 'partial' to each type that contains the method.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0005.md");

    public static readonly DiagnosticDescriptor NotCaptured = new(
        id: "VG0006",
        title: "A part of a Verify condition cannot be captured",
        messageFormat: "'{0}' is not captured, because {1}. The failure message shows its text with the value unavailable.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0006.md");

    public static readonly DiagnosticDescriptor CheckWithoutScope = new(
        id: "VG0007",
        title: "Check where no VerifyScope can report it",
        messageFormat: "Check in {0} runs where no VerifyScope can be open, so a failure throws at once. Use Verify instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0007.md");

    public static readonly DiagnosticDescriptor FileTypeInFile = new(
        id: "VG0008",
        title: "A [Verify] method is not rewritten because its file declares a file type",
        messageFormat: "'{0}' is not rewritten, because its file declares the file type '{1}', which a copy in another file cannot see. It gets tier 2 messages.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0008.md");

    public static readonly DiagnosticDescriptor PreferLambda = new(
        id: "VG0009",
        title: "Write the condition as a lambda to list each part's value",
        messageFormat: "Write '{0}(() => {1})' so that a failure lists the value of each part",
        category: Category,
        // LambdaAnalyzer reports Info unless VerificationGlueLambdaSeverity says otherwise. A command-line
        // build skips an analyzer whose descriptors are all Info, which would drop warning and error too.
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0009.md");

    public static readonly DiagnosticDescriptor EquivalenceOption = new(
        id: "VG0010",
        title: "A VerifyEquivalent option does not name a member the comparer reads",
        messageFormat: "The VerifyEquivalent option '{0}' is not applied, because {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0010.md");

    public static readonly DiagnosticDescriptor EquivalenceUnwalked = new(
        id: "VG0011",
        title: "VerifyEquivalent compares a type it cannot walk with Equals",
        messageFormat: "VerifyEquivalent compares '{0}' {1}, because {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VG0011.md");
}
