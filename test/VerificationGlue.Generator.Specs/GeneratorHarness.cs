using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Compiles sources the way a project that references <c>VerificationGlue</c> does: with the runtime,
/// the generator, the <c>InterceptorsNamespaces</c> feature and a project directory.
/// </summary>
internal static class GeneratorHarness
{
    public const string ProjectDirectory = "/repo/Project/";

    public static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest)
        .WithFeatures([new("InterceptorsNamespaces", InterceptorEmitter.GeneratedNamespace)]);

    public static IReadOnlyList<MetadataReference> References { get; } =
        [.. PlatformReferences.All, MetadataReference.CreateFromFile(typeof(VerifySyntax).Assembly.Location)];

    public static GeneratorDriverRunResult Run(IIncrementalGenerator generator, string source, params MetadataReference[] references) =>
        Generate(Compilation(("/repo/Project/Input.cs", source)), generator, out _);

    public static CSharpCompilation Compilation(params (string Path, string Text)[] sources) =>
        CSharpCompilation.Create(
            "SpecsInput",
            sources.Select(source => CSharpSyntaxTree.ParseText(source.Text, ParseOptions, source.Path)),
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    public static GeneratorDriverRunResult Generate(CSharpCompilation compilation, IIncrementalGenerator generator, out Compilation output)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: ParseOptions,
            optionsProvider: new ProjectOptions(ProjectDirectory));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out _);
        return driver.GetRunResult();
    }

    /// <summary>Generates, compiles and loads the sources. Fails on any compile warning or error.</summary>
    public static Assembly Load(params (string Path, string Text)[] sources)
    {
        var result = Generate(Compilation(sources), new VerificationGlueGenerator(), out var output);
        Assert.Empty(result.Diagnostics);
        using var stream = new MemoryStream();
        var emitted = output.Emit(stream);
        var problems = emitted.Diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).ToList();
        if (problems.Count > 0)
        {
            Assert.Fail(string.Join("\n", problems.Select(problem => $"{problem} in: {problem.Location.SourceTree?.GetText().Lines.GetLineFromPosition(problem.Location.SourceSpan.Start)}")));
        }

        stream.Position = 0;
        return new AssemblyLoadContext("SpecsInput", isCollectible: true).LoadFromStream(stream);
    }

    /// <summary>Runs the analyzer over the source and returns its diagnostics in source order.</summary>
    public static ImmutableArray<Diagnostic> Analyze(string source)
    {
        var compilation = Compilation(("/repo/Project/Input.cs", source));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.ToString()));
        return compilation
            .WithAnalyzers([new VerifyAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().Result
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ToImmutableArray();
    }

    private sealed class ProjectOptions(string projectDirectory) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(projectDirectory);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;

        private sealed class Options(string? projectDirectory) : AnalyzerConfigOptions
        {
            public static readonly Options Empty = new(null);

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                value = key == VerificationGlueGenerator.ProjectDirectoryProperty ? projectDirectory : null;
                return value is not null;
            }
        }
    }
}
