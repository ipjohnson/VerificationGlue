using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Tests.DependencyInjection.Generator;

/// <summary>
/// Reports a <c>[ServiceCollection]</c> or <c>[ServiceCollection&lt;T&gt;]</c> whose <c>Scope</c> is wider than
/// its target (VGD0002): <c>Scope = ContainerScope.Class</c> on a method, or <c>ContainerScope.Assembly</c> on a
/// class or a method. The shared container is built from the attributes of its own scope, so it would not
/// read the attribute.
/// </summary>
/// <remarks>
/// The runner uses the narrowest container attribute, so a source on a narrower target than a shared
/// scope is always this attribute itself.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedScopeAnalyzer : DiagnosticAnalyzer
{
    private const string Plain = "VerificationGlue.Tests.DependencyInjection.ServiceCollectionAttribute";
    private const string Generic = "VerificationGlue.Tests.DependencyInjection.ServiceCollectionAttribute`1";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Diagnostics.SourceNarrowerThanScope];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var plain = start.Compilation.GetTypeByMetadataName(Plain);
            var generic = start.Compilation.GetTypeByMetadataName(Generic);
            if (plain is null && generic is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbol =>
            {
                var onMethod = symbol.Symbol is IMethodSymbol;
                foreach (var attribute in symbol.Symbol.GetAttributes())
                {
                    if (attribute.AttributeClass is not { } type ||
                        !(SymbolEqualityComparer.Default.Equals(type, plain) || SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, generic)))
                    {
                        continue;
                    }

                    var scope = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Scope").Value.Value;
                    if (scope is not (1 or 2) || (scope is 1 && !onMethod))
                    {
                        continue;
                    }

                    var name = type.IsGenericType
                        ? "ServiceCollection<" + type.TypeArguments[0].ToDisplayString() + ">"
                        : "ServiceCollection";
                    var location = attribute.ApplicationSyntaxReference?.GetSyntax(symbol.CancellationToken).GetLocation()
                        ?? symbol.Symbol.Locations.FirstOrDefault()
                        ?? Location.None;
                    symbol.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.SourceNarrowerThanScope,
                        location,
                        name,
                        (onMethod ? "the method '" : "the class '") + symbol.Symbol.Name + "'",
                        scope is 1 ? "Class" : "Assembly",
                        scope is 1 ? "the class and the assembly" : "the assembly",
                        scope is 1 ? "the class or the assembly" : "the assembly"));
                }
            }, SymbolKind.NamedType, SymbolKind.Method);
        });
    }
}
