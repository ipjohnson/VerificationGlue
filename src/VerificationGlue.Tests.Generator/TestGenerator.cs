using Microsoft.CodeAnalysis;
using VerificationGlue.Shared;

namespace VerificationGlue.Tests.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class TestGenerator : IIncrementalGenerator
{
    private const string VerificationGlueAssembly = "VerificationGlue";
    private const string InformationalVersionAttribute = "System.Reflection.AssemblyInformationalVersionAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<string?> referencedVersion =
            context.CompilationProvider.Select(static (compilation, _) => ReferencedVerificationGlueVersion(compilation));

        context.RegisterSourceOutput(referencedVersion, static (output, version) =>
        {
            if (version is not null && version != GeneratorVersion.Current)
            {
                output.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.VersionMismatch, Location.None, version, GeneratorVersion.Current));
            }
        });
    }

    internal static string? ReferencedVerificationGlueVersion(Compilation compilation)
    {
        foreach (IAssemblySymbol assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (assembly.Identity.Name != VerificationGlueAssembly)
            {
                continue;
            }

            foreach (AttributeData attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == InformationalVersionAttribute &&
                    attribute.ConstructorArguments.Length == 1 &&
                    attribute.ConstructorArguments[0].Value is string informational)
                {
                    return GeneratorVersion.Normalize(informational);
                }
            }

            return assembly.Identity.Version.ToString();
        }

        return null;
    }
}
