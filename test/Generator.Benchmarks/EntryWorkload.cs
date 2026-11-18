using System.IO;
using System.Linq;
using System.Text;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using VerificationGlue.Tests.AspNetCore.Generator;

namespace Generator.Benchmarks;

/// <summary>
/// A test project for the entry generator: one <c>[WebAppEntry("ShopApp")]</c> class,
/// <c>testClassCount</c> test classes and an unrelated file to edit, over a reference to ShopApp, an
/// app with top-level statements and <c>appTypeCount</c> types.
/// </summary>
public static class EntryWorkload
{
    public const string AppName = "ShopApp";

    private static readonly string AttributeSource = ReadAttributeSource();

    public static GeneratorDriverHarness Create(int testClassCount, MetadataReference app)
    {
        var harness = new GeneratorDriverHarness(new EntryGenerator())
            .WithSource("WebAppEntryAttribute.cs", AttributeSource)
            .WithSource("ShopAppEntry.cs", $"namespace Workload; [VerificationGlue.Tests.AspNetCore.WebAppEntry(\"{AppName}\")] public sealed partial class ShopAppEntry;")
            .WithReference(AppName, app);
        for (var i = 0; i < testClassCount; i++)
        {
            harness.WithSource($"Tests{i}.cs", $"namespace Workload; public class Tests{i} {{ public void Places_an_order() {{ var total = {i} + 1; }} }}");
        }

        return harness.WithSource("Unrelated.cs", Unrelated(0));
    }

    public static string Unrelated(int edit) => $"namespace Workload; public class Unrelated {{ public int Value => {edit}; }}";

    /// <summary>
    /// The reference assembly of ShopApp, as a project reference passes it. <paramref name="version"/>
    /// changes a constant in one type, so two versions differ as two builds of an edited app do.
    /// </summary>
    public static byte[] App(int appTypeCount, int version)
    {
        var sources = new StringBuilder("namespace ShopApp.Services;\n");
        for (var i = 0; i < appTypeCount; i++)
        {
            sources.Append($$"""
                public sealed class Service{{i}}
                {
                    public const int Version = {{(i == 0 ? version : 0)}};
                    public string Name { get; set; } = "";
                    public int Count(int a, string b) => a + b.Length;
                    public System.Threading.Tasks.Task<string> LoadAsync(int id, System.Threading.CancellationToken token) => System.Threading.Tasks.Task.FromResult(Name);
                    internal void Reset() => Name = "";
                }

                """);
        }

        var compilation = CSharpCompilation.Create(
            AppName,
            [
                CSharpSyntaxTree.ParseText("System.Console.WriteLine(new ShopApp.Services.Service0().Name);", path: "Program.cs"),
                CSharpSyntaxTree.ParseText(sources.ToString(), path: "Services.cs"),
            ],
            PlatformReferences.All,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, options: new EmitOptions(metadataOnly: true, includePrivateMembers: false));
        if (!result.Success)
        {
            throw new System.InvalidOperationException("ShopApp does not compile:\n" + string.Join("\n", result.Diagnostics.Take(10)));
        }

        return stream.ToArray();
    }

    /// <summary>Throws unless the last run wrote the entry type, so a benchmark cannot time a run that failed.</summary>
    public static void CheckGenerated(GeneratorDriverHarness harness)
    {
        if (harness.LastRun.GeneratedTrees.Length != 1 || harness.LastRun.Diagnostics.Length != 0)
        {
            throw new System.InvalidOperationException("The entry generator did not write ShopAppEntry:\n" + string.Join("\n", harness.LastRun.Diagnostics));
        }
    }

    private static string ReadAttributeSource()
    {
        using var stream = typeof(EntryWorkload).Assembly.GetManifestResourceStream("WebAppEntryAttribute.cs")
            ?? throw new InvalidDataException("The benchmark assembly does not embed WebAppEntryAttribute.cs.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
