using System.Reflection;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Tier 3: a failed <c>==</c> on two strings shows where they first differ, by line and column for
/// multi-line text and by index for a long single line.
/// </summary>
public class StringDifferenceSpecs
{
    private const string Path = "/repo/Project/Tests/StringDifference.cs";

    private const string Source = """
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Sample;

        public static partial class Cases
        {
            [Verify]
            public static void Equal(string written, string expected) => Verify(written == expected);

            [Verify]
            public static void Constant(string written) => Verify(written == "<svg>\n<g>\n</g>\n</svg>");

            [Verify]
            public static void Checked(string written, string expected)
            {
                using (VerifyScope.Begin())
                {
                    Check(written == expected);
                }
            }

            [Verify]
            public static void Because(string written, string expected) => Verify(written == expected).Because("the file is generated");

            [Verify]
            public static void NotEqual(string written, string expected) => Verify(written != expected);

            [Verify]
            public static void Lengths(string written, string expected) => Verify(written.Length == expected.Length);
        }
        """;

    private static readonly Lazy<Type> Cases = new(() => GeneratorHarness.Load(rewrite: true, (Path, Source)).GetType("Sample.Cases", throwOnError: true)!);

    [VerifyTest]
    public void A_difference_on_the_first_line_shows_the_lines_after_it()
    {
        Assert.Equal(
            Lines(
                "Verify failed: written == expected",
                "  written  → \"<svg>\\n<g>\\n</g>\\n</svg>\"",
                "  expected → \"<svx>\\n<g>\\n</g>\\n</svg>\"",
                "  first difference at line 1, column 4",
                "  - 1   <svg>",
                "  + 1   <svx>",
                "    2   <g>",
                "    3   </g>",
                $"  at Tests/StringDifference.cs:{LineOf("Equal")}"),
            Fail("Equal", "<svg>\n<g>\n</g>\n</svg>", "<svx>\n<g>\n</g>\n</svg>").Message);
    }

    [VerifyTest]
    public void A_difference_on_the_last_line_shows_the_lines_before_it()
    {
        Assert.Equal(
            [
                "  first difference at line 4, column 2",
                "    2   b",
                "    3   c",
                "  - 4   d1",
                "  + 4   d2",
            ],
            Difference(Fail("Equal", "a\nb\nc\nd1", "a\nb\nc\nd2")));
    }

    [VerifyTest]
    public void A_longer_right_side_shows_where_the_left_side_ends()
    {
        Assert.Equal(
            [
                "  first difference at line 3, column 1",
                "    1   a",
                "    2   b",
                "  - 3   <end of text>",
                "  + 3   c",
                "  + 4   d",
            ],
            Difference(Fail("Equal", "a\nb", "a\nb\nc\nd")));
    }

    [VerifyTest]
    public void A_longer_left_side_shows_where_the_right_side_ends()
    {
        Assert.Equal(
            [
                "  first difference at line 3, column 1",
                "    1   a",
                "    2   b",
                "  - 3",
                "  + 3   <end of text>",
            ],
            Difference(Fail("Equal", "a\nb\n", "a\nb")));
    }

    [VerifyTest]
    public void A_carriage_return_against_a_line_feed_shows_the_carriage_return()
    {
        Assert.Equal(
            [
                "  first difference at line 1, column 2",
                "  - 1   a\\r",
                "  + 1   a",
                "  - 2   b\\r",
                "  + 2   b",
                "    3   c",
            ],
            Difference(Fail("Equal", "a\r\nb\r\nc", "a\nb\nc")));
    }

    [VerifyTest]
    public void The_lines_around_the_difference_are_numbered_to_the_same_width()
    {
        var written = string.Join("\n", Enumerable.Range(1, 12).Select(number => $"line {number}"));

        Assert.Equal(
            [
                "  first difference at line 10, column 6",
                "     8   line 8",
                "     9   line 9",
                "  - 10   line 10",
                "  + 10   line ten",
                "    11   line 11",
                "    12   line 12",
            ],
            Difference(Fail("Equal", written, written.Replace("line 10", "line ten"))));
    }

    [VerifyTest]
    public void Long_values_are_cut_short_once_the_difference_is_shown()
    {
        var written = "<svg>\n" + string.Concat(Enumerable.Repeat("<rect/>\n", 8)) + "</svg>";

        var lines = Fail("Equal", written, written.Replace("</svg>", "</svx>")).Message.Split('\n');

        Assert.Equal("  written  → \"<svg>\\n<rect/>\\n<rect/>\\n<rect/>\\n<rect/>\\n<r\"… (76 characters)", lines[1]);
        Assert.Equal("  expected → \"<svg>\\n<rect/>\\n<rect/>\\n<rect/>\\n<rect/>\\n<r\"… (76 characters)", lines[2]);
    }

    [VerifyTest]
    public void A_long_line_shows_the_text_around_the_difference()
    {
        var written = "a\n" + new string('x', 100) + "y" + new string('z', 20);

        Assert.Equal(
            [
                "  first difference at line 2, column 101",
                "    1   a",
                $"  - 2   …{new string('x', 59)}y{new string('z', 20)}",
                $"  + 2   …{new string('x', 59)}Y{new string('z', 20)}",
            ],
            Difference(Fail("Equal", written, written.Replace('y', 'Y'))));
    }

    [VerifyTest]
    public void A_long_single_line_shows_the_index_and_the_text_around_it()
    {
        var written = new string('x', 100) + "\"y\"" + new string('z', 100);

        Assert.Equal(
            [
                "  first difference at index 101",
                $"  - …\"{new string('x', 29)}\\\"y\\\"{new string('z', 48)}\"…",
                $"  + …\"{new string('x', 29)}\\\"Y\\\"{new string('z', 48)}\"…",
            ],
            Difference(Fail("Equal", written, written.Replace('y', 'Y'))));
    }

    [VerifyTest]
    public void A_single_line_that_ends_early_shows_both_ends()
    {
        var written = new string('x', 45);

        Assert.Equal(
            [
                "  first difference at index 45",
                $"  - \"{written}\"",
                $"  + \"{written}!\"",
            ],
            Difference(Fail("Equal", written, written + "!")));
    }

    [VerifyTest]
    public void A_constant_operand_gives_the_difference()
    {
        Assert.Equal(
            [
                "  first difference at line 3, column 2",
                "    1   <svg>",
                "    2   <g>",
                "  - 3   <g>",
                "  + 3   </g>",
                "  - 4   <svg>",
                "  + 4   </svg>",
            ],
            Difference(Fail("Constant", "<svg>\n<g>\n<g>\n<svg>")));
    }

    [VerifyTest]
    public void Check_gives_the_same_message()
    {
        var verified = Fail("Equal", "a\nb", "a\nc").Message.Split('\n')[1..^1];

        Assert.Contains($"Check failed: written == expected\n{string.Join("\n", verified)}\n  at ", Fail("Checked", "a\nb", "a\nc").Message);
    }

    [VerifyTest]
    public void The_reason_follows_the_difference()
    {
        Assert.EndsWith(
            Lines(
                "  + 2   c",
                "  because the file is generated",
                $"  at Tests/StringDifference.cs:{LineOf("Because")}"),
            Fail("Because", "a\nb", "a\nc").Message);
    }

    [VerifyTest]
    public void Short_single_lines_and_other_conditions_keep_their_message()
    {
        Assert.Equal(
            Lines(
                "Verify failed: written == expected",
                "  written  → \"abc\"",
                "  expected → \"abd\"",
                $"  at Tests/StringDifference.cs:{LineOf("Equal")}"),
            Fail("Equal", "abc", "abd").Message);
        Assert.DoesNotContain("first difference", Fail("NotEqual", "a\nb", "a\nb").Message);
        Assert.DoesNotContain("first difference", Fail("Lengths", "a\nb", "a\nbc").Message);
        Assert.DoesNotContain("first difference", Fail("Equal", "a\nb", null!).Message);
    }

    private static VerifyException Fail(string method, params object[] arguments)
    {
        var target = Cases.Value.GetMethod(method + "__Verified")!;
        var thrown = Assert.Throws<TargetInvocationException>(() => target.Invoke(null, arguments));
        return Assert.IsType<VerifyException>(thrown.InnerException);
    }

    /// <summary>The lines of the message from its first difference line to the line before the location.</summary>
    private static string[] Difference(VerifyException failure)
    {
        var lines = failure.Message.Split('\n');
        return lines[Array.FindIndex(lines, line => line.StartsWith("  first difference ", StringComparison.Ordinal))..^1];
    }

    private static int LineOf(string method) =>
        Array.FindIndex(Source.Split('\n'), line => line.Contains($" {method}(", StringComparison.Ordinal)) + 1;

    private static string Lines(params string[] lines) => string.Join("\n", lines);
}
