using System.Linq;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VerificationGlue.Shared;

namespace VerificationGlue.Tests.Generator;

/// <summary>
/// Registers each <c>[VerifyTest]</c> method with <c>TestRegistry</c>, one file per test class, and
/// reports VGT0007 when the referenced <c>VerificationGlue</c> has another version. The other
/// diagnostics come from <see cref="TestAnalyzer"/>, so no model here holds a location.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TestGenerator : IIncrementalGenerator
{
    public const string TestsStep = "Tests";
    public const string ClassesStep = "Classes";

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

        var rewrite = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => VerifyRewriteRules.IsEnabled(options.GlobalOptions));

        var tests = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownTypes.VerifyTestAttribute,
                static (node, _) => node is MethodDeclarationSyntax,
                static (syntax, _) => syntax.TargetSymbol is IMethodSymbol method && TestDiscovery.WhyNotRunnable(method) is null
                    ? TestDiscovery.Build(method, syntax.SemanticModel.Compilation)
                    : null)
            .Where(static entry => entry is not null)
            .Select(static (entry, _) => entry!)
            .WithTrackingName(TestsStep);

        var classes = tests.Collect()
            .SelectMany(static (entries, _) => entries
                .GroupBy(entry => entry.Class.MetadataName, System.StringComparer.Ordinal)
                .OrderBy(group => group.Key, System.StringComparer.Ordinal)
                .Select(group => new TestClassFile(group.First().Class, group.Select(entry => entry.Method).ToArray())))
            .WithTrackingName(ClassesStep);

        context.RegisterSourceOutput(classes.Combine(rewrite), static (output, pair) =>
            output.AddSource(TestEmitter.HintName(pair.Left.Class), TestEmitter.Emit(pair.Left, pair.Right)));

        var assemblyAttributes = context.CompilationProvider
            .Combine(tests.Collect().Select(static (entries, _) => entries.Any(entry => entry.ConstructsUnresolved)))
            .Select(static (pair, _) => AttributeMaterializer.Materialize(pair.Left.Assembly.GetAttributes(), pair.Left, pair.Right));

        context.RegisterSourceOutput(
            assemblyAttributes.Combine(tests.Collect().Select(static (entries, _) => entries.Length > 0)),
            static (output, pair) =>
            {
                if (pair.Right)
                {
                    output.AddSource(TestEmitter.AssemblyAttributesHintName, TestEmitter.EmitAssemblyAttributes(pair.Left));
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
