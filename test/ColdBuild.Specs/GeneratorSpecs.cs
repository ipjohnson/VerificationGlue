using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using ColdBuild.Generator;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ColdBuild.Specs;

public class GeneratorSpecs
{
    private const string Failing = """
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Bench;

        public partial class Failing
        {
            [VerifyTest]
            public void Fails()
            {
                var count = 2;
                Verify(count > 3);
            }

            public void CallsHelper() => Helper(5);

            [Verify]
            private static void Helper(int value)
            {
                Verify(value < 3);
            }
        }
        """;

    [Fact]
    public void Tier_2_intercepts_every_call_with_a_message_from_its_shape()
    {
        var (assembly, generated) = Build(rewrite: false);

        Assert.Equal(3, generated.Count(tree => tree.FilePath.EndsWith(".Interceptors.g.cs", StringComparison.Ordinal)));
        Assert.DoesNotContain(generated, tree => tree.FilePath.EndsWith(".Verified.g.cs", StringComparison.Ordinal));
        Assert.Contains("expected count to be greater than 3", Invoke(assembly, "Fails"));
        Assert.Contains("expected value to be less than 3", Invoke(assembly, "CallsHelper"));
        RunsEveryTest(assembly, copies: false);
    }

    [Fact]
    public void Tier_3_copies_eligible_methods_and_redirects_direct_calls_to_them()
    {
        var (assembly, generated) = Build(rewrite: true);

        Assert.Equal(3, generated.Count(tree => tree.FilePath.EndsWith(".Redirects.g.cs", StringComparison.Ordinal)));
        Assert.Contains("count = 2", Invoke(assembly, "Fails__Verified"));
        Assert.Contains("value = 5", Invoke(assembly, "CallsHelper"));

        // The runner calls the copy, so the original body gets no interceptor and keeps tier 1.
        Assert.Equal("Verify failed: count > 3", Invoke(assembly, "Fails"));
        RunsEveryTest(assembly, copies: true);
    }

    private static (Assembly Assembly, IReadOnlyList<SyntaxTree> Generated) Build(bool rewrite)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([new("InterceptorsNamespaces", "VerificationGlue.Generated")]);
        var sources = new TestSources("Bench", 20, 10).Files().Append((Path: "Failing.cs", Text: Failing));
        var compilation = CSharpCompilation.Create(
            "Bench" + Guid.NewGuid().ToString("N"),
            sources.Select(source => CSharpSyntaxTree.ParseText(source.Text, parse, "/src/" + source.Path)),
            [.. PlatformReferences.All, MetadataReference.CreateFromFile(typeof(VerificationGlue.VerifyAttribute).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var options = new Options(rewrite ? ImmutableDictionary<string, string>.Empty.Add(ColdBuildGenerator.RewriteProperty, "true") : ImmutableDictionary<string, string>.Empty);
        CSharpGeneratorDriver.Create([new ColdBuildGenerator().AsSourceGenerator()], parseOptions: parse, optionsProvider: options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        using var stream = new MemoryStream();
        var emit = output.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        var generated = output.SyntaxTrees.Skip(compilation.SyntaxTrees.Count()).ToArray();
        return (new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(new MemoryStream(stream.ToArray())), generated);
    }

    private static void RunsEveryTest(Assembly assembly, bool copies)
    {
        var tests = assembly.GetTypes().Where(type => type.Name.StartsWith("OrderTests", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(method => method.Name.StartsWith("Test", StringComparison.Ordinal) && method.Name.EndsWith("__Verified", StringComparison.Ordinal) == copies)
            .ToArray();

        Assert.Equal(20, tests.Length);
        foreach (var test in tests)
        {
            test.Invoke(Activator.CreateInstance(test.DeclaringType!), null);
        }
    }

    private static string Invoke(Assembly assembly, string method)
    {
        var type = assembly.GetType("Bench.Failing")!;
        var target = type.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => target.Invoke(Activator.CreateInstance(type), null));
        return Assert.IsType<VerificationGlue.VerifyException>(error.InnerException).Message;
    }

    private sealed class Options(ImmutableDictionary<string, string> global) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Values(global);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Values(ImmutableDictionary<string, string>.Empty);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Values(ImmutableDictionary<string, string>.Empty);

        private sealed class Values(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
        }
    }
}
