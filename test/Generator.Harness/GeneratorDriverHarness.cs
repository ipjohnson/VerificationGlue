using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Generator.Harness;

/// <summary>
/// Runs one incremental generator over an in-memory compilation and keeps the driver between runs,
/// so a spec can edit a source file, run again and read why each tracked step ran.
/// </summary>
public sealed class GeneratorDriverHarness
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    private readonly Dictionary<string, SyntaxTree> trees = new(StringComparer.Ordinal);
    private CSharpCompilation compilation;
    private GeneratorDriver driver;

    public GeneratorDriverHarness(IIncrementalGenerator generator, IEnumerable<MetadataReference>? references = null)
    {
        compilation = CSharpCompilation.Create(
            "HarnessInput",
            [],
            references ?? PlatformReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    /// <summary>The result of the last <see cref="Run"/>.</summary>
    public GeneratorDriverRunResult LastRun { get; private set; } = null!;

    /// <summary>The input compilation with the generated sources of the last run added.</summary>
    public Compilation OutputCompilation { get; private set; } = null!;

    /// <summary>Adds a source file, or replaces the file with the same path.</summary>
    public GeneratorDriverHarness WithSource(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, ParseOptions, path);
        compilation = trees.TryGetValue(path, out var old)
            ? compilation.ReplaceSyntaxTree(old, tree)
            : compilation.AddSyntaxTrees(tree);
        trees[path] = tree;
        return this;
    }

    public GeneratorDriverHarness WithoutSource(string path)
    {
        if (!trees.Remove(path, out var old))
        {
            throw new ArgumentException($"No source file {path}.", nameof(path));
        }

        compilation = compilation.RemoveSyntaxTrees(old);
        return this;
    }

    public GeneratorDriverRunResult Run()
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        OutputCompilation = output;
        LastRun = driver.GetRunResult();
        return LastRun;
    }

    /// <summary>
    /// The reasons of every output of the step with this tracking name in the last run. The name is
    /// one given with <c>WithTrackingName</c>, or <c>SourceOutput</c> for the source output step.
    /// </summary>
    public IReadOnlyList<IncrementalStepRunReason> StepReasons(string trackingName)
    {
        var result = LastRun?.Results.Single() ?? throw new InvalidOperationException("Call Run first.");
        if (!result.TrackedSteps.TryGetValue(trackingName, out var steps)
            && !result.TrackedOutputSteps.TryGetValue(trackingName, out steps))
        {
            var known = result.TrackedSteps.Keys.Concat(result.TrackedOutputSteps.Keys).OrderBy(name => name, StringComparer.Ordinal);
            throw new ArgumentException($"No tracked step {trackingName}. Tracked steps: {string.Join(", ", known)}.", nameof(trackingName));
        }

        return [.. steps.SelectMany(step => step.Outputs).Select(output => output.Reason)];
    }

    /// <summary>Errors and warnings of the output compilation, to check that the generated code compiles.</summary>
    public IReadOnlyList<Diagnostic> OutputCompilationDiagnostics() =>
        [.. OutputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)];
}

/// <summary>The assemblies of the running .NET, as metadata references.</summary>
public static class PlatformReferences
{
    public static IReadOnlyList<MetadataReference> All { get; } =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => Path.GetFileName(path) is var name && (name.StartsWith("System.", StringComparison.Ordinal) || name is "mscorlib.dll" or "netstandard.dll"))
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];
}
