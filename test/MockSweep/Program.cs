using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MockSweep;

// Usage: MockSweep --references <file> [--output <directory>]
// The references file is the one test/MockSweep.Packages writes when it builds.
string? referencesFile = null;
var output = Path.Combine("artifacts", "mock-sweep");
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--references" when i + 1 < args.Length:
            referencesFile = args[++i];
            break;
        case "--output" when i + 1 < args.Length:
            output = args[++i];
            break;
        default:
            Console.Error.WriteLine($"mock-sweep: unknown argument {args[i]}");
            return 2;
    }
}

if (referencesFile is null || !File.Exists(referencesFile))
{
    Console.Error.WriteLine("mock-sweep: pass --references with the file test/MockSweep.Packages writes. Build that project first.");
    return 2;
}

var input = SweepInput.Read(referencesFile);
var references = input.References.Select(r => (r.Source, Reference: (MetadataReference)MetadataReference.CreateFromFile(r.Path))).ToList();
var compilation = CSharpCompilation.Create(
    "MockSweep",
    [],
    references.Select(r => r.Reference).Append(MockCompiler.RuntimeReference),
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: true));

var groups = new List<SourceResult>();
foreach (var source in input.SweptSources)
{
    var watch = Stopwatch.StartNew();
    var assemblies = references
        .Where(r => r.Source == source)
        .Select(r => compilation.GetAssemblyOrModuleSymbol(r.Reference))
        .OfType<IAssemblySymbol>()
        .ToList();
    var interfaces = assemblies
        .SelectMany(a => PublicInterfaces(a.GlobalNamespace))
        .OrderBy(t => t.ToDisplayString(), StringComparer.Ordinal)
        .ToList();
    var outcomes = MockCompiler.Compile(compilation, interfaces, out var otherErrors, out _);
    groups.Add(new SourceResult(source, assemblies.Select(a => a.Name).Order().ToList(), outcomes, otherErrors, watch.Elapsed));
    Console.WriteLine($"{source}: {interfaces.Count} interfaces, {outcomes.Count(o => o.Failed)} failed, {watch.Elapsed.TotalSeconds:F1}s");
}

var report = new SweepReport(groups, input.PackagesWithoutAssemblies);
Directory.CreateDirectory(output);
var failedDirectory = Path.Combine(output, "failed");
if (Directory.Exists(failedDirectory))
{
    Directory.Delete(failedDirectory, recursive: true);
}

foreach (var failed in groups.SelectMany(g => g.Outcomes).Where(o => o.Failed))
{
    Directory.CreateDirectory(failedDirectory);
    File.WriteAllText(Path.Combine(failedDirectory, failed.MockName + ".g.cs"), failed.Source);
}

File.WriteAllText(Path.Combine(output, "report.md"), report.Markdown());
File.WriteAllText(Path.Combine(output, "report.json"), report.Json());

Console.WriteLine();
Console.WriteLine(report.Summary());
foreach (var failure in groups.SelectMany(g => g.Outcomes.Where(o => o.Failed).Select(o => (g.Source, o))))
{
    Console.WriteLine($"FAILED {failure.Source}: {failure.o.TypeName}");
    foreach (var error in failure.o.Errors.Take(3))
    {
        Console.WriteLine("    " + error);
    }
}

foreach (var error in groups.SelectMany(g => g.OtherErrors))
{
    Console.WriteLine("ERROR outside a mock: " + error);
}

return report.Passed ? 0 : 1;

static IEnumerable<INamedTypeSymbol> PublicInterfaces(INamespaceOrTypeSymbol container)
{
    foreach (var member in container.GetMembers())
    {
        switch (member)
        {
            case INamespaceSymbol ns:
                foreach (var type in PublicInterfaces(ns))
                {
                    yield return type;
                }

                break;
            case INamedTypeSymbol { DeclaredAccessibility: Accessibility.Public } type:
                if (type.TypeKind == TypeKind.Interface)
                {
                    yield return type;
                }

                foreach (var nested in PublicInterfaces(type))
                {
                    yield return nested;
                }

                break;
        }
    }
}
