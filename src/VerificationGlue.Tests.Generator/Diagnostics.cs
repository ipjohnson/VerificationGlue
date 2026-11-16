using Microsoft.CodeAnalysis;

namespace VerificationGlue.Tests.Generator;

internal static class Diagnostics
{
    private const string Category = "VerificationGlue.Tests";

    public static readonly DiagnosticDescriptor VersionMismatch = new(
        id: "VGT0007",
        title: "VerificationGlue version does not match VerificationGlue.Tests",
        messageFormat: "The project references VerificationGlue {0}, but VerificationGlue.Tests {1} requires VerificationGlue {1}. Reference the same version of both packages.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
