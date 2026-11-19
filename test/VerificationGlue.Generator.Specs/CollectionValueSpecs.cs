using System.Reflection;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// A failed <c>Verify(() =&gt; ...)</c> or <c>Check(() =&gt; ...)</c> shows a collection value by its
/// count and items, and a lazy sequence by its type without reading it again.
/// </summary>
public class CollectionValueSpecs
{
    private const string Path = "/repo/Project/Tests/CollectionValues.cs";

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Sample;

        public static class Cases
        {
            public static int Reads;

            public static void Problems()
            {
                var problems = new List<string> { "flowchart/a: box differs", "pie/b: missing label" };
                Verify(() => problems.Count == 0); /*Problems*/
            }

            public static void Array()
            {
                int[] values = [1, 2, 3];
                Verify(() => values.Length == 0); /*Array*/
            }

            public static void Empty()
            {
                var names = new List<string>();
                Verify(() => names.Count == 1); /*Empty*/
            }

            public static void PastTheLimit()
            {
                var values = Enumerable.Range(1, 12).ToList();
                Verify(() => values.Count == 0); /*PastTheLimit*/
            }

            public static void LongPastTheLimit()
            {
                var lines = Enumerable.Range(1, 12).Select(number => "line number " + number).ToArray();
                Verify(() => lines.Length == 0); /*LongPastTheLimit*/
            }

            public static void Nested()
            {
                var grid = new List<int[]> { new[] { 1, 2 }, new[] { 3 } };
                Verify(() => grid.Count == 0); /*Nested*/
            }

            public static void Deep()
            {
                var deep = new[] { new[] { new[] { new[] { 1 } } } };
                Verify(() => deep.Length == 0); /*Deep*/
            }

            public static void Dictionary()
            {
                var stock = new Dictionary<string, int> { ["gift"] = 3, ["card"] = 1 };
                Verify(() => stock.Count == 0); /*Dictionary*/
            }

            public static void Set()
            {
                var tags = new HashSet<string> { "gift" };
                Verify(() => tags.Contains("express")); /*Set*/
            }

            public static void Lazy()
            {
                var items = new List<int> { 1, 2, 3 };
                var doubled = items.Select(item => { Reads++; return item * 2; });
                Verify(() => doubled.Count() == 0); /*Lazy*/
            }

            public static void Iterator()
            {
                var numbers = Numbers();
                Verify(() => numbers.Any(number => number > 5)); /*Iterator*/
            }

            public static void Text()
            {
                var name = "Bo";
                Verify(() => name.Length == 0); /*Text*/
            }

            public static void Checked()
            {
                using (VerifyScope.Begin())
                {
                    var values = new[] { 4, 5 };
                    Check(() => values.Length == 0); /*Checked*/
                }
            }

            private static IEnumerable<int> Numbers()
            {
                Reads++;
                yield return 1;
                yield return 2;
            }
        }
        """;

    private static readonly Lazy<Type> Cases = new(() => GeneratorHarness.Load((Path, Source)).GetType("Sample.Cases", throwOnError: true)!);

    public static IEnumerable<(string Method, string Expected)> Messages =>
    [
        ("Problems", Lines(
            "Verify failed: problems.Count == 0",
            "  problems.Count → 2",
            "  problems       → 2 items",
            "                     \"flowchart/a: box differs\"",
            "                     \"pie/b: missing label\"")),
        ("Array", Lines(
            "Verify failed: values.Length == 0",
            "  values.Length → 3",
            "  values        → 3 items: 1, 2, 3")),
        ("Empty", Lines(
            "Verify failed: names.Count == 1",
            "  names.Count → 0",
            "  names       → 0 items")),
        ("PastTheLimit", Lines(
            "Verify failed: values.Count == 0",
            "  values.Count → 12",
            "  values       → 12 items: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, …")),
        ("LongPastTheLimit", Lines(
            "Verify failed: lines.Length == 0",
            "  lines.Length → 12",
            "  lines        → 12 items",
            "                   \"line number 1\"",
            "                   \"line number 2\"",
            "                   \"line number 3\"",
            "                   \"line number 4\"",
            "                   \"line number 5\"",
            "                   \"line number 6\"",
            "                   \"line number 7\"",
            "                   \"line number 8\"",
            "                   \"line number 9\"",
            "                   \"line number 10\"",
            "                   …")),
        ("Nested", Lines(
            "Verify failed: grid.Count == 0",
            "  grid.Count → 2",
            "  grid       → 2 items: [1, 2], [3]")),
        ("Deep", Lines(
            "Verify failed: deep.Length == 0",
            "  deep.Length → 1",
            "  deep        → 1 item: [[[…]]]")),
        ("Dictionary", Lines(
            "Verify failed: stock.Count == 0",
            "  stock.Count → 2",
            "  stock       → 2 items: \"gift\": 3, \"card\": 1")),
        ("Set", Lines(
            "Verify failed: tags.Contains(\"express\")",
            "  tags → 1 item: \"gift\"")),
        ("Text", Lines(
            "Verify failed: name.Length == 0",
            "  name.Length → 2",
            "  name        → \"Bo\"")),
    ];

    [VerifyTest]
    [RowSource(nameof(Messages))]
    public void A_collection_value_shows_its_count_and_items(string method, string expected)
    {
        Assert.Equal(expected + $"\n  at Tests/CollectionValues.cs:{LineOf(method)}", Fail(method).Message);
    }

    [VerifyTest]
    public void A_lazy_sequence_is_not_read_again_and_shows_its_type()
    {
        var reads = (FieldInfo)Cases.Value.GetMember("Reads")[0];
        reads.SetValue(null, 0);

        var lazy = Fail("Lazy").Message;
        Assert.Equal(3, reads.GetValue(null));
        Assert.Contains("\n  doubled.Count() → 3\n", lazy);
        Assert.Matches(@"\n  doubled         → \w*Select\w*<Int32, Int32> \(not read again, because reading it could run its code\)\n", lazy);

        reads.SetValue(null, 0);
        var iterator = Fail("Iterator").Message;
        Assert.Equal(1, reads.GetValue(null));
        Assert.Matches(@"\n  numbers → <Numbers>\w+ \(not read again, because reading it could run its code\)\n", iterator);
    }

    [VerifyTest]
    public void A_check_lambda_shows_the_items_when_the_scope_ends()
    {
        Assert.Equal(
            $"1 check failed\n\nCheck failed: values.Length == 0\n  values.Length → 2\n  values        → 2 items: 4, 5\n  at Tests/CollectionValues.cs:{LineOf("Checked")}",
            Fail("Checked").Message);
    }

    private static VerifyException Fail(string method)
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Cases.Value.GetMethod(method)!.Invoke(null, null));
        return Assert.IsType<VerifyException>(thrown.InnerException);
    }

    private static int LineOf(string method) =>
        Array.FindIndex(Source.Split('\n'), line => line.Contains($"/*{method}*/", StringComparison.Ordinal)) + 1;

    private static string Lines(params string[] lines) => string.Join("\n", lines);
}
