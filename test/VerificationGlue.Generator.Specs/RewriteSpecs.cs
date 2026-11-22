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

        public sealed class Noisy
        {
            public int Formats;

            public bool Ready => false;

            public override string ToString()
            {
                Formats++;
                throw new FormatException("noisy");
            }
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
            public static unsafe void Pointer(int value)
            {
                int* p = &value;
                Verify(p == null);
            }

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
            public static void MessageHoles(int x, Counter counter) => Verify(x == 1, $"call {counter.Next()}");

            private const string Reason = "x is wrong";

            [Verify]
            public static void PlainMessage(int x) => Verify(x == 1, "x is wrong");

            [Verify]
            public static void ConstMessage(int x) => Verify(x == 1, Reason);

            [Verify]
            public static void LocalMessage(int x)
            {
                var reason = "x is wrong";
                Verify(x == 1, reason);
            }

            [Verify]
            public static void CheckPlainMessage(int x) => Check(x == 1, "x is wrong");

            [Verify]
            public static void PlainMessageInLambda(int x)
            {
                Action check = () => Verify(x == 1, "x is wrong");
                check();
            }

            [Verify]
            public static void NoisyValue(Noisy noisy) => Verify(noisy.Ready);

            [Verify]
            public static void NoisyHole(int x, Noisy noisy) => Verify(x == 1, $"noisy is {noisy}");

            [Verify]
            public static void InLambda(int x)
            {
                Action check = () => Verify(x == 1);
                check();
            }

            [Verify]
            public static void InLocalFunction(int x)
            {
                void Run() => Verify(x == 1);
                Run();
            }

            [Verify]
            public static void ThrowsInCopy() => Throws<InvalidOperationException>(() => { });

            [Verify]
            public static void SingleInCopy(int count)
            {
                var items = new int[count];
                Single(items);
            }

            [Verify]
            public static void SingleMatchInCopy(int count)
            {
                var items = new int[count];
                Single(items, n => n > 1);
            }

            [Verify]
            public static void IsInCopy(object value) => Is<string>(value);

            [Verify]
            public static void NotNullInCopy(string? text)
            {
                var length = NotNull(text).Length;
            }

            [Verify]
            public static void NotNullValueInCopy(int? count)
            {
                int value = NotNull(count);
            }

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

    [VerifyTest]
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

    [VerifyTest]
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

    [VerifyTest]
    public void Rule_1_a_getter_that_throws_on_a_second_read_is_read_once()
    {
        var order = NewOrder();

        var failure = Fail("GetterReadOnce", order);

        Assert.Equal(1, order.GetType().GetField("TotalReads")!.GetValue(order));
        Assert.Contains("\n  order.Total → 30\n", failure.Message);
    }

    [VerifyTest]
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

    [VerifyTest]
    public void Rule_3_constants_and_literals_are_not_captured()
    {
        var failure = Fail("Constants", 2);

        Assert.Equal(
            Lines("Verify failed: x > Limit + 1", "  x → 2", $"  at Tests/Rewrite.cs:{LineOf("x > Limit + 1")}"),
            failure.Message);
    }

    [VerifyTest]
    [Row("MutationByIncrement", "x++ == x", new[] { "  x++ → 1", "  x   → 2" })]
    [Row("MutationByRef", "x == Bump(ref x) && x == 3", new[] { "  x == Bump(ref x) → false", "  x                → 1", "  Bump(ref x)      → 2", "  x == 3           → <not evaluated>" })]
    [Row("MutationByClosure", "x + bump() == x", new[] { "  x + bump() → 3", "  x          → 1", "  bump()     → 2", "  x          → 2" })]
    public void Rule_4_reads_separated_by_a_change_keep_their_own_values(string method, string condition, string[] rows)
    {
        var failure = Fail(method);

        Assert.Equal(Lines([$"Verify failed: {condition}", .. rows, $"  at Tests/Rewrite.cs:{LineOf(condition)}"]), failure.Message);
    }

    [VerifyTest]
    [Row("SkippedAnd", null, new[] { "  s != null    → false", "  s            → null", "  s.Length > 3 → <not evaluated>", "  s.Length     → <not evaluated>" })]
    [Row("SkippedOr", 0, new[] { "  x == 0 || Explode() → true", "  x == 0              → true", "  x                   → 0", "  Explode()           → <not evaluated>" })]
    [Row("SkippedConditional", true, new[] { "  flag      → true", "  No()      → false", "  Explode() → <not evaluated>" })]
    [Row("SkippedCoalesce", "ab", new[] { "  (name ?? Fallback()).Length → 2", "  name ?? Fallback()          → \"ab\"", "  name                        → \"ab\"", "  Fallback()                  → <not evaluated>" })]
    public void Rule_5_the_skipped_side_is_not_evaluated(string method, object? argument, string[] rows)
    {
        var failure = Fail(method, argument);

        Assert.Equal(rows, failure.Message.Split('\n')[1..^1]);
    }

    [VerifyTest]
    public void Rule_6_an_out_variable_stays_in_scope_after_the_call()
    {
        Assert.Equal(12, Invoke("OutVarScope", "12"));
        Assert.StartsWith("Verify failed: int.TryParse(text, out var number)\n  text → \"x\"\n", Fail("OutVarScope", "x").Message);
    }

    [VerifyTest]
    public void Rule_6_an_embedded_statement_is_wrapped_in_braces()
    {
        Invoke("Embedded", false, 0);

        Assert.StartsWith("Verify failed: x > 1\n  x → 0\n", Fail("Embedded", true, 0).Message);
    }

    [VerifyTest]
    public async Task Rule_7_await_stays_in_the_condition()
    {
        var task = (Task)Cases.Value.GetMethod("Awaited__Verified")!.Invoke(null, null)!;

        var failure = await Assert.ThrowsAsync<VerifyException>(() => task);

        Assert.Equal(
            Lines("Verify failed: await Task.FromResult(2) == 3", "  await Task.FromResult(2) → 2", "  Task.FromResult(2)       → System.Threading.Tasks.Task`1[System.Int32]", $"  at Tests/Rewrite.cs:{LineOf("await Task.FromResult(2) == 3")}"),
            failure.Message);
    }

    [VerifyTest]
    public void Rule_8_property_pattern_members_are_not_read_again_and_skipped_members_stay_unevaluated()
    {
        var order = NewOrder();

        var failure = Fail("PropertyPattern", order);

        Assert.Equal(1, order.GetType().GetField("TotalReads")!.GetValue(order));
        Assert.Equal(0, order.GetType().GetField("NameReads")!.GetValue(order));
        Assert.Equal(["  order       → order", "  order.Total → <unavailable>", "  order.Name  → <unavailable>"], failure.Message.Split('\n')[1..^1]);
    }

    [VerifyTest]
    public void Rule_9_a_ref_struct_is_not_captured()
    {
        var failure = Fail("RefStruct", new[] { 1, 2 });

        Assert.Equal(["  values.AsSpan().Length → 2", "  values.AsSpan()        → <unavailable>", "  values                 → 2 items: 1, 2"], failure.Message.Split('\n')[1..^1]);
    }

    [VerifyTest]
    public void Rule_9_a_pointer_is_not_captured()
    {
        var failure = Fail("Pointer", 1);

        Assert.Equal(["  p → <unavailable>"], failure.Message.Split('\n')[1..^1]);
    }

    [VerifyTest]
    public void Rule_10_a_mutable_struct_receiver_is_not_copied()
    {
        var shared = Cases.Value.GetField("Shared")!;

        var failure = Fail("MutableReceiver");

        Assert.Equal(1, shared.FieldType.GetField("Value")!.GetValue(shared.GetValue(null)));
        Assert.Equal(["  Shared.Next() → 1", "  Shared        → <unavailable>"], failure.Message.Split('\n')[1..^1]);
    }

    [VerifyTest]
    public void Rule_11_a_part_whose_type_has_no_name_is_declared_from_the_part()
    {
        var failure = Fail("Anonymous");

        Assert.Equal(["  item.Name → \"a\"", "  item      → { Name = a }"], failure.Message.Split('\n')[1..^1]);
        Assert.Contains("= false ? (item) : default;", CopyText("Anonymous"));
    }

    [VerifyTest]
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

    [VerifyTest]
    public void A_copied_call_passes_the_original_name_to_CallerMemberName()
    {
        Assert.Equal("CallerName", Invoke("CallerName"));
    }

    [VerifyTest]
    public void A_copy_binds_with_the_using_aliases_of_its_file()
    {
        Assert.StartsWith("Verify failed: text.Length == 3\n  text.Length → 2\n", Fail("UsesAlias").Message);
    }

    [VerifyTest]
    public void A_copy_calls_extension_methods_of_its_file()
    {
        Assert.StartsWith("Verify failed: items.Second() == 5\n  items.Second() → 2\n", Fail("UsesExtension", new List<int> { 1, 2 }).Message);
    }

    [VerifyTest]
    public void A_direct_call_to_an_extension_method_is_sent_to_its_copy()
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Cases.Value.GetMethod("CallsExtension")!.Invoke(null, [new List<int> { 1 }]));

        Assert.StartsWith("Verify failed: items.Count == count\n  items.Count → 1\n  items       → 1 item: 1\n  count       → 3\n", thrown.InnerException!.Message);
    }

    [VerifyTest]
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

    [VerifyTest]
    public void Because_adds_its_reason()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  x → 2", "  because the discount applies", $"  at Tests/Rewrite.cs:{LineOf("x == 1).Because")}"),
            Fail("WithReason", 2).Message);
    }

    [VerifyTest]
    public void An_interpolated_message_is_added()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  x → 2", "  x is 2", $"  at Tests/Rewrite.cs:{LineOf("x == 1, $\"x is {x}\"")}"),
            Fail("WithMessage", 2).Message);
    }

    [VerifyTest]
    [Row("PlainMessage", "x == 1, \"x is wrong\"")]
    [Row("ConstMessage", "x == 1, Reason")]
    [Row("LocalMessage", "x == 1, reason")]
    public void A_plain_string_message_is_added(string method, string call)
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  x → 2", "  x is wrong", $"  at Tests/Rewrite.cs:{LineOf(call)}"),
            Fail(method, 2).Message);
    }

    [VerifyTest]
    public void A_check_with_a_plain_string_message_adds_it()
    {
        var check = Cases.Value.GetMethod("CheckPlainMessage__Verified")!;

        var scope = VerifyScope.Begin();
        check.Invoke(null, [2]);
        var failure = Assert.Throws<VerifyException>(scope.Dispose);

        Assert.Equal(
            Lines("1 check failed", "", "Check failed: x == 1", "  x → 2", "  x is wrong", $"  at Tests/Rewrite.cs:{LineOf("Check(x == 1, \"x is wrong\")")}"),
            failure.Message);
    }

    [VerifyTest]
    public void A_plain_string_message_in_a_lambda_keeps_its_tier_2_message()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  expected x to equal 1", "  x is wrong", $"  at Tests/Rewrite.cs:{LineOf("() => Verify(x == 1, \"x is wrong\")")}"),
            Fail("PlainMessageInLambda", 2).Message);
    }

    [VerifyTest]
    public void Message_holes_run_only_on_failure()
    {
        var counter = Activator.CreateInstance(Cases.Value.Assembly.GetType("Sample.Counter")!)!;
        var calls = counter.GetType().GetField("Calls")!;

        Invoke("MessageHoles", 1, counter);
        Assert.Equal(0, calls.GetValue(counter));

        var failure = Fail("MessageHoles", 2, counter);

        Assert.Equal(1, calls.GetValue(counter));
        Assert.Equal(["  x → 2", "  call 1"], failure.Message.Split('\n')[1..^1]);
    }

    [VerifyTest]
    public void A_captured_value_whose_ToString_throws_shows_a_placeholder()
    {
        var noisy = Activator.CreateInstance(Cases.Value.Assembly.GetType("Sample.Noisy")!)!;

        var failure = Fail("NoisyValue", noisy);

        Assert.Equal(["  noisy → <ToString threw FormatException>"], failure.Message.Split('\n')[1..^1]);
        Assert.Equal(1, noisy.GetType().GetField("Formats")!.GetValue(noisy));
    }

    [VerifyTest]
    public void A_message_hole_whose_ToString_throws_shows_a_placeholder()
    {
        var noisy = Activator.CreateInstance(Cases.Value.Assembly.GetType("Sample.Noisy")!)!;

        Invoke("NoisyHole", 1, noisy);
        var failure = Fail("NoisyHole", 2, noisy);

        Assert.Equal(["  x → 2", "  noisy is <ToString threw FormatException>"], failure.Message.Split('\n')[1..^1]);
        Assert.Equal(1, noisy.GetType().GetField("Formats")!.GetValue(noisy));
    }

    [VerifyTest]
    public void A_call_in_a_lambda_keeps_its_tier_2_message()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  expected x to equal 1", $"  at Tests/Rewrite.cs:{LineOf("() => Verify(x == 1)")}"),
            Fail("InLambda", 2).Message);
    }

    [VerifyTest]
    public void A_call_in_a_local_function_keeps_its_tier_2_message()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  expected x to equal 1", $"  at Tests/Rewrite.cs:{LineOf("void Run() => Verify(x == 1)")}"),
            Fail("InLocalFunction", 2).Message);
    }

    [VerifyTest]
    public void Throws_in_a_copy_keeps_its_tier_2_message()
    {
        Assert.Equal(
            Lines("Throws failed: () => { }", "  expected () => { } to throw InvalidOperationException", "  no exception was thrown", $"  at Tests/Rewrite.cs:{LineOf("Throws<InvalidOperationException>(() => { })")}"),
            Fail("ThrowsInCopy").Message);
    }

    public static IEnumerable<(string Method, object? Argument, string Call, string Expected)> ValueChecksInCopies =>
    [
        ("SingleInCopy", 2, "Single(items);", "Single failed: items\n  expected one item, but found 2: [0, 0]"),
        ("SingleMatchInCopy", 1,"Single(items, n => n > 1)", "Single failed: items, n => n > 1\n  expected one match, but found 0"),
        ("IsInCopy", 3, "Is<string>(value)", "Is failed: value\n  expected String, but it was Int32"),
        ("NotNullInCopy", null, "NotNull(text)", "NotNull failed: text"),
        ("NotNullValueInCopy", null, "NotNull(count)", "NotNull failed: count"),
    ];

    [VerifyTest]
    [RowSource(nameof(ValueChecksInCopies))]
    public void Single_Is_and_NotNull_in_a_copy_keep_their_tier_2_message(string method, object? argument, string call, string expected)
    {
        Assert.Equal(expected + $"\n  at Tests/Rewrite.cs:{LineOf(call)}", Fail(method, [argument], copy: true).Message);
    }

    [VerifyTest]
    public void A_direct_call_to_a_Verify_method_is_sent_to_its_copy()
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Cases.Value.GetMethod("CallsHelper")!.Invoke(null, [2]));

        Assert.Equal(
            Lines("Verify failed: x == 1", "  x → 2", $"  at Tests/Rewrite.cs:{LineOf("Helper(int x) => Verify(x == 1)")}"),
            thrown.InnerException!.Message);
    }

    [VerifyTest]
    [Row("Checks", "FromOwnType")]
    [Row("OtherTests", "FromOtherType")]
    public void A_direct_call_to_a_Verify_method_named_like_a_VerifySyntax_method_is_sent_to_its_copy(string type, string method)
    {
        var assembly = GeneratorHarness.Load(rewrite: true, ("/repo/Project/Named.cs", NamedLikeVerifySyntax));

        var thrown = Assert.Throws<TargetInvocationException>(() => assembly.GetType(type)!.GetMethod(method)!.Invoke(null, [4]));

        Assert.StartsWith("Verify failed: width == 5\n  width → 4\n", thrown.InnerException!.Message);
    }

    [VerifyTest]
    public void A_call_to_a_VerifySyntax_method_gets_no_redirect()
    {
        var result = GeneratorHarness.Generate(GeneratorHarness.Compilation(("/repo/Project/Named.cs", NamedLikeVerifySyntax)), new VerificationGlueGenerator(), out _, rewrite: true);

        var redirects = result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith(".Redirects.g.cs", StringComparison.Ordinal)).ToString();
        Assert.Equal(2, redirects.Split("=> global::Checks.Check__Verified(").Length - 1);
        Assert.Equal(2, redirects.Split("[global::System.Runtime.CompilerServices.InterceptsLocation(").Length - 1);
    }

    private const string NamedLikeVerifySyntax = """
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        public static partial class Checks
        {
            [Verify]
            public static void Check(int width) => Verify(width == 5);

            public static void FromOwnType(int width) => Check(width);

            public static void CallsVerifySyntax(int width) => VerifySyntax.Check(width == 5);
        }

        public static class OtherTests
        {
            public static void FromOtherType(int width) => Checks.Check(width);
        }
        """;

    [VerifyTest]
    public void A_call_in_a_copy_to_a_Verify_method_calls_its_copy()
    {
        Assert.StartsWith("Verify failed: x == 1\n  x → 2\n", Fail("HelperFromCopy", 2).Message);
        Assert.Contains("Helper__Verified(x)", CopyText("HelperFromCopy"));
    }

    [VerifyTest]
    public void The_rewritten_check_maps_to_its_original_line(CancellationToken cancellationToken)
    {
        var copy = CopyTree("Checks");
        var statements = copy.GetRoot(cancellationToken).DescendantNodes().OfType<IfStatementSyntax>().ToArray();

        var mapped = statements.Select(statement => statement.GetLocation().GetMappedLineSpan()).ToArray();

        Assert.All(mapped, span => Assert.Equal(Path, span.Path));
        Assert.Equal([LineOf("Check(x > 1)"), LineOf("Check(x > 2)")], mapped.Select(span => span.StartLinePosition.Line + 1));
    }

    [VerifyTest]
    public void The_original_method_still_gives_tier_2_messages()
    {
        Assert.Equal(
            Lines("Verify failed: x == 1", "  expected x to equal 1", $"  at Tests/Rewrite.cs:{LineOf("Helper(int x) => Verify(x == 1)")}"),
            Fail("Helper", 2, copy: false).Message);
    }

    [VerifyTest]
    public void With_rewriting_off_no_copy_or_redirect_is_written()
    {
        var result = GeneratorHarness.Generate(GeneratorHarness.Compilation((Path, Source)), new VerificationGlueGenerator(), out _);

        Assert.Equal(["Rewrite."], result.GeneratedTrees.Select(tree => System.IO.Path.GetFileName(tree.FilePath)[..8]).Distinct());
        Assert.All(result.GeneratedTrees, tree => Assert.EndsWith(".Interceptors.g.cs", tree.FilePath));
    }

    [VerifyTest]
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

    [VerifyTest]
    [Row("generic", "public static partial class T { [Verify] public static void M<TItem>(int x) => Verify(x == 1); }")]
    [Row("virtual", "public partial class T { [Verify] public virtual void M(int x) => Verify(x == 1); }")]
    [Row("abstract", "public abstract partial class T { [Verify] public abstract void M(int x); }")]
    [Row("override", "public class Base { public virtual void M(int x) { } } public partial class T : Base { [Verify] public override void M(int x) => Verify(x == 1); }")]
    [Row("extern", "public static partial class T { [Verify] public static extern void M(int x); }")]
    [Row("returns by reference", "public static partial class T { private static int _value; [Verify] public static ref int M(int x) { Verify(x == 1); return ref _value; } }")]
    [Row("instance method of a struct", "public partial struct T { [Verify] public void M(int x) => Verify(x == 1); }")]
    [Row("#if directive", "public static partial class T { [Verify] public static void M(int x)\n{\nVerify(x == 1);\n#if DEBUG\n#endif\n}\n}")]
    [Row("in a generic type", "public static partial class T<TItem> { [Verify] public static void M(int x) => Verify(x == 1); }")]
    [Row("in a private type", "public static partial class Outer { private static partial class T { [Verify] public static void M(int x) => Verify(x == 1); } }")]
    [Row("in a protected type", "public partial class Outer { protected static partial class T { [Verify] public static void M(int x) => Verify(x == 1); } }")]
    [Row("private nested type in the signature", "public static partial class T { private sealed class Hidden { } [Verify] private static void M(int x, Hidden hidden) => Verify(x == 1); }")]
    [Row("pointer type in the signature", "public static unsafe partial class T { [Verify] public static void M(int x, int* p) => Verify(x == 1); }")]
    [Row("dynamic type in the signature", "public static partial class T { [Verify] public static void M(int x, dynamic d) => Verify(x == 1); }")]
    public void A_method_that_breaks_an_eligibility_rule_is_not_rewritten_and_keeps_tier_2(string rule, string declaration)
    {
        const string Control = "public static partial class Control { [Verify] public static void M(int x) => Verify(x == 1); }";
        var source = "using VerificationGlue;\nusing static VerificationGlue.VerifySyntax;\n\n" + declaration + "\n" + Control;
        var calls = declaration.Split("Verify(x == 1)").Length;

        var result = GeneratorHarness.Generate(GeneratorHarness.Compilation(("/repo/Project/Rule.cs", source)), new VerificationGlueGenerator(), out _, rewrite: true);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(GeneratorHarness.Analyze(source, rewrite: true));
        var copy = Assert.Single(result.GeneratedTrees, tree => tree.FilePath.EndsWith(".Verified.g.cs", StringComparison.Ordinal));
        Assert.Contains("Control", copy.FilePath, StringComparison.Ordinal);
        Assert.Equal(
            calls,
            result.GeneratedTrees.Where(tree => tree.FilePath.EndsWith(".Interceptors.g.cs", StringComparison.Ordinal))
                .Sum(tree => tree.ToString().Split("[global::System.Runtime.CompilerServices.InterceptsLocation(").Length - 1));
    }

    [VerifyTest]
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

    [VerifyTest]
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
