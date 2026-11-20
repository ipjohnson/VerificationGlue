using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MockSweep;

// Usage: MockSweep --references <file> [--output <directory>] [--classifications <file> [--write-classifications]]
// The references file is the one test/MockSweep.Packages writes when it builds. With --classifications
// the run fails when an interface's outcome differs from the pinned one, unless --write-classifications
// rewrites the file with this run's outcomes.
string? referencesFile = null;
string? classificationsFile = null;
var writeClassifications = false;
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
        case "--classifications" when i + 1 < args.Length:
            classificationsFile = args[++i];
            break;
        case "--write-classifications":
            writeClassifications = true;
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

if (writeClassifications && classificationsFile is null)
{
    Console.Error.WriteLine("mock-sweep: --write-classifications needs --classifications with the file to write.");
    return 2;
}

if (classificationsFile is not null && !writeClassifications && !File.Exists(classificationsFile))
{
    Console.Error.WriteLine($"mock-sweep: the classifications file {classificationsFile} does not exist. Pass --write-classifications to create it.");
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
var swept = new List<INamedTypeSymbol>();
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
    swept.AddRange(interfaces);
    var outcomes = MockCompiler.Compile(compilation, interfaces, out var otherErrors, out _);
    groups.Add(new SourceResult(source, assemblies.Select(a => a.Name).Order().ToList(), outcomes, otherErrors, watch.Elapsed));
    Console.WriteLine($"{source}: {interfaces.Count} interfaces, {outcomes.Count(o => o.Failed)} failed, {watch.Elapsed.TotalSeconds:F1}s");
}

var classifications = SweepClassifications.Of(groups);
ClassificationChanges? changes = null;
if (classificationsFile is not null && writeClassifications)
{
    File.WriteAllText(classificationsFile, SweepClassifications.Write(classifications));
    Console.WriteLine($"Wrote {classifications.Count} classifications to {classificationsFile}.");
}
else if (classificationsFile is not null)
{
    changes = SweepClassifications.Compare(SweepClassifications.Read(File.ReadAllText(classificationsFile)), classifications);
}

var report = new SweepReport(groups, input.PackagesWithoutAssemblies, changes);
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

var depthWatch = Stopwatch.StartNew();
var depths = RecursionDepths.Markdown(RecursionDepths.Measure(compilation, swept));
File.WriteAllText(Path.Combine(output, "report.md"), report.Markdown() + Environment.NewLine + depths);
File.WriteAllText(Path.Combine(output, "report.json"), report.Json());

Console.WriteLine();
Console.WriteLine(depths);
Console.WriteLine($"Recursion depths measured in {depthWatch.Elapsed.TotalSeconds:F1}s.");
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

if (changes is { None: false })
{
    Console.WriteLine();
    Console.WriteLine($"The outcomes differ from {classificationsFile}. Review each change, then rerun with --write-classifications.");
    foreach (var (kind, list) in new[] { ("REGRESSED", changes.Regressions), ("CHANGED", changes.Improvements), ("ADDED", changes.Added), ("REMOVED", changes.Removed) })
    {
        foreach (var change in list)
        {
            Console.WriteLine($"{kind} {change}");
        }
    }
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
