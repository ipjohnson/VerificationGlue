using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Tier 3: the rules of the rewrite in <c>docs/design/verification-glue.md</c>, one or more cases
/// each, and the once-only conformance cases. Each case calls a <c>__Verified</c> copy, which is
/// what a redirected call or the test runner runs.
/// </summary>
public class RewriteSpecs
{
    private const string Path = "/repo/Project/Tests/Rewrite.cs";

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Runtime.CompilerServices;
        using System.Threading.Tasks;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;
        using Builder = System.Text.StringBuilder;

        namespace Sample;

        public sealed class Counter
        {
            public int Calls;

            public int Next() => ++Calls;
        }

        public sealed class Order
        {
            public int TotalReads;
            public int NameReads;

            public int Total
            {
                get
                {
                    if (++TotalReads > 1)
                    {
                        throw new InvalidOperationException("Total was read twice");
                    }

                    return 30;
                }
            }

            public string Name
            {
                get
                {
                    NameReads++;
                    return "a";
                }
            }

            public override string ToString() => "order";
        }

        public struct MutableCounter
        {
            public int Value;

            public int Next() => ++Value;
        }

        public static partial class Extensions
        {
            public static int Second(this List<int> items) => items[1];

            [Verify]
            public static void HasCount(this List<int> items, int count) => Verify(items.Count == count);
        }

        public static partial class Cases
        {
            public static MutableCounter Shared;

            [Verify]
            public static void Values()
            {
                var s = "hello";
                Verify(s.StartsWith("x") && s.Length < 10);
            }

            [Verify]
            public static void OnceOnly(Counter counter) => Verify(counter.Next() + counter.Next() == 10 || counter.Next() == 0);

            [Verify]
            public static void GetterReadOnce(Order order) => Verify(order.Total == 40);

            [Verify]
            public static void Passes(int x) => Verify(x + 1 > x && Math.Abs(x) >= 0);

            [Verify]
            public static void Constants(int x)
            {
                const int Limit = 3;
                Verify(x > Limit + 1);
            }

            [Verify]
            public static void MutationByIncrement()
            {
                var x = 1;
                Verify(x++ == x);
            }

            [Verify]
            public static void MutationByRef()
            {
                var x = 1;
                Verify(x == Bump(ref x) && x == 3);
            }

            [Verify]
            public static void MutationByClosure()
            {
                var x = 1;
                Func<int> bump = () => ++x;
                Verify(x + bump() == x);
            }

            [Verify]
            public static void SkippedAnd(string? s) => Verify(s != null && s.Length > 3);

            [Verify]
            public static void SkippedOr(int x) => Verify(!(x == 0 || Explode()));

            [Verify]
            public static void SkippedConditional(bool flag) => Verify(flag ? No() : Explode());

            [Verify]
            public static void SkippedCoalesce(string? name) => Verify((name ?? Fallback()).Length == 0);

            [Verify]
            public static int OutVarScope(string text)
            {
                Verify(int.TryParse(text, out var number));
                return number;
            }

            [Verify]
            public static void Embedded(bool ready, int x)
            {
                if (ready) Verify(x > 1);
            }

            [Verify]
            public static async Task Awaited() => Verify(await Task.FromResult(2) == 3);

            [Verify]
            public static void PropertyPattern(Order order) => Verify(order is { Total: 40, Name: "a" });

            [Verify]
            public static void RefStruct(int[] values) => Verify(values.AsSpan().Length == 0);

            [Verify]
            public static void MutableReceiver() => Verify(Shared.Next() == 5);

            [Verify]
            public static void Anonymous()
            {
                var item = new { Name = "a" };
                Verify(item.Name == "b");
            }

            [Verify]
            public static void AnonymousWithDeclaration()
            {
                var item = new { Name = "a" };
                Verify(Pass(item, out var count).Name == "b" && count == 1);
            }

            [Verify]
            public static string CallerName()
            {
                var name = Name();
                Verify(name.Length > 0);
                return name;
            }

            [Verify]
            public static void UsesAlias()
            {
                var text = new Builder("ab");
                Verify(text.Length == 3);
            }

            [Verify]
            public static void UsesExtension(List<int> items) => Verify(items.Second() == 5);

            [Verify]
            public static void Checks(int x)
            {
                Check(x > 1);
                Check(x > 2);
            }

            [Verify]
            public static void WithReason(int x) => Verify(x == 1).Because("the discount applies");

            [Verify]
            public static void WithMessage(int x) => Verify(x == 1, $"x is {x}");

            [Verify]
            public static void InLambda(int x)
            {
                Action check = () => Verify(x == 1);
                check();
            }

            [Verify]
            public static void ThrowsInCopy() => Throws<InvalidOperationException>(() => { });

            [Verify]
            public static void Helper(int x) => Verify(x == 1);

            public static void CallsHelper(int x) => Helper(x);

            public static void CallsExtension(List<int> items) => items.HasCount(3);

            [Verify]
            public static void HelperFromCopy(int x) => Helper(x);

            private static int Bump(ref int value) => ++value;

            private static bool Explode() => throw new InvalidOperationException("skipped side ran");

            private static bool No() => false;

            private static string Fallback() => throw new InvalidOperationException("skipped side ran");

            private static T Pass<T>(T value, out int count)
            {
                count = 0;
                return value;
            }

            private static string Name([CallerMemberName] string name = "") => name;
        }
        """;

    private static readonly Lazy<Type> Cases = new(() => GeneratorHarness.Load(rewrite: true, (Path, Source)).GetType("Sample.Cases", throwOnError: true)!);

    [Fact]
    public void A_failure_lists_the_captured_values()
    {
        var failure = Fail("Values");

        Assert.Equal(
            Lines(
                "Verify failed: s.StartsWith(\"x\") && s.Length < 10",
                "  s.StartsWith(\"x\") → false",
                "  s                 → \"hello\"",
                "  s.Length < 10     → <not evaluated>",
                "  s.Length          → <not evaluated>",
                $"  at Tests/Rewrite.cs:{LineOf("s.StartsWith(\"x\") && s.Length < 10")}"),
            failure.Message);
    }

    [Fact]
    public void Rule_1_each_part_runs_once_in_the_original_order()
    {
        var counter = Activator.CreateInstance(Cases.Value.Assembly.GetType("Sample.Counter")!)!;

        var failure = Fail("OnceOnly", counter);

        Assert.Equal(3, (int)counter.GetType().GetField("Calls")!.GetValue(counter)!);
        Assert.Equal(
            Lines(
                "Verify failed: counter.Next() + counter.Next() == 10 || counter.Next() == 0",
                "  counter.Next() + counter.Next() == 10 → false",
                "  counter.Next() + counter.Next()       → 3",
                "  counter.Next()                        → 1",
                "  counter                               → order-counter",
                "  counter.Next()                        → 2",
                "  counter.Next() == 0                   → false",
                "  counter.Next()                        → 3",
                $"  at Tests/Rewrite.cs:{LineOf("counter.Next() + counter.Next()")}"),
            failure.Message.Replace("Sample.Counter", "order-counter"));
    }

    [Fact]
    public void Rule_1_a_getter_that_throws_on_a_second_read_is_read_once()
    {
        var order = NewOrder();

        var failure = Fail("GetterReadOnce", order);

        Assert.Equal(1, order.GetType().GetField("TotalReads")!.GetValue(order));
        Assert.Contains("\n  order.Total → 30\n", failure.Message);
    }

    [Fact]
    public void Rule_2_a_passing_check_does_not_allocate()
    {
        var passes = Cases.Value.GetMethod("Passes__Verified")!.CreateDelegate<Action<int>>();
        passes(1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            passes(i);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Contains("int __vg0_0 = default!;", CopyText("Passes"));
    }

    [Fact]
    public void Rule_3_constants_and_literals_are_not_captured()
    {
        var failure = Fail("Constants", 2);

        Assert.Equal(
            Lines("Verify failed: x > Limit + 1", "  x → 2", $"  at Tests/Rewrite.cs:{LineOf("x > Limit + 1")}"),
            failure.Message);
    }

    [Theory]
    [InlineData("MutationByIncrement", "x++ == x", new[] { "  x++ → 1", "  x   → 2" })]
    [InlineData("MutationByRef", "x == Bump(ref x) && x == 3", new[] { "  x == Bump(ref x) → false", "  x                → 1", "  Bump(ref x)      → 2", "  x == 3           → <not evaluated>" })]
    [InlineData("MutationByClosure", "x + bump() == x", new[] { "  x + bump() → 3", "  x          → 1", "  bump()     → 2", "  x          → 2" })]
    public void Rule_4_reads_separated_by_a_change_keep_their_own_values(string method, string condition, string[] rows)
    {
        var failure = Fail(method);

        Assert.Equal(Lines([$"Verify failed: {condition}", .. rows, $"  at Tests/Rewrite.cs:{LineOf(condition)}"]), failure.Message);
    }

    [Theory]
    [InlineData("SkippedAnd", null, new[] { "  s != null    → false", "  s            → null", "  s.Length > 3 → <not evaluated>", "  s.Length     → <not evaluated>" })]
    [InlineData("SkippedOr", 0, new[] { "  x == 0 || Explode() → true", "  x == 0              → true", "  x                   → 0", "  Explode()           → <not evaluated>" })]
    [InlineData("SkippedConditional", true, new[] { "  flag      → true", "  No()      → false", "  Explode() → <not evaluated>" })]
    [InlineData("SkippedCoalesce", "ab", new[] { "  (name ?? Fallback()).Length → 2", "  name ?? Fallback()          → \"ab\"", "  name                        → \"ab\"", "  Fallback()                  → <not evaluated>" })]
    public void Rule_5_the_skipped_side_is_not_evaluated(string method, object? argument, string[] rows)
    {
        var failure = Fail(method, argument);

        Assert.Equal(rows, failure.Message.Split('\n')[1..^1]);
    }

    [Fact]
    public void Rule_6_an_out_variable_stays_in_scope_after_the_call()
    {
        Assert.Equal(12, Invoke("OutVarScope", "12"));
        Assert.StartsWith("Verify failed: int.TryParse(text, out var number)\n  text → \"x\"\n", Fail("OutVarScope", "x").Message);
    }

    [Fact]
    public void Rule_6_an_embedded_statement_is_wrapped_in_braces()
    {
        Invoke("Embedded", false, 0);

        Assert.StartsWith("Verify failed: x > 1\n  x → 0\n", Fail("Embedded", true, 0).Message);
    }

    [Fact]
    public async Task Rule_7_await_stays_in_the_condition()
    {
        var task = (Task)Cases.Value.GetMethod("Awaited__Verified")!.Invoke(null, null)!;

        var failure = await Assert.ThrowsAsync<VerifyException>(() => task);

        Assert.Equal(
            Lines("Verify failed: await Task.FromResult(2) == 3", "  await Task.FromResult(2) → 2", "  Task.FromResult(2)       → System.Threading.Tasks.Task`1[System.Int32]", $"  at Tests/Rewrite.cs:{LineOf("await Task.FromResult(2) == 3")}"),
            failure.Message);
    }

    [Fact]
    public void Rule_8_property_pattern_members_are_not_read_again_and_skipped_members_stay_unevaluated()
    {
        var order = NewOrder();

        var failure = Fail("PropertyPattern", order);

        Assert.Equal(1, order.GetType().GetField("TotalReads")!.GetValue(order));
        Assert.Equal(0, order.GetType().GetField("NameReads")!.GetValue(order));
        Assert.Equal(["  order       → order", "  order.Total → <unavailable>", "  order.Name  → <unavailable>"], failure.Message.Split('\n')[1..^1]);
    }

    [Fact]
    public void Rule_9_a_ref_struct_is_not_captured()
    {
        var failure = Fail("RefStruct", new[] { 1, 2 });

        Assert.Equal(["  values.AsSpan().Length → 2", "  values.AsSpan()        → <unavailable>", "  values                 → System.Int32[]"], failure.Message.Split('\n')[1..^1]);
    }

    [Fact]
    public void Rule_10_a_mutable_struct_receiver_is_not_copied()
    {
        var shared = Cases.Value.GetField("Shared")!;

        var failure = Fail("MutableReceiver");

        Assert.Equal(1, shared.FieldType.GetField("Value")!.GetValue(shared.GetValue(null)));
        Assert.Equal(["  Shared.Next() → 1", "  Shared        → <unavailable>"], failure.Message.Split('\n')[1..^1]);
    }

    [Fact]
    public void Rule_11_a_part_whose_type_has_no_name_is_declared_from_the_part()
    {
        var failure = Fail("Anonymous");

        Assert.Equal(["  item.Name → \"a\"", "  item      → { Name = a }"], failure.Message.Split('\n')[1..^1]);
        Assert.Contains("= false ? (item) : default;", CopyText("Anonymous"));
    }

    [Fact]
    public void Rule_11_a_part_with_no_type_name_that_declares_a_variable_is_text_only()
    {
        var failure = Fail("AnonymousWithDeclaration");

        Assert.Equal(
            [
                "  Pass(item, out var count).Name == \"b\" → false",
                "  Pass(item, out var count).Name        → \"a\"",
                "  Pass(item, out var count)             → <unavailable>",
                "  item                                  → { Name = a }",
                "  count == 1                            → <not evaluated>",
                "  count                                 → <not evaluated>",
            ],
            failure.Message.Split('\n')[1..^1]);
    }

    [Fact]
    public void A_copied_call_passes_the_original_name_to_CallerMemberName()
    {
        Assert.Equal("CallerName", Invoke("CallerName"));
    }

    [Fact]
    public void A_copy_binds_with_the_using_aliases_of_its_file()
    {
        Assert.StartsWith("Verify failed: text.Length == 3\n  text.Length → 2\n", Fail("UsesAlias").Message);
    }

    [Fact]
    public void A_copy_calls_extension_methods_of_its_file()
    {
        Assert.StartsWith("Verify failed: items.Second() == 5\n  items.Second() → 2\n", Fail("UsesExtension", new List<int> { 1, 2 }).Message);
    }

    [Fact]
    public void A_direct_call_to_an_extension_method_is_sent_to_its_copy()
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Cases.Value.GetMethod("CallsExtension")!.Invoke(null, [new List<int> { 1 }]));

        Assert.StartsWith("Verify failed: items.Count == count\n  items.Count → 1\n  items       → System.Collections.Generic.List`1[System.Int32]\n  count       → 3\n", thrown.InnerException!.Message);
    }

    [Fact]
    public void Check_failures_are_reported_when_the_scope_ends()
    {
        var checks = Cases.Value.GetMethod("Checks__Verified")!;

        var scope = VerifyScope.Begin();
        checks.Invoke(null, [0]);
        var failure = Assert.Throws<VerifyException>(scope.Dispose);

        Assert.Equal(
            Lines(
                "2 checks failed",
                "",
                "Check failed: x > 1",
                "  x → 0",
                $"  at Tests/Rewrite.cs:{LineOf("Check(x > 1)")}",
                "",
                "Check failed: x > 2",
                "  x → 0",
                $"  at Tests/Rewrite.cs:{LineOf("Check(x > 2)")}"),
            failure.Message);
    }

    [Fact]
    public void Because_adds_its_reason()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  x → 2", "  because the discount applies", $"  at Tests/Rewrite.cs:{LineOf("x == 1).Because")}"),
            Fail("WithReason", 2).Message);
    }

    [Fact]
    public void An_interpolated_message_is_added()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  x → 2", "  x is 2", $"  at Tests/Rewrite.cs:{LineOf("x == 1, $\"x is {x}\"")}"),
            Fail("WithMessage", 2).Message);
    }

    [Fact]
    public void A_call_in_a_lambda_keeps_its_tier_2_message()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  expected x to equal 1", $"  at Tests/Rewrite.cs:{LineOf("() => Verify(x == 1)")}"),
            Fail("InLambda", 2).Message);
    }

    [Fact]
    public void Throws_in_a_copy_keeps_its_tier_2_message()
    {
        Assert.Equal(
            Lines("Throws failed: () => { }", "  expected () => { } to throw InvalidOperationException", "  no exception was thrown", $"  at Tests/Rewrite.cs:{LineOf("Throws<InvalidOperationException>(() => { })")}"),
            Fail("ThrowsInCopy").Message);
    }

    [Fact]
    public void A_direct_call_to_a_Verify_method_is_sent_to_its_copy()
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Cases.Value.GetMethod("CallsHelper")!.Invoke(null, [2]));

        Assert.Equal(
            Lines("Verify failed: x == 1", "  x → 2", $"  at Tests/Rewrite.cs:{LineOf("Helper(int x) => Verify(x == 1)")}"),
            thrown.InnerException!.Message);
    }

    [Fact]
    public void A_call_in_a_copy_to_a_Verify_method_calls_its_copy()
    {
        Assert.StartsWith("Verify failed: x == 1\n  x → 2\n", Fail("HelperFromCopy", 2).Message);
        Assert.Contains("Helper__Verified(x)", CopyText("HelperFromCopy"));
    }

    [Fact]
    public void The_rewritten_check_maps_to_its_original_line()
    {
        var copy = CopyTree("Checks");
        var statements = copy.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<IfStatementSyntax>().ToArray();

        var mapped = statements.Select(statement => statement.GetLocation().GetMappedLineSpan()).ToArray();

        Assert.All(mapped, span => Assert.Equal(Path, span.Path));
        Assert.Equal([LineOf("Check(x > 1)"), LineOf("Check(x > 2)")], mapped.Select(span => span.StartLinePosition.Line + 1));
    }

    [Fact]
    public void The_original_method_still_gives_tier_2_messages()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  expected x to equal 1", $"  at Tests/Rewrite.cs:{LineOf("Helper(int x) => Verify(x == 1)")}"),
            Fail("Helper", 2, copy: false).Message);
    }

    [Fact]
    public void With_rewriting_off_no_copy_or_redirect_is_written()
    {
        var result = GeneratorHarness.Generate(GeneratorHarness.Compilation((Path, Source)), new VerificationGlueGenerator(), out _);

        Assert.Equal(["Rewrite."], result.GeneratedTrees.Select(tree => System.IO.Path.GetFileName(tree.FilePath)[..8]).Distinct());
        Assert.All(result.GeneratedTrees, tree => Assert.EndsWith(".Interceptors.g.cs", tree.FilePath));
    }

    [Fact]
    public void A_method_whose_file_declares_a_file_type_is_not_rewritten()
    {
        var source = """
            using VerificationGlue;
            using static VerificationGlue.VerifySyntax;

            file sealed class Hidden { }

            public static partial class Local
            {
                [Verify]
                public static void M(int x) => Verify(x == 1);
            }
            """;

        var type = GeneratorHarness.Load(rewrite: true, ("/repo/Project/Local.cs", source)).GetType("Local")!;

        Assert.Null(type.GetMethod("M__Verified"));
    }

    [Fact]
    public void A_method_with_syntax_errors_is_not_copied()
    {
        var source = """
            using VerificationGlue;
            using static VerificationGlue.VerifySyntax;

            public static partial class Broken
            {
                [Verify]
                public static void M(int x) { Verify(x == 1) }
            }
            """;

        var result = GeneratorHarness.Generate(GeneratorHarness.Compilation(("/repo/Project/Broken.cs", source)), new VerificationGlueGenerator(), out _, rewrite: true);

        Assert.DoesNotContain(result.GeneratedTrees, tree => tree.FilePath.EndsWith(".Verified.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void A_call_in_a_method_the_runner_calls_through_its_copy_gets_no_interceptor()
    {
        var source = """
            using VerificationGlue;
            using static VerificationGlue.VerifySyntax;

            [VerifyRewrite]
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class RunnerTestAttribute : System.Attribute { }

            public partial class RunnerTests
            {
                [RunnerTest]
                public void Test(int x) => Verify(x == 1);

                public void NotATest(int x) => Verify(x == 1);
            }
            """;
        var compilation = GeneratorHarness.Compilation(("/repo/Project/RunnerTests.cs", source));

        var on = GeneratorHarness.Generate(compilation, new VerificationGlueGenerator(), out _, rewrite: true);
        var off = GeneratorHarness.Generate(compilation, new VerificationGlueGenerator(), out _);

        Assert.Equal(1, Count(on, "[global::System.Runtime.CompilerServices.InterceptsLocation("));
        Assert.Equal(1, on.GeneratedTrees.Count(tree => tree.FilePath.EndsWith(".Verified.g.cs", StringComparison.Ordinal)));
        Assert.Equal(2, Count(off, "[global::System.Runtime.CompilerServices.InterceptsLocation("));

        static int Count(GeneratorDriverRunResult result, string text) =>
            result.GeneratedTrees.Where(tree => tree.FilePath.EndsWith(".Interceptors.g.cs", StringComparison.Ordinal))
                .Sum(tree => tree.ToString().Split(text).Length - 1);
    }

    private static object NewOrder() => Activator.CreateInstance(Cases.Value.Assembly.GetType("Sample.Order")!)!;

    private static object? Invoke(string method, params object?[] arguments) =>
        Cases.Value.GetMethod(method + "__Verified")!.Invoke(null, arguments);

    private static VerifyException Fail(string method, params object?[] arguments) => Fail(method, arguments, copy: true);

    private static VerifyException Fail(string method, object? argument, object? second = null, bool copy = true) =>
        Fail(method, second is null ? [argument] : [argument, second], copy);

    private static VerifyException Fail(string method, object?[] arguments, bool copy)
    {
        var target = Cases.Value.GetMethod(copy ? method + "__Verified" : method)!;
        var parameters = target.GetParameters().Length;
        var thrown = Assert.Throws<TargetInvocationException>(() => target.Invoke(null, arguments.Take(parameters).ToArray()));
        return Assert.IsType<VerifyException>(thrown.InnerException);
    }

    private static SyntaxTree CopyTree(string method)
    {
        var result = GeneratorHarness.Generate(GeneratorHarness.Compilation((Path, Source)), new VerificationGlueGenerator(), out _, rewrite: true);
        return result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith($".Cases.{method}.{LineOfDeclaration(method)}.Verified.g.cs", StringComparison.Ordinal));
    }

    private static string CopyText(string method) => CopyTree(method).ToString();

    private static int LineOfDeclaration(string method) =>
        Array.FindIndex(Source.Split('\n'), line => line.Contains($" {method}(", StringComparison.Ordinal) && line.Contains("public static", StringComparison.Ordinal)) + 1;

    private static int LineOf(string text) => Array.FindIndex(Source.Split('\n'), line => line.Contains(text, StringComparison.Ordinal)) + 1;

    private static string Lines(params string[] lines) => string.Join("\n", lines);
}
