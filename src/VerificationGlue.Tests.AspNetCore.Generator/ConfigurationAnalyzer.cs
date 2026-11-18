using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Tests.AspNetCore.Generator;

/// <summary>
/// Reports a configuration attribute on a test method whose <c>[WebApp&lt;TEntry&gt;]</c> has class or
/// assembly scope (VGA0003). A shared app starts before its tests and does not read their attributes.
/// </summary>
/// <remarks>
/// The container is the narrowest <c>ContainerAttribute</c>, as the runner finds it. A <c>Scope</c> that a
/// derived attribute sets in its own constructor is not seen, as for VGT0008.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigurationAnalyzer : DiagnosticAnalyzer
{
    private const string VerifyTestAttribute = "VerificationGlue.Tests.VerifyTestAttribute";
    private const string ContainerAttribute = "VerificationGlue.Tests.ContainerAttribute";
    private const string WebAppAttribute = "VerificationGlue.Tests.AspNetCore.WebAppAttribute<TEntry>";

    private static readonly string[] ConfigurationAttributes =
    [
        "VerificationGlue.Tests.AspNetCore.AppSettingAttribute",
        "VerificationGlue.Tests.AspNetCore.AppEnvironmentAttribute",
        "VerificationGlue.Tests.AspNetCore.KestrelAttribute",
        "VerificationGlue.Tests.AspNetCore.WatchAppConfigurationAttribute",
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Diagnostics.ConfigurationOnSharedApp];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(VerifyTestAttribute) is not { } verifyTest ||
                start.Compilation.GetTypeByMetadataName(ContainerAttribute) is not { } container)
            {
                return;
            }

            var configuration = ConfigurationAttributes
                .Select(start.Compilation.GetTypeByMetadataName)
                .OfType<INamedTypeSymbol>()
                .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            if (configuration.IsEmpty)
            {
                return;
            }

            start.RegisterSymbolAction(symbol =>
            {
                var method = (IMethodSymbol)symbol.Symbol;
                var attributes = method.GetAttributes();
                if (!attributes.Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, verifyTest)))
                {
                    return;
                }

                var onMethod = attributes.Where(attribute => attribute.AttributeClass is { } type && configuration.Contains(type)).ToList();
                if (onMethod.Count == 0)
                {
                    return;
                }

                var app = FindContainer(attributes, container)
                    ?? FindContainer(method.ContainingType.GetAttributes(), container)
                    ?? FindContainer(symbol.Compilation.Assembly.GetAttributes(), container);
                if (app is null || WebApp(app.AttributeClass) is not { } webApp || Scope(app) is not { } scope)
                {
                    return;
                }

                foreach (var attribute in onMethod)
                {
                    var location = attribute.ApplicationSyntaxReference?.GetSyntax(symbol.CancellationToken).GetLocation()
                        ?? method.Locations.FirstOrDefault()
                        ?? Location.None;
                    var name = attribute.AttributeClass!.Name;
                    symbol.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.ConfigurationOnSharedApp,
                        location,
                        method.Name,
                        name.EndsWith("Attribute", System.StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : name,
                        webApp.TypeArguments[0].ToDisplayString(),
                        scope,
                        scope == "Class" ? "the class or the assembly" : "the assembly"));
                }
            }, SymbolKind.Method);
        });
    }

    private static AttributeData? FindContainer(IEnumerable<AttributeData> attributes, INamedTypeSymbol container) =>
        attributes.LastOrDefault(attribute => DerivesFrom(attribute.AttributeClass, container));

    private static bool DerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(type, baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The <c>WebApp&lt;TEntry&gt;</c> that <paramref name="attribute"/> is or derives from, or null.</summary>
    private static INamedTypeSymbol? WebApp(INamedTypeSymbol? attribute)
    {
        for (var type = attribute; type is not null; type = type.BaseType)
        {
            if (type is { IsGenericType: true } && type.ConstructedFrom.ToDisplayString() == WebAppAttribute)
            {
                return type;
            }
        }

        return null;
    }

    private static string? Scope(AttributeData container) =>
        container.NamedArguments.FirstOrDefault(pair => pair.Key == "Scope").Value.Value switch
        {
            1 => "Class",
            2 => "Assembly",
            _ => null,
        };
}
