using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MockSweep;

namespace VerificationGlue.Mocks.Generator.Specs;

/// <summary>
/// One hand-written shape file. Its header names the expected outcome and any library it needs.
/// Every interface the file declares is mocked, generic ones as open definitions, and so is the
/// type of every field of a class named MockTargets.
/// </summary>
internal sealed class ShapeCase
{
    public static string Directory { get; } = Path.Combine(
        typeof(ShapeCase).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "VerificationGlue.ProjectDirectory").Value!,
        "Shapes");

    public static IReadOnlyList<MetadataReference> FrameworkReferences { get; } =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.StartsWith(RuntimeEnvironment.GetRuntimeDirectory(), StringComparison.Ordinal))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

    private ShapeCase(string source, IReadOnlyList<string> expected, IReadOnlyList<(string File, string? Alias)> libraries, bool allowUnsafe)
    {
        Source = source;
        Expected = expected;
        Libraries = libraries;
        AllowUnsafe = allowUnsafe;
    }

    public string Source { get; }

    /// <summary>The diagnostic IDs the case expects, sorted. Empty for <c>expect: pass</c>.</summary>
    public IReadOnlyList<string> Expected { get; }

    public IReadOnlyList<(string File, string? Alias)> Libraries { get; }

    public bool AllowUnsafe { get; }

    public static IEnumerable<string> Files() =>
        System.IO.Directory.GetFiles(Directory, "*.cs").Select(Path.GetFileName).Order(StringComparer.Ordinal)!;

    public static ShapeCase Load(string file)
    {
        var source = File.ReadAllText(Path.Combine(Directory, file));
        var expected = new List<string>();
        var libraries = new List<(string, string?)>();
        var allowUnsafe = false;
        foreach (var line in source.Split('\n').Select(l => l.Trim()).TakeWhile(l => l.StartsWith("//", StringComparison.Ordinal)))
        {
            var text = line[2..].Trim();
            if (text.StartsWith("expect:", StringComparison.Ordinal))
            {
                expected.AddRange(text["expect:".Length..].Split(',', StringSplitOptions.TrimEntries).Where(id => id != "pass"));
            }
            else if (text.StartsWith("library:", StringComparison.Ordinal))
            {
                var parts = text["library:".Length..].Split(" alias ", StringSplitOptions.TrimEntries);
                libraries.Add((parts[0], parts.Length > 1 ? parts[1] : null));
            }
            else if (text == "options: unsafe")
            {
                allowUnsafe = true;
            }
        }

        return new ShapeCase(source, expected.Order(StringComparer.Ordinal).ToList(), libraries, allowUnsafe);
    }

    public ShapeResult Compile()
    {
        var references = FrameworkReferences.Append(MockCompiler.RuntimeReference).ToList();
        foreach (var (file, alias) in Libraries)
        {
            references.Add(CompileLibrary(file, alias));
        }

        var tree = CSharpSyntaxTree.ParseText(Source, MockCompiler.ParseOptions);
        var compilation = CSharpCompilation.Create(
            "ShapeCase",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: AllowUnsafe));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => "case source: " + d)
            .ToList();

        var outcomes = MockCompiler.Compile(compilation, Targets(compilation, tree), out var otherErrors, out _);
        errors.AddRange(outcomes.SelectMany(o => o.Errors.Select(e => $"{o.TypeName}: {e}")));
        errors.AddRange(otherErrors.Select(e => "outside a mock: " + e));
        var ids = outcomes.SelectMany(o => o.Diagnostics).Select(d => d.Id).Distinct().Order(StringComparer.Ordinal).ToList();
        return new ShapeResult(errors, ids, outcomes);
    }

    private static List<INamedTypeSymbol> Targets(CSharpCompilation compilation, SyntaxTree tree)
    {
        var model = compilation.GetSemanticModel(tree);
        var targets = new List<INamedTypeSymbol>();
        foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(declaration) is INamedTypeSymbol { TypeKind: TypeKind.Interface } declared)
            {
                targets.Add(declared);
            }
            else if (model.GetDeclaredSymbol(declaration) is INamedTypeSymbol { Name: "MockTargets" } holder)
            {
                targets.AddRange(holder.GetMembers().OfType<IFieldSymbol>()
                    .Select(f => f.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated))
                    .OfType<INamedTypeSymbol>());
            }
        }

        return targets.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
    }

    private static MetadataReference CompileLibrary(string file, string? alias)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var library = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Directory, file)), MockCompiler.ParseOptions)],
            FrameworkReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: true));
        using var image = new MemoryStream();
        var result = library.Emit(image);
        if (!result.Success)
        {
            throw new InvalidOperationException($"{file} does not compile:\n" + string.Join("\n", result.Diagnostics));
        }

        var reference = MetadataReference.CreateFromImage(image.ToArray());
        return alias is null ? reference : reference.WithAliases([alias]);
    }
}

internal sealed record ShapeResult(IReadOnlyList<string> Errors, IReadOnlyList<string> DiagnosticIds, IReadOnlyList<MockOutcome> Outcomes);
