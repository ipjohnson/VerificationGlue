using System.IO;
using System.Linq;
using System.Text;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerificationGlue.Mocks;
using VerificationGlue.Mocks.Generator;

namespace Generator.Benchmarks;

/// <summary>
/// A test project for the mock generator: <c>interfaceCount</c> interfaces of
/// <c>membersPerInterface</c> members, one file that mocks each of them with <c>Mock.Of</c> and
/// <c>Arg</c> matchers, and an unrelated file to edit.
/// </summary>
public static class MockWorkload
{
    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Preview)
        .WithFeatures([new("InterceptorsNamespaces", "VerificationGlue.Mocks.Generated")]);

    private static readonly MetadataReference[] References =
        [.. PlatformReferences.All, MetadataReference.CreateFromFile(typeof(MockAdapter).Assembly.Location)];

    public static GeneratorDriverHarness Create(int interfaceCount, int membersPerInterface)
    {
        var harness = new GeneratorDriverHarness(new MockGenerator(), References, ParseOptions);
        var tests = new StringBuilder("using VerificationGlue.Mocks;\nnamespace Workload;\npublic class Tests\n{\n    public void Run()\n    {\n");
        for (var i = 0; i < interfaceCount; i++)
        {
            harness.WithSource($"I{i}.cs", Interface(i, membersPerInterface));
            tests.Append($"        var m{i} = Mock.Of<IService{i}>();\n");
            tests.Append($"        m{i}.Method0(Arg.Any<int>(), \"a\").Returns(1);\n");
        }

        tests.Append("    }\n}\n");
        return harness.WithSource("Tests.cs", tests.ToString()).WithSource("Unrelated.cs", Unrelated(0));
    }

    public static string Unrelated(int edit) => $"namespace Workload; public class Unrelated {{ public int Value => {edit}; }}";

    /// <summary>Compiles the input and the generated sources of the last run, as the build does after the generator.</summary>
    public static void Compile(GeneratorDriverHarness harness)
    {
        using var stream = new MemoryStream();
        var result = harness.OutputCompilation.Emit(stream);
        if (!result.Success)
        {
            throw new System.InvalidOperationException("The workload does not compile:\n" + string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(10)));
        }
    }

    private static string Interface(int index, int members)
    {
        var text = new StringBuilder($"namespace Workload;\npublic interface IService{index}\n{{\n");
        for (var m = 0; m < members; m++)
        {
            text.Append((m % 5) switch
            {
                0 => $"    int Method{m}(int a, string b);\n",
                1 => $"    System.Threading.Tasks.Task<string> Load{m}Async(int id, System.Threading.CancellationToken token);\n",
                2 => $"    string? Property{m} {{ get; set; }}\n",
                3 => $"    bool TryGet{m}(string key, out int value);\n",
                _ => $"    event System.EventHandler<int>? Changed{m};\n",
            });
        }

        return text.Append("}\n").ToString();
    }
}
