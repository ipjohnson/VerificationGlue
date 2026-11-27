using System.Collections.Immutable;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace VerificationGlue.Tests.DependencyInjection.Generator.Specs;

internal static class GeneratorHarness
{
    /// <summary>
    /// The files of src/VerificationGlue.Tests that the package's runtime and the specs' input use. They
    /// do not use MTP, so a spec compiles them with its input without the MTP 3 preview.
    /// </summary>
    private static readonly string[] TestsFiles =
    [
        "Scheduling.cs", "VerifyTestAttribute.cs", "TestDescriptor.cs", "TestRegistry.cs", "TestContext.cs", "TestRuntime.cs", "TestParameter.cs",
        "TestScope.cs", "TestContainer.cs", "ContainerAttribute.cs", "ExtensionPoints.cs", "TestRunner.cs",
        "RowAttribute.cs", "NameFormatter.cs", "DataSource.cs", "TraitAttribute.cs",
    ];

    /// <summary>The files of src/VerificationGlue.Tests.DependencyInjection.</summary>
    private static readonly string[] PackageFiles =
    [
        "ServiceCollectionAttribute.cs", "ServiceCollectionBootstrap.cs", "ServiceCollectionSource.cs", "ServiceProviderContainer.cs",
    ];

    public static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest)
        .WithFeatures([new("InterceptorsNamespaces", "VerificationGlue.Generated;VerificationGlue.Mocks.Generated")]);

    public const string Usings = """
        using Microsoft.Extensions.DependencyInjection;
        using VerificationGlue.Mocks;
        using VerificationGlue.Tests;
        using VerificationGlue.Tests.DependencyInjection;

        """;

    public static string RepositoryRoot { get; } = FindRoot();

    /// <summary>DependencyModules.Runtime, which a test project that uses DependencyModules references.</summary>
    public static readonly MetadataReference DependencyModulesRuntime =
        MetadataReference.CreateFromFile(typeof(DependencyModules.Runtime.Interfaces.IDependencyModule).Assembly.Location);

    /// <summary>
    /// A compilation of <paramref name="source"/>, the runtime files of VerificationGlue.Tests and of the
    /// package, VerificationGlue, VerificationGlue.Mocks, Microsoft.Extensions.DependencyInjection and
    /// <paramref name="references"/>.
    /// </summary>
    public static CSharpCompilation Compilation(string source, string declarations = "", params MetadataReference[] references) =>
        CSharpCompilation.Create(
            "SpecsInput",
            new[] { ("/repo/Tests/OrderTests.cs", source), ("/repo/Tests/Services.cs", Usings + declarations) }
                .Concat(TestsFiles.Select(file => (file, File.ReadAllText(Path.Combine(RepositoryRoot, "src", "VerificationGlue.Tests", file)))))
                .Concat(PackageFiles.Select(file => (file, File.ReadAllText(Path.Combine(RepositoryRoot, "src", "VerificationGlue.Tests.DependencyInjection", file)))))
                .Append(("Usings.cs", "global using System; global using System.Linq; global using System.Threading; global using System.Threading.Tasks; global using System.Collections.Generic;"))
                .Select(file => CSharpSyntaxTree.ParseText(file.Item2, ParseOptions, file.Item1)),
            [
                .. PlatformReferences.All,
                MetadataReference.CreateFromFile(typeof(VerifySyntax).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(VerificationGlue.Mocks.Mock).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location),
                .. references,
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    /// <summary>
    /// Runs <see cref="ConfigureGenerator"/> on <paramref name="source"/> and <paramref name="declarations"/>,
    /// which are two files that each start with <see cref="Usings"/>, and returns the run and the
    /// compilation with the generated file. The compilation also references <paramref name="references"/>.
    /// </summary>
    public static (GeneratorDriverRunResult Run, Compilation Output) Run(string source, string declarations = "", params MetadataReference[] references)
    {
        var driver = CSharpGeneratorDriver.Create([new ConfigureGenerator().AsSourceGenerator()], parseOptions: ParseOptions)
            .RunGeneratorsAndUpdateCompilation(Compilation(Usings + source, declarations, references), out var output, out _, CancellationToken.None);
        return (driver.GetRunResult(), output);
    }

    public static ImmutableArray<Diagnostic> Analyze(string source, string declarations, params DiagnosticAnalyzer[] analyzers)
    {
        var compilation = Compilation(Usings + source, declarations);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return compilation
            .WithAnalyzers([.. analyzers])
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public static byte[] Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return stream.ToArray();
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VerificationGlue.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No VerificationGlue.slnx above {AppContext.BaseDirectory}.");
    }
}
