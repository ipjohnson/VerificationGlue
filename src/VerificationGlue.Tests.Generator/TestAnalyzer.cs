using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Tests.Generator;

/// <summary>
/// Reports the <c>[VerifyTest]</c> methods the runner cannot call (VGT0004), the attributes the
/// registration leaves out (VGT0006), test IDs that two tests share (VGT0001), <c>[Row]</c> values
/// that do not match their parameters (VGT0002), <c>[RunAfter]</c> cycles between schedule groups
/// (VGT0003), and tests with <c>[Mock]</c> parameters under a shared container that cannot take
/// overrides (VGT0008).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [Diagnostics.DuplicateUid, Diagnostics.RowMismatch, Diagnostics.RunAfterCycle, Diagnostics.NotRunnable, Diagnostics.AttributeNotMaterialized, Diagnostics.SharedContainerWithoutChildOverrides];

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
            var waits = new ConcurrentDictionary<(string From, string To, Location Location), byte>();
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
                foreach (var row in RowReader.Read(method, compilation))
                {
                    if (row.Error is not null)
                    {
                        var rowLocation = row.Attribute.ApplicationSyntaxReference?.GetSyntax(symbol.CancellationToken).GetLocation() ?? location;
                        symbol.ReportDiagnostic(Diagnostic.Create(Diagnostics.RowMismatch, rowLocation, method.Name, row.Error));
                    }
                }

                var group = TestDiscovery.Schedule(method, compilation).Group;
                foreach (var (attribute, waitsOn) in TestDiscovery.RunAfter(method))
                {
                    var attributeLocation = attribute.ApplicationSyntaxReference?.GetSyntax(symbol.CancellationToken).GetLocation() ?? location;
                    waits.TryAdd((group, waitsOn, attributeLocation), 0);
                }

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

                ReportCycles(waits.Keys, end.ReportDiagnostic);
            });
        });
    }

    /// <summary>
    /// Reports each <c>[RunAfter]</c> whose group can be reached again from the group it waits on, with
    /// the shortest path back.
    /// </summary>
    private static void ReportCycles(IEnumerable<(string From, string To, Location Location)> waits, System.Action<Diagnostic> report)
    {
        var edges = waits
            .OrderBy(wait => wait.Location.SourceTree?.FilePath, System.StringComparer.Ordinal)
            .ThenBy(wait => wait.Location.SourceSpan.Start)
            .ToList();
        var next = edges
            .GroupBy(edge => edge.From, System.StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.To).Distinct(System.StringComparer.Ordinal).ToList(), System.StringComparer.Ordinal);

        foreach (var (from, to, location) in edges)
        {
            if (Path(next, to, from) is { } path)
            {
                report(Diagnostic.Create(Diagnostics.RunAfterCycle, location, from, to, string.Join(" -> ", new[] { from }.Concat(path))));
            }
        }
    }

    /// <summary>The shortest list of groups from <paramref name="start"/> to <paramref name="target"/>, both included, or null.</summary>
    private static List<string>? Path(Dictionary<string, List<string>> next, string start, string target)
    {
        var previous = new Dictionary<string, string?>(System.StringComparer.Ordinal) { [start] = null };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var group = queue.Dequeue();
            if (group == target)
            {
                var path = new List<string>();
                for (string? step = group; step is not null; step = previous[step])
                {
                    path.Add(step);
                }

                path.Reverse();
                return path;
            }

            foreach (var following in next.TryGetValue(group, out var list) ? list : [])
            {
                if (!previous.ContainsKey(following))
                {
                    previous.Add(following, group);
                    queue.Enqueue(following);
                }
            }
        }

        return null;
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
                RowReader.IsRow(attribute) ||
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
