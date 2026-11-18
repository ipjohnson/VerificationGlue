using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace VerificationGlue.Tests.AspNetCore.Generator.Specs;

internal static class GeneratorHarness
{
    /// <summary>
    /// <c>IWebAppEntry</c> as src/VerificationGlue.Tests.AspNetCore declares it. That project depends on
    /// the MTP 3 preview, which these xunit specs cannot load, so the specs compile the interface with
    /// their input.
    /// </summary>
    private const string Interface = """
        namespace VerificationGlue.Tests.AspNetCore;

        public interface IWebAppEntry
        {
            static abstract string ApplicationName { get; }

            static abstract int Run(string[] args);
        }
        """;

    public static string RepositoryRoot { get; } = FindRoot();

    /// <summary>
    /// Runs <see cref="EntryGenerator"/> on <paramref name="source"/> with the runtime's
    /// <c>WebAppEntryAttribute</c>, and returns the run and the compilation with the generated files.
    /// </summary>
    public static (GeneratorDriverRunResult Run, Compilation Output) Run(string source, params MetadataReference[] references)
    {
        var driver = Driver().RunGeneratorsAndUpdateCompilation(Compilation(source, references), out var output, out _, TestContext.Current.CancellationToken);
        return (driver.GetRunResult(), output);
    }

    public static CSharpCompilation Compilation(string source, params MetadataReference[] references) =>
        CSharpCompilation.Create(
            "SpecsInput",
            [
                CSharpSyntaxTree.ParseText(source, path: "Entry.cs"),
                CSharpSyntaxTree.ParseText(Interface, path: "IWebAppEntry.cs"),
                CSharpSyntaxTree.ParseText(
                    File.ReadAllText(Path.Combine(RepositoryRoot, "src", "VerificationGlue.Tests.AspNetCore", "WebAppEntryAttribute.cs")),
                    path: "WebAppEntryAttribute.cs"),
            ],
            [.. PlatformReferences.All, .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    public static GeneratorDriver Driver() =>
        CSharpGeneratorDriver.Create(
            [new EntryGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

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
