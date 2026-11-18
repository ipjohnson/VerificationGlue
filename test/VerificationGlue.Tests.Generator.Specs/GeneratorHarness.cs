using System.Diagnostics.CodeAnalysis;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Tests.Generator.Specs;

internal static class GeneratorHarness
{
    /// <summary>
    /// The files of src/VerificationGlue.Tests that generated registrations call. They do not use MTP,
    /// so a spec compiles them with its input and runs the registrations without the MTP 3 preview,
    /// which these xunit specs cannot load.
    /// </summary>
    private static readonly string[] RuntimeFiles =
    [
        "Scheduling.cs", "VerifyTestAttribute.cs", "TestDescriptor.cs", "TestRegistry.cs", "TestContext.cs", "TestRuntime.cs", "TestParameter.cs",
        "TestScope.cs", "TestContainer.cs", "ContainerAttribute.cs", "ExtensionPoints.cs", "TestRunner.cs",
    ];

    public static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest)
        .WithFeatures([new("InterceptorsNamespaces", "VerificationGlue.Generated;VerificationGlue.Mocks.Generated")]);

    public static string RepositoryRoot { get; } = FindRoot();

    public static GeneratorDriverRunResult Run(IIncrementalGenerator generator, string source, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
            "SpecsInput",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location), .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(generator).RunGenerators(compilation).GetRunResult();
    }

    /// <summary>A compilation of <paramref name="sources"/>, the runtime files, VerificationGlue and VerificationGlue.Mocks.</summary>
    public static CSharpCompilation Compilation(IEnumerable<(string Path, string Text)> sources, params MetadataReference[] references) =>
        CSharpCompilation.Create(
            "SpecsInput",
            sources
                .Concat(RuntimeFiles.Select(file => (file, File.ReadAllText(Path.Combine(RepositoryRoot, "src", "VerificationGlue.Tests", file)))))
                .Append(("Usings.cs", "global using System; global using System.Linq; global using System.Threading; global using System.Threading.Tasks; global using System.Collections.Generic;"))
                .Select(source => CSharpSyntaxTree.ParseText(source.Item2, ParseOptions, source.Item1)),
            [
                .. PlatformReferences.All,
                MetadataReference.CreateFromFile(typeof(VerifySyntax).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(VerificationGlue.Mocks.Mock).Assembly.Location),
                .. references,
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    public static GeneratorDriver Driver(bool rewrite, params IIncrementalGenerator[] generators) =>
        CSharpGeneratorDriver.Create(
            generators.Select(generator => generator.AsSourceGenerator()),
            parseOptions: ParseOptions,
            optionsProvider: new Options(rewrite));

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

    private sealed class Options(bool rewrite) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Values(rewrite);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Values.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Values.Empty;

        private sealed class Values(bool rewrite) : AnalyzerConfigOptions
        {
            public static readonly Values Empty = new(false);

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                value = key switch
                {
                    "build_property.ProjectDir" => "/repo/Tests/",
                    "build_property.VerificationGlueRewrite" when rewrite => "true",
                    _ => null,
                };
                return value is not null;
            }
        }
    }
}
