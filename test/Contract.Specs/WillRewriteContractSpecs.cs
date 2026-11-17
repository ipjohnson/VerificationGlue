using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using VerificationGlue;
using VerificationGlue.Generator;
using VerificationGlue.Mocks.Generator;
using VerificationGlue.Tests.Generator;

namespace Contract.Specs;

/// <summary>
/// The contract between the generator that writes <c>__Verified</c> copies and the generators that
/// call them: with both compiling the shared <c>WillRewrite</c>, every call the runner writes binds,
/// whether rewriting is on or off and in whatever order the generators run.
/// </summary>
public class WillRewriteContractSpecs
{
    private const string Tests = """
        using System;
        using System.Threading.Tasks;
        using VerificationGlue;
        using VerificationGlue.Mocks;
        using static VerificationGlue.VerifySyntax;

        namespace Contract;

        [VerifyRewrite]
        [AttributeUsage(AttributeTargets.Method)]
        public sealed class VerifyTestAttribute : Attribute
        {
        }

        public interface ICalculator
        {
            int Add(int a, int b);
        }

        public partial class CalculatorTests
        {
            [VerifyTest]
            public void Counts_calls()
            {
                var calc = Mock.Of<ICalculator>();
                calc.Add(1, 2);
                Verify(calc.Calls().Add(1, 2) == 2);
            }

            [VerifyTest]
            public async Task Awaits()
            {
                await Task.Yield();
                Verify(Total(2) == 5);
            }

            [VerifyTest]
            public void Calls_a_helper() => Totals(3);

            [VerifyTest]
            public virtual void Virtual() => Verify(Total(1) == 0);

            [VerifyTest]
            public static void Static() => Verify(Total(1) == 0);

            [VerifyTest]
            public void Generic<T>() => Verify(Total(1) == 0);

            [Verify]
            private static void Totals(int expected) => Verify(Total(1) == expected);

            private static int Total(int value) => value + 1;
        }

        public class NotPartialTests
        {
            [VerifyTest]
            public void Not_partial() => Verify(Value() == 3);

            private static int Value() => 2;
        }
        """;

    private const string FileTypeTests = """
        using static VerificationGlue.VerifySyntax;

        namespace Contract;

        file sealed class Hidden
        {
        }

        public partial class FileTypeTests
        {
            [VerifyTest]
            public void In_a_file_with_a_file_type() => Verify(new Hidden() is null);
        }
        """;

    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest)
        .WithFeatures([new("InterceptorsNamespaces", "VerificationGlue.Generated;VerificationGlue.Mocks.Generated")]);

    public static TheoryData<bool, bool> Combinations => new()
    {
        { false, false },
        { false, true },
        { true, false },
        { true, true },
    };

    [Theory]
    [MemberData(nameof(Combinations))]
    public async Task Every_runner_call_binds_and_runs(bool rewrite, bool reverseOrder)
    {
        var runner = Build(rewrite, reverseOrder);

        var expected = new Dictionary<string, (string Target, string Message)>
        {
            ["Contract.CalculatorTests.Counts_calls"] = rewrite
                ? ("Counts_calls__Verified", "Verify failed: calc.Calls().Add(1, 2) == 2\n  calc.Calls().Add(1, 2) → 1\n")
                : ("Counts_calls", "Verify failed: calc.Calls().Add(1, 2) == 2\n  expected calc.Calls().Add(1, 2) to equal 2\n"),
            ["Contract.CalculatorTests.Awaits"] = rewrite
                ? ("Awaits__Verified", "Verify failed: Total(2) == 5\n  Total(2) → 3\n")
                : ("Awaits", "Verify failed: Total(2) == 5\n  expected Total(2) to equal 5\n"),
            ["Contract.CalculatorTests.Calls_a_helper"] = rewrite
                ? ("Calls_a_helper__Verified", "Verify failed: Total(1) == expected\n  Total(1) → 2\n  expected → 3\n")
                : ("Calls_a_helper", "Verify failed: Total(1) == expected\n  expected Total(1) to equal expected\n"),
            ["Contract.CalculatorTests.Virtual"] = ("Virtual", "Verify failed: Total(1) == 0\n  expected Total(1) to equal 0\n"),
            ["Contract.CalculatorTests.Static"] = rewrite
                ? ("Static__Verified", "Verify failed: Total(1) == 0\n  Total(1) → 2\n")
                : ("Static", "Verify failed: Total(1) == 0\n  expected Total(1) to equal 0\n"),
            ["Contract.NotPartialTests.Not_partial"] = ("Not_partial", "Verify failed: Value() == 3\n  expected Value() to equal 3\n"),
            ["Contract.FileTypeTests.In_a_file_with_a_file_type"] = ("In_a_file_with_a_file_type", "Verify failed: new Hidden() is null\n  expected new Hidden() to be null\n"),
        };

        Assert.Equal(expected.Keys.Order(), runner.Keys.Order());
        foreach (var (id, (target, message)) in expected)
        {
            var (actualTarget, run) = runner[id];
            Assert.Equal(target, actualTarget);
            var failure = await Assert.ThrowsAsync<VerifyException>(run);
            Assert.StartsWith(message, failure.Message);
        }
    }

    [Fact]
    public void A_copy_exists_exactly_when_rewriting_is_on_and_WillRewrite_is_true()
    {
        foreach (var rewrite in new[] { false, true })
        {
            var (input, output) = Run(rewrite, reverseOrder: false);

            foreach (var tree in input.SyntaxTrees)
            {
                var model = input.GetSemanticModel(tree);
                var token = TestContext.Current.CancellationToken;
                foreach (var declaration in tree.GetRoot(token).DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>())
                {
                    var method = (IMethodSymbol)model.GetDeclaredSymbol(declaration, token)!;
                    if (!VerificationGlue.Shared.VerifyRewriteRules.HasRewriteAttribute(method, out _))
                    {
                        continue;
                    }

                    var copies = output.GetTypeByMetadataName(method.ContainingType.ToDisplayString())!.GetMembers(method.Name + "__Verified");
                    Assert.True(
                        copies.Length == (rewrite && VerificationGlue.Shared.VerifyRewriteRules.WillRewrite(method) ? 1 : 0),
                        $"{method.ContainingType.Name}.{method.Name} has {copies.Length} copies with rewriting {(rewrite ? "on" : "off")}.");
                }
            }
        }
    }

    [Fact]
    public void A_test_with_syntax_errors_is_neither_copied_nor_called_through_a_copy()
    {
        var source = """
            using static VerificationGlue.VerifySyntax;

            namespace Contract;

            [VerificationGlue.VerifyRewrite]
            public sealed class VerifyTestAttribute : System.Attribute { }

            public partial class BrokenTests
            {
                [VerifyTest]
                public void Broken() { Verify(1 == 2) }
            }
            """;
        var compilation = Compilation(("/repo/Contract/Broken.cs", source));

        var result = Driver(rewrite: true, reverseOrder: false).RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();

        Assert.DoesNotContain(result.GeneratedTrees, tree => tree.FilePath.EndsWith(".Verified.g.cs", StringComparison.Ordinal));
        Assert.Contains("[\"Contract.BrokenTests.Broken\"] = (\"Broken\"", result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith("Runner.g.cs", StringComparison.Ordinal)).ToString());
    }

    private static Dictionary<string, (string Target, Func<Task> Run)> Build(bool rewrite, bool reverseOrder)
    {
        var (_, output) = Run(rewrite, reverseOrder);
        using var stream = new MemoryStream();
        var emitted = output.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        stream.Position = 0;
        var assembly = new AssemblyLoadContext("Contract", isCollectible: true).LoadFromStream(stream);
        var tests = assembly.GetType("Contract.Generated.Runner", throwOnError: true)!.GetProperty("Tests")!.GetValue(null)!;
        return (Dictionary<string, (string, Func<Task>)>)tests;
    }

    private static (CSharpCompilation Input, Compilation Output) Run(bool rewrite, bool reverseOrder)
    {
        var input = Compilation(("/repo/Contract/CalculatorTests.cs", Tests), ("/repo/Contract/FileTypeTests.cs", FileTypeTests));
        var driver = Driver(rewrite, reverseOrder).RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(diagnostics.Concat(driver.GetRunResult().Diagnostics), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        return (input, output);
    }

    private static GeneratorDriver Driver(bool rewrite, bool reverseOrder)
    {
        ISourceGenerator[] generators =
        [
            new VerificationGlueGenerator().AsSourceGenerator(),
            new MockGenerator().AsSourceGenerator(),
            new TypedCallsGenerator().AsSourceGenerator(),
            new TestGenerator().AsSourceGenerator(),
            new RunnerGenerator().AsSourceGenerator(),
        ];

        return CSharpGeneratorDriver.Create(
            reverseOrder ? generators.Reverse() : generators,
            parseOptions: ParseOptions,
            optionsProvider: new Options(rewrite));
    }

    private static CSharpCompilation Compilation(params (string Path, string Text)[] sources) =>
        CSharpCompilation.Create(
            "Contract",
            sources.Select(source => CSharpSyntaxTree.ParseText(source.Text, ParseOptions, source.Path)),
            [
                .. PlatformReferences.All,
                MetadataReference.CreateFromFile(typeof(VerifySyntax).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(VerificationGlue.Mocks.Mock).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

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
                    "build_property.ProjectDir" => "/repo/Contract/",
                    "build_property.VerificationGlueRewrite" when rewrite => "true",
                    _ => null,
                };
                return value is not null;
            }
        }
    }
}
