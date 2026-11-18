using System.Reflection;

namespace VerificationGlue.Generator.Specs;

public class InterceptorSpecs
{
    private const string Path = "/repo/Project/Tests/Shapes.cs";

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using static VerificationGlue.VerifySyntax;

        namespace Sample;

        public sealed class Order
        {
            public int Total { get; set; } = 30;
            public List<int> Lines { get; } = new() { 1, 2 };
        }

        public static class Cases
        {
            private static readonly Order order = new();
            private static readonly string? missing = null;
            private static readonly string present = "x";
            private static readonly object boxed = "text";

            public static void NotNullOperator() => Verify(missing != null);
            public static void NullOnTheLeft() => Verify(null != missing);
            public static void NotNullPattern() => Verify(missing is not null);
            public static void EmptyPropertyPattern() => Verify(missing is { });
            public static void NullOperator() => Verify(present == null);
            public static void NullPattern() => Verify(present is null);
            public static void Equal() => Verify(order.Total == 40);
            public static void NotEqual() => Verify(order.Total != 30);
            public static void LessThan() => Verify(order.Total < 10);
            public static void AtMost() => Verify(order.Total <= 10);
            public static void GreaterThan() => Verify(order.Total > 50);
            public static void AtLeast() => Verify(order.Total >= 50);
            public static void IsType() => Verify(boxed is int);
            public static void DeclarationPattern() => Verify(boxed is int number);
            public static void PropertyPattern() => Verify(order is { Total: > 50 });
            public static void RelationalPattern() => Verify(order.Total is > 50 and < 60);
            public static void Not() => Verify(!present.StartsWith("x"));
            public static void BoolCall() => Verify(present.StartsWith("y"));
            public static void NoShape() => Verify(order.Total > 0 && present.Length > 5);
            public static void Parenthesized() => Verify((order.Total == 40));
            public static void Qualified() => VerificationGlue.VerifySyntax.Verify(order.Total == 40);
            public static void NamedArguments() => Verify(expression: "the total", condition: order.Total == 40);
            public static void MultiLine() => Verify(
                order.Total
                    == 40);
            public static void MultiLineOperand() => Verify(present == @"y
        z");
            public static void WithMessage() => Verify(order.Total == 40, $"order total {order.Total}");
            public static void CheckWithoutScope() => Check(order.Total == 40);
            public static void ThrowsNothing() => Throws<InvalidOperationException>(() => order.ToString());
            public static void ThrowsOther() => Throws<InvalidOperationException>(() => throw new FormatException("bad digit"));
            public static void ThrowsBlock() => Throws<InvalidOperationException>(() => { order.ToString(); });
            public static void ThrowsTypeParameter() => ThrowsOf<InvalidOperationException>();
            public static Task ThrowsAsyncNothing() => ThrowsAsync<TimeoutException>(() => Task.Delay(1));

            private static void ThrowsOf<TException>() where TException : Exception => Throws<TException>(() => { });

            public static string Passes()
            {
                Verify(order.Total == 30);
                Verify(order.Total == 30, $"never formatted {Fail()}");
                var thrown = Throws<FormatException>(() => throw new FormatException("expected"));
                return thrown.Message;
            }

            public static async Task<string> PassesAsync()
            {
                var thrown = await ThrowsAsync<TimeoutException>(async () =>
                {
                    await Task.Yield();
                    throw new TimeoutException("slow");
                });
                return thrown.Message;
            }

            private static string Fail() => throw new InvalidOperationException("a hole ran");
        }
        """;

    private static readonly Lazy<Type> Cases = new(() => GeneratorHarness.Load((Path, Source)).GetType("Sample.Cases", throwOnError: true)!);

    public static IEnumerable<(string Method, string Expected)> Shapes =>
    [
        ("NotNullOperator", "Verify failed: missing != null\n  missing can not be null"),
        ("NullOnTheLeft", "Verify failed: null != missing\n  missing can not be null"),
        ("NotNullPattern", "Verify failed: missing is not null\n  missing can not be null"),
        ("EmptyPropertyPattern", "Verify failed: missing is { }\n  missing can not be null"),
        ("NullOperator", "Verify failed: present == null\n  expected present to be null"),
        ("NullPattern", "Verify failed: present is null\n  expected present to be null"),
        ("Equal", "Verify failed: order.Total == 40\n  expected order.Total to equal 40"),
        ("NotEqual", "Verify failed: order.Total != 30\n  expected order.Total to not equal 30"),
        ("LessThan", "Verify failed: order.Total < 10\n  expected order.Total to be less than 10"),
        ("AtMost", "Verify failed: order.Total <= 10\n  expected order.Total to be at most 10"),
        ("GreaterThan", "Verify failed: order.Total > 50\n  expected order.Total to be greater than 50"),
        ("AtLeast", "Verify failed: order.Total >= 50\n  expected order.Total to be at least 50"),
        ("IsType", "Verify failed: boxed is int\n  expected boxed to be of type int"),
        ("DeclarationPattern", "Verify failed: boxed is int number\n  expected boxed to be of type int"),
        ("PropertyPattern", "Verify failed: order is { Total: > 50 }\n  expected order to match { Total: > 50 }"),
        ("RelationalPattern", "Verify failed: order.Total is > 50 and < 60\n  expected order.Total to match > 50 and < 60"),
        ("Not", "Verify failed: !present.StartsWith(\"x\")\n  expected present.StartsWith(\"x\") to be false"),
        ("BoolCall", "Verify failed: present.StartsWith(\"y\")\n  expected present.StartsWith(\"y\") to return true"),
        ("NoShape", "Verify failed: order.Total > 0 && present.Length > 5"),
        ("Parenthesized", "Verify failed: order.Total == 40\n  expected order.Total to equal 40"),
        ("Qualified", "Verify failed: order.Total == 40\n  expected order.Total to equal 40"),
        ("NamedArguments", "Verify failed: the total\n  expected order.Total to equal 40"),
        ("WithMessage", "Verify failed: order.Total == 40\n  expected order.Total to equal 40\n  order total 30"),
        ("CheckWithoutScope", "Check failed: order.Total == 40\n  expected order.Total to equal 40"),
        ("ThrowsNothing", "Throws failed: () => order.ToString()\n  expected order.ToString() to throw InvalidOperationException\n  no exception was thrown"),
        ("ThrowsOther", "Throws failed: () => throw new FormatException(\"bad digit\")\n  expected throw new FormatException(\"bad digit\") to throw InvalidOperationException\n  it threw System.FormatException: bad digit"),
        ("ThrowsBlock", "Throws failed: () => { order.ToString(); }\n  expected () => { order.ToString(); } to throw InvalidOperationException\n  no exception was thrown"),
    ];

    [VerifyTest]
    [RowSource(nameof(Shapes))]
    public Task Each_shape_reports_its_message_file_and_line(string method, string expected) => OutsideTheRunnersScope(() =>
    {
        var failure = Fail(method);

        Assert.Equal(expected + $"\n  at Tests/Shapes.cs:{LineOf(method)}", failure.Message);
    });

    [VerifyTest]
    public void A_check_in_a_scope_is_reported_when_the_scope_ends()
    {
        var scope = VerifyScope.Begin();
        Cases.Value.GetMethod("CheckWithoutScope")!.Invoke(null, null);

        var failure = Assert.Throws<VerifyException>(scope.Dispose);

        Assert.Equal($"1 check failed\n\nCheck failed: order.Total == 40\n  expected order.Total to equal 40\n  at Tests/Shapes.cs:{LineOf("CheckWithoutScope")}", failure.Message);
    }

    [VerifyTest]
    public void A_multi_line_call_reports_the_line_where_it_starts()
    {
        var failure = Fail("MultiLine");

        Assert.EndsWith($"\n  expected order.Total to equal 40\n  at Tests/Shapes.cs:{LineOf("MultiLine")}", failure.Message);
    }

    [VerifyTest]
    public void An_operand_on_several_lines_gives_no_shape_line()
    {
        var failure = Fail("MultiLineOperand");

        Assert.Equal($"Verify failed: present == @\"y\nz\"\n  at Tests/Shapes.cs:{LineOf("MultiLineOperand")}", failure.Message);
    }

    [VerifyTest]
    public void Throws_with_a_type_parameter_names_the_type_at_run_time()
    {
        var failure = Fail("ThrowsTypeParameter");

        var line = Array.FindIndex(Source.Split('\n'), line => line.Contains("Throws<TException>", StringComparison.Ordinal)) + 1;
        Assert.Equal($"Throws failed: () => {{ }}\n  expected InvalidOperationException\n  no exception was thrown\n  at Tests/Shapes.cs:{line}", failure.Message);
    }

    [VerifyTest]
    public async Task ThrowsAsync_reports_its_message_file_and_line()
    {
        var task = (Task)Cases.Value.GetMethod("ThrowsAsyncNothing")!.Invoke(null, null)!;

        var failure = await Assert.ThrowsAsync<VerifyException>(() => task);

        Assert.Equal(
            $"ThrowsAsync failed: () => Task.Delay(1)\n  expected Task.Delay(1) to throw TimeoutException\n  no exception was thrown\n  at Tests/Shapes.cs:{LineOf("ThrowsAsyncNothing")}",
            failure.Message);
    }

    [VerifyTest]
    public async Task Passing_calls_do_not_throw_or_format_the_message()
    {
        Assert.Equal("expected", Cases.Value.GetMethod("Passes")!.Invoke(null, null));
        Assert.Equal("slow", await (Task<string>)Cases.Value.GetMethod("PassesAsync")!.Invoke(null, null)!);
    }

    [VerifyTest]
    public void Each_file_gets_one_interceptor_file()
    {
        var other = """
            using static VerificationGlue.VerifySyntax;
            static class Other { static void M() => Verify(1 + 1 == 2); }
            """;
        var compilation = GeneratorHarness.Compilation((Path, Source), ("/repo/Project/Other.cs", other), ("/repo/Project/None.cs", "class None { }"));

        var result = GeneratorHarness.Generate(compilation, new VerificationGlueGenerator(), out _);

        Assert.Equal(
            ["Other.", "Shapes."],
            result.GeneratedTrees.Select(tree => System.IO.Path.GetFileName(tree.FilePath)).Select(name => name[..(name.IndexOf('.') + 1)]).Order());
    }

    [VerifyTest]
    public void A_method_of_another_type_named_Verify_is_not_intercepted()
    {
        var source = """
            static class Mine
            {
                static void Verify(bool condition) { }
                static void M() => Verify(false);
            }
            """;

        var result = GeneratorHarness.Run(new VerificationGlueGenerator(), source);

        Assert.Empty(result.GeneratedTrees);
    }

    [VerifyTest]
    [Row("/repo/Project/Tests/A.cs", "/repo/Project/", "Tests/A.cs")]
    [Row("/repo/Project/Tests/A.cs", "/repo/Project", "Tests/A.cs")]
    [Row(@"C:\repo\Project\Tests\A.cs", @"C:\repo\Project\", "Tests/A.cs")]
    [Row("/elsewhere/Shared/A.cs", "/repo/Project/", "A.cs")]
    [Row("/repo/Project/A.cs", "", "A.cs")]
    public void Paths_are_relative_to_the_project(string path, string projectDirectory, string expected)
    {
        Assert.Equal(expected, InterceptorEmitter.DisplayPath(path, projectDirectory));
    }

    // VerificationGlue.Tests runs each test inside a VerifyScope, and CheckWithoutScope needs none.
    // The body runs without the execution context, so it starts with no scope.
    private static Task OutsideTheRunnersScope(Action body)
    {
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(body);
        }
    }

    private static VerifyException Fail(string method)
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Cases.Value.GetMethod(method)!.Invoke(null, null));
        return Assert.IsType<VerifyException>(thrown.InnerException);
    }

    private static int LineOf(string method)
    {
        var lines = Source.Split('\n');
        return Array.FindIndex(lines, line => line.Contains($" {method}()", StringComparison.Ordinal)) + 1;
    }
}
