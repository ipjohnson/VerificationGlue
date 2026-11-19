using System.Reflection;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Tier 3: a failed count condition, <c>Any</c> or <c>Contains</c> lists the items, and a failed
/// <c>All</c> names the first item that fails.
/// </summary>
public class CountSpecs
{
    private const string Path = "/repo/Project/Tests/Counts.cs";

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Sample;

        public static partial class Cases
        {
            public static int Runs;
            public static int AllCalls;
            public static int ThrowCalls;
            public static int AnyCalls;

            [Verify]
            public static void ListCount(List<int> items) => Verify(items.Count == 3);

            [Verify]
            public static void ArrayLength(string[] names) => Verify(2 < names.Length);

            [Verify]
            public static void CountCall(IEnumerable<int> items) => Verify(items.Count() > 0);

            [Verify]
            public static void StaticCount(IEnumerable<int> items) => Verify(Enumerable.Count(items) == 1);

            [Verify]
            public static void BothCounts(List<int> left, int[] right) => Verify(left.Count == right.Length);

            [Verify]
            public static void LazyCount(int count) => Verify(Numbers(count).Count() == 0);

            [Verify]
            public static void StringLength(string name) => Verify(name.Length == 3);

            [Verify]
            public static void DictionaryCount(Dictionary<string, int> stock) => Verify(stock.Count == 0);

            [Verify]
            public static void AllPositive(int[] values) => Verify(values.All(value => value > 0));

            [Verify]
            public static void AllCounted(int[] values) => Verify(values.All(value => { AllCalls++; return value > 0; }));

            [Verify]
            public static void AllStatic(List<string> names) => Verify(Enumerable.All(names, name => name.Length < 4));

            [Verify]
            public static void AllMethodGroup(int[] values) => Verify(values.All(IsEven));

            [Verify]
            public static void AllDelegate(int[] values, Func<int, bool> rule) => Verify(values.All(rule));

            [Verify]
            public static void AllThrows(int[] values) => Verify(values.All(value => ++ThrowCalls < 3 ? value < 2 : Throw()));

            [Verify]
            public static void AllDeclared(int[] values) => Verify(Limit(values, out var max).All(value => value < max));

            [Verify]
            public static void AllLazy(int count) => Verify(Numbers(count).All(value => value < 2));

            [Verify]
            public static void AnyItem(List<int> items) => Verify(items.Any());

            [Verify]
            public static void AnyMatch(int[] values) => Verify(values.Any(value => { AnyCalls++; return value > 10; }));

            [Verify]
            public static void ListContains(List<string> tags, string tag) => Verify(tags.Contains(tag));

            [Verify]
            public static void ArrayContains(int[] values, int value) => Verify(values.Contains(value));

            [Verify]
            public static void StaticContains(IEnumerable<int> values, int value) => Verify(Enumerable.Contains(values, value));

            [Verify]
            public static void SetContains(HashSet<string> tags) => Verify(tags.Contains("gift"));

            [Verify]
            public static void IdContains(HashSet<int> ids) => Verify(ids.Contains(7));

            [Verify]
            public static void StringContains(string text) => Verify(text.Contains('x'));

            [Verify]
            public static void Checked(List<int> items)
            {
                using (VerifyScope.Begin())
                {
                    Check(items.Count == 3);
                }
            }

            private static bool IsEven(int value) => value % 2 == 0;

            private static bool Throw() => throw new InvalidOperationException();

            private static int[] Limit(int[] values, out int max)
            {
                max = 2;
                return values;
            }

            private static IEnumerable<int> Numbers(int count)
            {
                Runs++;
                for (var number = 1; number <= count; number++)
                {
                    yield return number;
                }
            }
        }
        """;

    private static readonly Lazy<Type> Cases = new(() => GeneratorHarness.Load(rewrite: true, (Path, Source)).GetType("Sample.Cases", throwOnError: true)!);

    [VerifyTest]
    public void A_failed_count_lists_the_items()
    {
        Assert.Equal(
            Lines(
                "Verify failed: items.Count == 3",
                "  items.Count → 2",
                "  items       → 2 items: 10, 20",
                $"  at Tests/Counts.cs:{LineOf("ListCount")}"),
            Fail("ListCount", new List<int> { 10, 20 }).Message);
    }

    [VerifyTest]
    public void An_empty_collection_shows_no_items()
    {
        Assert.Contains("\n  items       → 0 items\n", Fail("ListCount", new List<int>()).Message);
        Assert.Contains("\n  names        → 0 items\n", Fail("ArrayLength", new object[] { Array.Empty<string>() }).Message);
        Assert.Contains("\n  items → 0 items\n", Fail("AnyItem", new List<int>()).Message);
    }

    [VerifyTest]
    public void Items_stop_after_ten()
    {
        Assert.Contains(
            "\n  items       → 12 items: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, …\n",
            Fail("ListCount", Enumerable.Range(1, 12).ToList()).Message);
        Assert.Contains(
            "\n  items       → 10 items: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10\n",
            Fail("ListCount", Enumerable.Range(1, 10).ToList()).Message);
    }

    [VerifyTest]
    public void Length_count_call_and_the_static_form_list_the_items()
    {
        Assert.Contains("\n  names        → 1 item: \"Ann\"\n", Fail("ArrayLength", new object[] { new[] { "Ann" } }).Message);
        Assert.Contains("→ 0 items\n", Fail("CountCall", new List<int>()).Message);
        Assert.Contains("→ 2 items: 4, 5\n", Fail("StaticCount", new[] { 4, 5 }).Message);
    }

    [VerifyTest]
    public void A_count_on_each_side_lists_both()
    {
        var message = Fail("BothCounts", new List<int> { 1 }, new[] { 1, 2 }).Message;

        Assert.Contains("\n  left         → 1 item: 1\n", message);
        Assert.Contains("\n  right        → 2 items: 1, 2\n", message);
    }

    [VerifyTest]
    public void A_set_of_values_lists_its_items()
    {
        Assert.Contains("\n  ids → 2 items: 1, 2\n", Fail("IdContains", new HashSet<int> { 1, 2 }).Message);
    }

    [VerifyTest]
    public void A_lazy_sequence_is_not_read_again_and_gives_no_lines()
    {
        var runs = (FieldInfo)Cases.Value.GetMember("Runs")[0];
        runs.SetValue(null, 0);

        Assert.DoesNotContain(" has ", Fail("LazyCount", 2).Message);
        Assert.DoesNotContain("first item that fails", Fail("AllLazy", 3).Message);
        Assert.Equal(2, runs.GetValue(null));
    }

    [VerifyTest]
    public void A_string_length_does_not_list_characters()
    {
        Assert.Equal(
            Lines(
                "Verify failed: name.Length == 3",
                "  name.Length → 2",
                "  name        → \"Bo\"",
                $"  at Tests/Counts.cs:{LineOf("StringLength")}"),
            Fail("StringLength", "Bo").Message);
        Assert.DoesNotContain(" has ", Fail("StringContains", "abc").Message);
    }

    [VerifyTest]
    public void Dictionary_entries_print_as_pairs()
    {
        Assert.Contains("\n  stock       → 1 item: \"gift\": 3\n", Fail("DictionaryCount", new Dictionary<string, int> { ["gift"] = 3 }).Message);
    }

    [VerifyTest]
    public void A_failed_all_names_the_first_item_that_fails()
    {
        Assert.Equal(
            Lines(
                "Verify failed: values.All(value => value > 0)",
                "  first item that fails at index 2: -1",
                "  values → 4 items: 4, 5, -1, -2",
                $"  at Tests/Counts.cs:{LineOf("AllPositive")}"),
            Fail("AllPositive", new[] { 4, 5, -1, -2 }).Message);
    }

    [VerifyTest]
    public void The_lambda_of_all_runs_again_only_when_the_check_fails()
    {
        var calls = (FieldInfo)Cases.Value.GetMember("AllCalls")[0];
        var passing = Cases.Value.GetMethod("AllCounted__Verified")!;

        calls.SetValue(null, 0);
        passing.Invoke(null, new object[] { new[] { 1, 2, 3 } });
        Assert.Equal(3, calls.GetValue(null));

        calls.SetValue(null, 0);
        Assert.Contains("\n  first item that fails at index 1: -1\n", Fail("AllCounted", new[] { 1, -1, 3 }).Message);
        Assert.Equal(4, calls.GetValue(null));
    }

    [VerifyTest]
    public void All_with_the_static_form_a_method_group_or_a_delegate_names_the_item()
    {
        Assert.Contains("\n  first item that fails at index 1: \"Grace\"\n", Fail("AllStatic", new List<string> { "Ada", "Grace" }).Message);
        Assert.Contains("\n  first item that fails at index 0: 3\n", Fail("AllMethodGroup", new[] { 3, 4 }).Message);
        Assert.Contains("\n  first item that fails at index 1: 7\n", Fail("AllDelegate", new[] { 1, 7 }, new Func<int, bool>(value => value < 5)).Message);
    }

    [VerifyTest]
    public void All_gives_no_line_when_the_lambda_throws_or_reads_a_variable_the_condition_declares()
    {
        var calls = (FieldInfo)Cases.Value.GetMember("ThrowCalls")[0];
        calls.SetValue(null, 0);

        Assert.Equal(
            Lines(
                "Verify failed: values.All(value => ++ThrowCalls < 3 ? value < 2 : Throw())",
                "  values → 2 items: 1, 5",
                $"  at Tests/Counts.cs:{LineOf("AllThrows")}"),
            Fail("AllThrows", new[] { 1, 5 }).Message);
        Assert.Equal(3, calls.GetValue(null));
        Assert.DoesNotContain("first item that fails", Fail("AllDeclared", new object[] { new[] { 1, 5 } }).Message);
    }

    [VerifyTest]
    public void A_failed_any_lists_the_items_without_running_the_lambda_again()
    {
        var calls = (FieldInfo)Cases.Value.GetMember("AnyCalls")[0];
        calls.SetValue(null, 0);

        Assert.Equal(
            Lines(
                "Verify failed: values.Any(value => { AnyCalls++; return value > 10; })",
                "  values → 3 items: 1, 2, 3",
                $"  at Tests/Counts.cs:{LineOf("AnyMatch")}"),
            Fail("AnyMatch", new[] { 1, 2, 3 }).Message);
        Assert.Equal(3, calls.GetValue(null));
    }

    [VerifyTest]
    public void A_failed_contains_lists_the_items()
    {
        Assert.Equal(
            Lines(
                "Verify failed: tags.Contains(tag)",
                "  tags → 2 items: \"gift\", \"fragile\"",
                "  tag  → \"express\"",
                $"  at Tests/Counts.cs:{LineOf("ListContains")}"),
            Fail("ListContains", new List<string> { "gift", "fragile" }, "express").Message);
        Assert.Contains("\n  values → 2 items: 1, 2\n", Fail("ArrayContains", new[] { 1, 2 }, 3).Message);
        Assert.Contains("\n  values → 0 items\n", Fail("StaticContains", new List<int>(), 3).Message);
        Assert.Contains("\n  tags → 1 item: \"fragile\"\n", Fail("SetContains", new HashSet<string> { "fragile" }).Message);
    }

    [VerifyTest]
    public void Check_lists_the_items()
    {
        Assert.Contains("→ 1 item: 4\n", Fail("Checked", new List<int> { 4 }).Message);
    }

    private static VerifyException Fail(string method, params object[] arguments)
    {
        var target = Cases.Value.GetMethod(method + "__Verified")!;
        var thrown = Assert.Throws<TargetInvocationException>(() => target.Invoke(null, arguments));
        return Assert.IsType<VerifyException>(thrown.InnerException);
    }

    private static int LineOf(string method) =>
        Array.FindIndex(Source.Split('\n'), line => line.Contains($" {method}(", StringComparison.Ordinal)) + 1;

    private static string Lines(params string[] lines) => string.Join("\n", lines);
}
