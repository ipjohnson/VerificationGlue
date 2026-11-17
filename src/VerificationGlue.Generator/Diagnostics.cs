using Microsoft.CodeAnalysis;

namespace VerificationGlue.Generator;

internal static class Diagnostics
{
    private const string Category = "VerificationGlue";

    private const string HelpLinkBase = "https://github.com/ipjohnson/VerificationGlue/blob/main/docs/diagnostics/";

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
}
