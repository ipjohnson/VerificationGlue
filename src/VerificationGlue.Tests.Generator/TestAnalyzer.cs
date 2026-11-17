using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Tests.Generator;

/// <summary>
/// Reports the <c>[VerifyTest]</c> methods the runner cannot call (VGT0004), the attributes the
/// registration leaves out (VGT0006), test IDs that two tests share (VGT0001), and tests with
/// <c>[Mock]</c> parameters under a shared container that cannot take overrides (VGT0008).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [Diagnostics.DuplicateUid, Diagnostics.NotRunnable, Diagnostics.AttributeNotMaterialized, Diagnostics.SharedContainerWithoutChildOverrides];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(KnownTypes.VerifyTestAttribute) is not { } verifyTest)
            {
                return;
            }

            var uids = new ConcurrentBag<(string Uid, Location Location)>();
            var reported = new ConcurrentDictionary<SyntaxReference, byte>();
            var assemblyChecked = 0;

            start.RegisterSymbolAction(symbol =>
            {
                var method = (IMethodSymbol)symbol.Symbol;
                if (!method.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, verifyTest)))
                {
                    return;
                }

                var location = method.Locations.FirstOrDefault() ?? Location.None;
                if (TestDiscovery.WhyNotRunnable(method) is { } reason)
                {
                    symbol.ReportDiagnostic(Diagnostic.Create(Diagnostics.NotRunnable, location, method.Name, reason));
                    return;
                }

                uids.Add((TestDiscovery.Uid(method), location));
                var compilation = symbol.Compilation;
                if (TestDiscovery.SharedContainerWithoutChildOverrides(method, compilation) is { } shared)
                {
                    symbol.ReportDiagnostic(Diagnostic.Create(Diagnostics.SharedContainerWithoutChildOverrides, location, method.Name, shared.Bootstrap, shared.Scope));
                }

                Check(method.GetAttributes(), compilation, reported, symbol.ReportDiagnostic, symbol.CancellationToken);
                foreach (var parameter in method.Parameters)
                {
                    Check(parameter.GetAttributes(), compilation, reported, symbol.ReportDiagnostic, symbol.CancellationToken);
                }

                if (TestDiscovery.Constructor(method, compilation) is { } constructor)
                {
                    foreach (var parameter in constructor.Parameters)
                    {
                        Check(parameter.GetAttributes(), compilation, reported, symbol.ReportDiagnostic, symbol.CancellationToken);
                    }
                }

                Check(method.ContainingType.GetAttributes(), compilation, reported, symbol.ReportDiagnostic, symbol.CancellationToken);
                if (Interlocked.Exchange(ref assemblyChecked, 1) == 0)
                {
                    Check(compilation.Assembly.GetAttributes(), compilation, reported, symbol.ReportDiagnostic, symbol.CancellationToken);
                }
            }, SymbolKind.Method);

            start.RegisterCompilationEndAction(end =>
            {
                foreach (var group in uids.GroupBy(entry => entry.Uid, System.StringComparer.Ordinal).Where(group => group.Count() > 1))
                {
                    foreach (var (uid, location) in group.OrderBy(entry => entry.Location.SourceTree?.FilePath, System.StringComparer.Ordinal).ThenBy(entry => entry.Location.SourceSpan.Start))
                    {
                        end.ReportDiagnostic(Diagnostic.Create(Diagnostics.DuplicateUid, location, uid));
                    }
                }
            });
        });
    }

    private static void Check(
        ImmutableArray<AttributeData> attributes,
        Compilation compilation,
        ConcurrentDictionary<SyntaxReference, byte> reported,
        System.Action<Diagnostic> report,
        CancellationToken cancellationToken)
    {
        foreach (var attribute in attributes)
        {
            if (AttributeMaterializer.IsSkipped(attribute) ||
                attribute.ApplicationSyntaxReference is not { } syntax ||
                AttributeMaterializer.WhyNotMaterialized(attribute, compilation) is not { } reason ||
                !reported.TryAdd(syntax, 0))
            {
                continue;
            }

            var name = attribute.AttributeClass is { TypeKind: not TypeKind.Error } type
                ? type.ToDisplayString()
                : syntax.GetSyntax(cancellationToken).ToString();
            report(Diagnostic.Create(Diagnostics.AttributeNotMaterialized, syntax.GetSyntax(cancellationToken).GetLocation(), name, reason));
        }
    }
}
