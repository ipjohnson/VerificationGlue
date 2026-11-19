using System.Reflection;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Tier 3: a failed <c>SequenceEqual</c> names the first index that differs, both lengths and the
/// items past the shorter side's end, and a failed <c>SetEquals</c> names the missing and extra items.
/// </summary>
public class CollectionSpecs
{
    private const string Path = "/repo/Project/Tests/Collections.cs";

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

            [Verify]
            public static void Arrays(int[] actual, int[] expected) => Verify(actual.SequenceEqual(expected));

            [Verify]
            public static void Lists(List<string> actual, List<string> expected) => Verify(actual.SequenceEqual(expected));

            [Verify]
            public static void Static(IEnumerable<int> actual, IEnumerable<int> expected) => Verify(Enumerable.SequenceEqual(actual, expected));

            [Verify]
            public static void Lazy(int count, int[] expected) => Verify(Numbers(count).SequenceEqual(expected));

            [Verify]
            public static void Comparer(string[] actual, string[] expected) => Verify(actual.SequenceEqual(expected, StringComparer.Ordinal));

            [Verify]
            public static void Checked(int[] actual, int[] expected)
            {
                using (VerifyScope.Begin())
                {
                    Check(actual.SequenceEqual(expected));
                }
            }

            [Verify]
            public static void Sets(HashSet<string> tags, string[] expected) => Verify(tags.SetEquals(expected));

            [Verify]
            public static void Interface(ISet<int> numbers, List<int> expected) => Verify(numbers.SetEquals(expected));

            [Verify]
            public static void LazySet(HashSet<int> numbers, int count) => Verify(numbers.SetEquals(Numbers(count)));

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
    public void Equal_lengths_with_one_differing_item_name_the_index_and_both_items()
    {
        Assert.Equal(
            Lines(
                "Verify failed: actual.SequenceEqual(expected)",
                "  first difference at index 2: actual has 3, expected has 4",
                "  actual has 4 items, expected has 4",
                "  actual   → System.Int32[]",
                "  expected → System.Int32[]",
                $"  at Tests/Collections.cs:{LineOf("Arrays")}"),
            Fail("Arrays", new[] { 1, 2, 3, 4 }, new[] { 1, 2, 4, 4 }).Message);
    }

    [VerifyTest]
    public void A_longer_right_side_lists_the_items_past_the_end_of_the_left()
    {
        Assert.Contains(
            Lines(
                "",
                "  first difference at index 2: actual has no item, expected has 3",
                "  actual has 2 items, expected has 4",
                "  expected has 2 items past the end of actual: 3, 4",
                ""),
            Fail("Arrays", new[] { 1, 2 }, new[] { 1, 2, 3, 4 }).Message);
    }

    [VerifyTest]
    public void A_longer_left_side_lists_the_items_past_the_end_of_the_right()
    {
        Assert.Contains(
            Lines(
                "",
                "  first difference at index 1: actual has 5, expected has 2",
                "  actual has 3 items, expected has 2",
                "  actual has 1 item past the end of expected: 6",
                ""),
            Fail("Arrays", new[] { 1, 5, 6 }, new[] { 1, 2 }).Message);
    }

    [VerifyTest]
    public void An_empty_side_has_no_item_at_index_0()
    {
        Assert.Contains(
            Lines(
                "",
                "  first difference at index 0: actual has no item, expected has 1",
                "  actual has 0 items, expected has 3",
                "  expected has 3 items past the end of actual: 1, 2, 3",
                ""),
            Fail("Arrays", Array.Empty<int>(), new[] { 1, 2, 3 }).Message);
        Assert.Contains(
            "\n  first difference at index 0: actual has 7, expected has no item\n  actual has 1 item, expected has 0\n",
            Fail("Arrays", new[] { 7 }, Array.Empty<int>()).Message);
    }

    [VerifyTest]
    public void Items_past_the_end_stop_after_ten()
    {
        Assert.Contains(
            "\n  expected has 12 items past the end of actual: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, …\n",
            Fail("Arrays", Array.Empty<int>(), Enumerable.Range(1, 12).ToArray()).Message);
        Assert.Contains(
            "\n  expected has 10 items past the end of actual: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10\n",
            Fail("Arrays", Array.Empty<int>(), Enumerable.Range(1, 10).ToArray()).Message);
    }

    [VerifyTest]
    public void String_items_print_quoted()
    {
        Assert.Contains(
            "\n  first difference at index 1: actual has \"b\", expected has \"c\"\n  actual has 2 items, expected has 2\n",
            Fail("Lists", new List<string> { "a", "b" }, new List<string> { "a", "c" }).Message);
    }

    [VerifyTest]
    public void The_static_form_gives_the_lines()
    {
        Assert.Contains(
            "\n  first difference at index 0: actual has 1, expected has 2\n",
            Fail("Static", new List<int> { 1 }, new[] { 2 }).Message);
    }

    [VerifyTest]
    public void Check_gives_the_lines()
    {
        Assert.Contains(
            "\n  first difference at index 0: actual has 1, expected has 2\n",
            Fail("Checked", new[] { 1 }, new[] { 2 }).Message);
    }

    [VerifyTest]
    public void A_lazy_sequence_is_not_read_again_and_gives_no_lines()
    {
        var runs = (FieldInfo)Cases.Value.GetMember("Runs")[0];
        runs.SetValue(null, 0);

        var message = Fail("Lazy", 2, new[] { 1, 2, 3 }).Message;

        Assert.Equal(1, runs.GetValue(null));
        Assert.DoesNotContain("first difference", message);
        Assert.DoesNotContain(" is missing ", Fail("LazySet", new HashSet<int> { 1 }, 2).Message);
    }

    [VerifyTest]
    public void A_comparer_gives_no_lines()
    {
        Assert.DoesNotContain("first difference", Fail("Comparer", new[] { "a" }, new[] { "A" }).Message);
    }

    [VerifyTest]
    public void A_failed_set_equals_names_the_missing_and_extra_items()
    {
        Assert.Equal(
            Lines(
                "Verify failed: tags.SetEquals(expected)",
                "  tags is missing 1 item that expected has: \"beta\"",
                "  tags has 2 items that expected does not: \"gamma\", \"delta\"",
                "  tags     → System.Collections.Generic.HashSet`1[System.String]",
                "  expected → System.String[]",
                $"  at Tests/Collections.cs:{LineOf("Sets")}"),
            Fail("Sets", new HashSet<string> { "alpha", "gamma", "delta" }, new[] { "alpha", "beta" }).Message);
    }

    [VerifyTest]
    public void Sets_of_equal_size_with_one_differing_item_name_one_missing_and_one_extra()
    {
        Assert.Contains(
            Lines(
                "",
                "  numbers is missing 1 item that expected has: 3",
                "  numbers has 1 item that expected does not: 4",
                ""),
            Fail("Interface", new HashSet<int> { 1, 2, 4 }, new List<int> { 1, 2, 3 }).Message);
    }

    [VerifyTest]
    public void An_empty_side_of_set_equals_gives_only_one_line()
    {
        var emptySet = Fail("Sets", new HashSet<string>(), new[] { "a", "b" }).Message;
        Assert.Contains("\n  tags is missing 2 items that expected has: \"a\", \"b\"\n", emptySet);
        Assert.DoesNotContain(" does not: ", emptySet);

        var emptyOther = Fail("Sets", new HashSet<string> { "a" }, Array.Empty<string>()).Message;
        Assert.Contains("\n  tags has 1 item that expected does not: \"a\"\n", emptyOther);
        Assert.DoesNotContain(" is missing ", emptyOther);
    }

    [VerifyTest]
    public void Missing_items_are_listed_once_and_stop_after_ten()
    {
        Assert.Contains(
            "\n  numbers is missing 12 items that expected has: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, …\n",
            Fail("Interface", new HashSet<int>(), Enumerable.Range(1, 12).Concat(Enumerable.Range(1, 12)).ToList()).Message);
    }

    [VerifyTest]
    public void Items_match_by_the_comparer_of_the_set()
    {
        var message = Fail("Sets", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Alpha" }, new[] { "alpha", "beta" }).Message;

        Assert.Contains("\n  tags is missing 1 item that expected has: \"beta\"\n", message);
        Assert.DoesNotContain(" does not: ", message);
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
