using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using VerificationGlue;
using VerificationGlue.Generator;

namespace Hardening.Specs;

public enum GeneratorMode
{
    /// <summary>The generator runs with <c>VerificationGlueRewrite</c> on, as the cases expect.</summary>
    Rewrite,

    /// <summary>The generator runs with tier 3 off.</summary>
    NoRewrite,

    /// <summary>The generator does not run.</summary>
    Off,
}

/// <summary>
/// Compiles a <see cref="CaseSource"/> the way a project that references VerificationGlue does, and its
/// baseline as plain C# with no generator and no reference to VerificationGlue.
/// </summary>
public static class CaseCompiler
{
    public const string GeneratedNamespace = "VerificationGlue.Generated";

    /// <summary>The configuration a failure report records.</summary>
    public const string Configuration = "C# latest, Debug, nullable enabled, Roslyn from Microsoft.CodeAnalysis.CSharp";

    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest)
        .WithFeatures([new("InterceptorsNamespaces", GeneratedNamespace)]);

    // Debug keeps the JIT from inlining a case method into its caller, which would hide the frame
    // that tells the tier.
    private static readonly CSharpCompilationOptions Options =
        new(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug, nullableContextOptions: NullableContextOptions.Enable);

    private static readonly MetadataReference ProbeReference = MetadataReference.CreateFromFile(typeof(Probe).Assembly.Location);

    private static readonly MetadataReference VerifyReference = MetadataReference.CreateFromFile(typeof(VerifySyntax).Assembly.Location);

    public static CompiledBatch Compile(CaseSource source, GeneratorMode mode)
    {
        var compilation = CSharpCompilation.Create(
            "HardeningCases",
            [CSharpSyntaxTree.ParseText(source.Text, ParseOptions, CaseSource.CasesPath)],
            [.. PlatformReferences.All, ProbeReference, VerifyReference],
            Options);

        Compilation output = compilation;
        if (mode != GeneratorMode.Off)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new VerificationGlueGenerator().AsSourceGenerator()],
                parseOptions: ParseOptions,
                optionsProvider: new ProjectOptions(mode == GeneratorMode.Rewrite));
            driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out var generatorDiagnostics);
            Fail(generatorDiagnostics, "the generator");
        }

        var baseline = CSharpCompilation.Create(
            "HardeningBaseline",
            [CSharpSyntaxTree.ParseText(source.Baseline, ParseOptions, CaseSource.BaselinePath)],
            [.. PlatformReferences.All, ProbeReference],
            Options);

        var context = new AssemblyLoadContext("Hardening", isCollectible: true);
        var cases = Load(context, output, "the cases");
        var plain = Load(context, baseline, "the baseline");
        return new CompiledBatch(source, cases, plain, context, Intercepted(cases));
    }

    private static Assembly Load(AssemblyLoadContext context, Compilation compilation, string what)
    {
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Fail(emitted.Diagnostics, what);
        stream.Position = 0;
        return context.LoadFromStream(stream);
    }

    private static void Fail(IEnumerable<Diagnostic> diagnostics, string what)
    {
        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Compiling {what} failed:\n" + string.Join("\n", errors.Select(error =>
                    $"{error} in: {error.Location.SourceTree?.GetText().Lines.GetLineFromPosition(error.Location.SourceSpan.Start)}")));
        }
    }

    // An interceptor's name ends with the line and column of the call it replaces, such as Verify_16_9.
    // The case methods' names end in no number.
    private static HashSet<(int, int)> Intercepted(Assembly assembly) =>
    [
        .. assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => Regex.Match(method.Name, @"_(\d+)_(\d+)$"))
            .Where(match => match.Success)
            .Select(match => (int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture))),
    ];

    private sealed class ProjectOptions(bool rewrite) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(rewrite);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;

        private sealed class Options(bool rewrite, bool global = true) : AnalyzerConfigOptions
        {
            public static readonly Options Empty = new(false, global: false);

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                value = key switch
                {
                    VerificationGlueGenerator.ProjectDirectoryProperty when global => "/repo/Project/",
                    "build_property.VerificationGlueRewrite" when rewrite => "true",
                    _ => null,
                };
                return value is not null;
            }
        }
    }
}

/// <summary>The loaded cases and baseline of one batch. Disposing unloads them.</summary>
public sealed class CompiledBatch(CaseSource source, Assembly cases, Assembly baseline, AssemblyLoadContext context, HashSet<(int, int)> intercepted) : IDisposable
{
    public CaseSource Source => source;

    public bool IsIntercepted((int Line, int Column) site) => intercepted.Contains(site);

    public Delegate Form(string name, Form form, bool async)
    {
        var method = cases.GetType($"Generated.{name}", throwOnError: true)!.GetMethod($"Run{form}")!;
        return async ? method.CreateDelegate<Func<Probe, Task>>() : method.CreateDelegate<Action<Probe>>();
    }

    public Delegate Baseline(string name, string shape, bool async)
    {
        var method = baseline.GetType($"Baseline.{name}", throwOnError: true)!.GetMethod(shape)!;
        return async ? method.CreateDelegate<Func<Probe, Task>>() : method.CreateDelegate<Action<Probe>>();
    }

    public void Dispose() => context.Unload();
}
