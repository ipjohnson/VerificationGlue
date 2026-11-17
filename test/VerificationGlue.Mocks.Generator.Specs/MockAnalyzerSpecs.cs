using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using MockSweep;

namespace VerificationGlue.Mocks.Generator.Specs;

/// <summary>The diagnostics layer: each case is a snippet and the diagnostics it reports, with the text and line each one points at.</summary>
public class MockAnalyzerSpecs
{
    private const string Contracts = """
        global using System;
        global using VerificationGlue.Mocks;

        public interface ICalculator
        {
            int Add(int a, int b);
            int Length { get; }
        }

        public struct Point { }

        public interface IFactory<TSelf> where TSelf : IFactory<TSelf>
        {
            static abstract TSelf Create();
        }

        public interface IReader
        {
            int Read(Span<byte> buffer);
        }

        public interface IRepository<T>
        {
            T Find(int id);
        }

        public static class Helpers
        {
            public static int Sum(int a, int b) => a + b;
        }

        public interface IShop
        {
            IOrders Orders { get; }
        }

        public interface IOrders
        {
            IOrder Find(int id);
            IServiceProvider Services { get; }
        }

        public interface IOrder
        {
            ICustomer Customer { get; }
        }

        public interface ICustomer
        {
            string Name { get; }
        }
        """;

    [Fact]
    public void A_type_that_cannot_be_mocked_reports_VGM0001_at_each_place_that_names_it()
    {
        var diagnostics = Analyze("""
            [assembly: GenerateMock(typeof(Point))]
            [assembly: GenerateMock(typeof(IRepository<>))]

            public class Tests
            {
                [Mock] public Point Field;
                [Mock] public IFactory<Impl>? Factory;

                public void Run()
                {
                    Mock.Of<Tests>();
                }
            }

            public class Impl : IFactory<Impl> { public static Impl Create() => new(); }
            """);

        Assert.Equal(
            [
                "VGM0001 line 1 'GenerateMock(typeof(Point))': 'Point' cannot be mocked: it is a struct",
                "VGM0001 line 2 'GenerateMock(typeof(IRepository<>))': 'IRepository<>' cannot be mocked: it is an open generic type. Name a closed type, such as typeof(IRepository<Order>)",
                "VGM0001 line 6 'Mock': 'Point' cannot be mocked: it is a struct",
                "VGM0001 line 7 'Mock': 'IFactory<Impl>' cannot be mocked: 'Create' is a static abstract member",
                "VGM0001 line 11 'Mock.Of<Tests>()': 'Tests' cannot be mocked: class mocks are not supported yet",
            ],
            diagnostics);
    }

    [Fact]
    public void A_matcher_that_is_not_an_argument_of_a_mock_call_reports_VGM0002()
    {
        var diagnostics = Analyze("""
            public class Tests
            {
                public void Run(ICalculator calc)
                {
                    var any = Arg.Any<int>();
                    Helpers.Sum(Arg.Is<int>(x => x > 1), 2);
                    calc.Add(any, Arg.Any<int>());
                    calc.Add(Arg.Is(3), 4);
                }
            }
            """);

        Assert.Equal(
            [
                "VGM0002 line 5 'Arg.Any<int>()': 'Arg.Any<int>()' is not passed directly as an argument of a mock member, so its argument position is unknown and it waits for the next mock call",
                "VGM0002 line 6 'Arg.Is<int>(x => x > 1)': 'Arg.Is<int>(x => x > 1)' is not passed directly as an argument of a mock member, so its argument position is unknown and it waits for the next mock call",
            ],
            diagnostics);
    }

    [Fact]
    public void An_NSubstitute_style_matcher_passed_to_Setup_or_Calls_reports_VGM0002_as_an_error()
    {
        var diagnostics = Analyze(
            """
            public class Tests
            {
                public bool Run()
                {
                    var calc = Mock.Of<ICalculator>();
                    calc.Setup().Add(1, Arg.Any<int>()).Returns(3);
                    calc.Setup().Add(Arg.Is<int>(x => x > 0), Arg<int>.Any()).Returns(4);
                    return calc.Calls().Add(Arg.Is(2), 1) == 1;
                }
            }
            """,
            generate: true);

        Assert.Equal(
            [
                "Error VGM0002 line 6 'Arg.Any<int>()': 'Arg.Any<int>()' is an NSubstitute-style matcher, so this Arg<T> parameter matches only the default value and the matcher waits for the next mock call. Use 'Arg<int>.Any()'.",
                "Error VGM0002 line 7 'Arg.Is<int>(x => x > 0)': 'Arg.Is<int>(x => x > 0)' is an NSubstitute-style matcher, so this Arg<T> parameter matches only the default value and the matcher waits for the next mock call. Use 'Arg<int>.Is(x => x > 0)'.",
                "Error VGM0002 line 8 'Arg.Is(2)': 'Arg.Is(2)' is an NSubstitute-style matcher, so this Arg<T> parameter matches only the default value and the matcher waits for the next mock call. Use 'Arg<int>.Is(2)'.",
            ],
            diagnostics);
    }

    [Fact]
    public void Typed_setup_and_call_counts_report_nothing()
    {
        var diagnostics = Analyze(
            """
            public class Tests
            {
                public bool Run()
                {
                    var calc = Mock.Of<ICalculator>();
                    calc.Setup().Add(1, Arg<int>.Any()).Returns(3);
                    calc.Setup().Length.Returns(4);
                    calc.Add(Arg.Any<int>(), 2).Returns(5);
                    return calc.Calls().Add(Arg<int>.Is(x => x > 0), 1) == 1;
                }
            }
            """,
            generate: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void A_member_that_cannot_be_mocked_reports_VGM0003_where_the_mock_is_named()
    {
        var diagnostics = Analyze("""
            public class Tests
            {
                public void Run()
                {
                    Mock.Of<IReader>();
                }
            }
            """);

        Assert.Equal(
            ["VGM0003 line 5 'Mock.Of<IReader>()': 'IReader.Read' cannot be mocked: parameter 'buffer' is the ref struct 'System.Span<byte>'. The generated member throws NotSupportedException."],
            diagnostics);
    }

    [Fact]
    public void A_generic_helper_reached_with_an_undiscovered_type_reports_VGM0004()
    {
        var diagnostics = Analyze("""
            [assembly: GenerateMock(typeof(IRepository<string>))]

            public class Tests
            {
                public void Run()
                {
                    Make<ICalculator>();
                    Make<IRepository<string>>();
                    Make<IRepository<int>>();
                }

                private static T Make<T>() where T : class => Mock.Of<T>();
            }
            """);

        Assert.Equal(
            [
                "VGM0004 line 12 'Mock.Of<T>()': 'Tests.Make<T>()' calls Mock.Of<T>() with T = 'ICalculator', but no mock of 'ICalculator' is generated. Add [assembly: GenerateMock(typeof(ICalculator))].",
                "VGM0004 line 12 'Mock.Of<T>()': 'Tests.Make<T>()' calls Mock.Of<T>() with T = 'IRepository<int>', but no mock of 'IRepository<int>' is generated. Add [assembly: GenerateMock(typeof(IRepository<int>))].",
            ],
            diagnostics);
    }

    [Fact]
    public void Returns_or_Throws_on_a_value_that_is_not_a_mock_call_reports_VGM0005()
    {
        var diagnostics = Analyze("""
            public class Tests
            {
                public void Run(ICalculator calc)
                {
                    calc.Add(1, 2).Returns(3);
                    calc.Length.Returns(4);
                    "abc".Length.Returns(3);
                    var value = calc.Add(1, 2);
                    value.Throws(new Exception());
                }
            }
            """);

        Assert.Equal(
            [
                "VGM0005 line 7 '\"abc\".Length': '\"abc\".Length' is not a call to a mock member, so Returns would apply to whichever mock call came before it",
                "VGM0005 line 9 'value': 'value' is not a call to a mock member, so Throws would apply to whichever mock call came before it",
            ],
            diagnostics);
    }

    [Fact]
    public void A_member_beyond_the_default_depth_reports_VGM0007_where_the_walk_starts()
    {
        var diagnostics = Analyze("""
            public class Tests
            {
                public object Run() => Mock.Of<IShop>();
            }
            """);

        Assert.Equal(
            ["VGM0007 line 3 'Mock.Of<IShop>()': 'IOrder.Customer' returns 'ICustomer', which is beyond the recursion depth of 2, so an unconfigured call returns null. Raise VerificationGlueMocksRecursionDepth to return a mock."],
            diagnostics);
    }

    [Fact]
    public void VerificationGlueMocksRecursionDepth_sets_the_depth_VGM0007_reports_beyond()
    {
        var diagnostics = Analyze(
            """
            public class Tests
            {
                public object Run() => Mock.Of<IShop>();
            }
            """,
            depth: "1");

        Assert.Equal(
            ["VGM0007 line 3 'Mock.Of<IShop>()': 'IOrders.Find' returns 'IOrder', which is beyond the recursion depth of 1, so an unconfigured call returns null. Raise VerificationGlueMocksRecursionDepth to return a mock."],
            diagnostics);
    }

    [Fact]
    public void A_type_also_named_at_a_call_site_starts_its_own_walk_and_reports_no_VGM0007()
    {
        var diagnostics = Analyze("""
            public class Tests
            {
                public object Run() => Mock.Of<IShop>();
                public object Order() => Mock.Of<IOrder>();
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Correct_use_reports_nothing()
    {
        var diagnostics = Analyze("""
            public class Tests
            {
                [Mock] public ICalculator? Calculator { get; set; }

                public void Run()
                {
                    var calc = Mock.Of<ICalculator>();
                    calc.Add(Arg.Any<int>(), Arg.Is<int>(x => x > 0)).Returns(3);
                    calc.Received().Add(1, Arg.Is(2));
                    var repository = Mock.Of<IRepository<string>>();
                    repository.Find(Arg.Any<int>()).Throws(new Exception());
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Each diagnostic as "ID line N 'text at its location': message", with lines counted in
    /// <paramref name="source"/>. With <paramref name="generate"/> the generator runs first, so the
    /// source can use the typed <c>Setup()</c> and <c>Calls()</c> surface, and each line starts with
    /// the severity. <paramref name="depth"/> is the <c>VerificationGlueMocksRecursionDepth</c> property, unset when null.
    /// </summary>
    private static List<string> Analyze(string source, bool generate = false, string? depth = null)
    {
        var options = new BuildProperties(depth is null ? [] : new() { ["VerificationGlueMocksRecursionDepth"] = depth });
        var parseOptions = MockCompiler.ParseOptions.WithFeatures([new("InterceptorsNamespaces", "VerificationGlue.Mocks.Generated")]);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, "Tests.cs");
        Compilation compilation = CSharpCompilation.Create(
            "AnalyzerCase",
            [CSharpSyntaxTree.ParseText(Contracts, parseOptions, "Contracts.cs"), tree],
            ShapeCase.FrameworkReferences.Append(MockCompiler.RuntimeReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        if (generate)
        {
            CSharpGeneratorDriver.Create([new MockGenerator().AsSourceGenerator()], parseOptions: parseOptions, optionsProvider: options)
                .RunGeneratorsAndUpdateCompilation(compilation, out compilation, out _, TestContext.Current.CancellationToken);
        }

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));

        var diagnostics = compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MockAnalyzer()), new AnalyzerOptions([], options))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken)
            .GetAwaiter()
            .GetResult();

        return diagnostics
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ThenBy(d => d.GetMessage(), StringComparer.Ordinal)
            .Select(d =>
            {
                Assert.Same(tree, d.Location.SourceTree);
                var line = d.Location.GetLineSpan().StartLinePosition.Line + 1;
                var severity = generate ? d.Severity + " " : "";
                return $"{severity}{d.Id} line {line} '{tree.GetText().ToString(d.Location.SourceSpan)}': {d.GetMessage()}";
            })
            .ToList();
    }
}
