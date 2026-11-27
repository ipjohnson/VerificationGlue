using Microsoft.CodeAnalysis;

namespace VerificationGlue.Tests.DependencyInjection.Generator;

internal static class Diagnostics
{
    private const string Category = "VerificationGlue.Tests.DependencyInjection";

    private const string HelpLinkBase = "https://github.com/ipjohnson/VerificationGlue/blob/main/docs/diagnostics/";

    public static readonly DiagnosticDescriptor NoConfigureMethod = new(
        id: "VGD0001",
        title: "The type in [ServiceCollection<T>] has no Configure method the generator can call and is not a DependencyModules module",
        messageFormat: "[ServiceCollection<{0}>] names '{0}', but {1}. The generator accepts a type with 'public static void Configure(IServiceCollection services)' or 'public static void Configure(IServiceCollection services, TestContainerContext context)', or a DependencyModules module with a public parameterless constructor.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGD0001.md");

    public static readonly DiagnosticDescriptor SourceNarrowerThanScope = new(
        id: "VGD0002",
        title: "A [ServiceCollection] attribute is on a narrower target than the scope that shares its container",
        messageFormat: "[{0}] is on {1} with Scope = {2}, but a container shared with Scope = {2} is built from the attributes of {3} before any test runs, so it does not read this attribute. Move it to {4}, or give it test scope.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "VGD0002.md");
}
