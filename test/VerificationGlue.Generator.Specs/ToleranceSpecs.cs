using System.Reflection;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Tier 3: a failed <c>Math.Abs(a - b) &lt;= t</c> or <c>&lt; t</c> adds a line that says how far
/// apart the values were and what the tolerance was.
/// </summary>
public class ToleranceSpecs
{
    private const string Path = "/repo/Project/Tests/Tolerance.cs";

    private const string Source = """
        using System;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Sample;

        public static partial class Cases
        {
            [Verify]
            public static void AtMost(double actual) => Verify(Math.Abs(actual - 120.5) <= 0.01);

            [Verify]
            public static void ConstantFirst(double actual) => Verify(Math.Abs(120.5 - actual) <= 0.01);

            [Verify]
            public static void ToleranceFirst(double actual) => Verify(0.01 >= Math.Abs(actual - 120.5));

            [Verify]
            public static void LessThan(double actual, double expected, double tolerance) => Verify(Math.Abs(actual - expected) < tolerance);

            [Verify]
            public static void ToleranceFirstLessThan(double actual, double expected, double tolerance) => Verify(tolerance > Math.Abs(expected - actual));

            [Verify]
            public static void Floats(float actual, float expected) => Verify(Math.Abs(actual - expected) <= 0.5f);

            [Verify]
            public static void Decimals(decimal actual) => Verify(Math.Abs(actual - 2.5m) < 0.1m);

            [Verify]
            public static void Checked(double actual)
            {
                using (VerifyScope.Begin())
                {
                    Check(Math.Abs(actual - 1) <= 0.25);
                }
            }

            [Verify]
            public static void NoSubtraction(double actual) => Verify(Math.Abs(actual) <= 1);

            [Verify]
            public static void Integers(int actual) => Verify(Math.Abs(actual - 3) <= 1);

            [Verify]
            public static void NotATolerance(double actual) => Verify(Math.Abs(actual - 1) >= 2);
        }
        """;

    private static readonly Lazy<Type> Cases = new(() => GeneratorHarness.Load(rewrite: true, (Path, Source)).GetType("Sample.Cases", throwOnError: true)!);

    [VerifyTest]
    public void A_failed_tolerance_check_says_how_far_apart_the_values_were()
    {
        var failure = Fail("AtMost", 120.5625);

        Assert.Equal(
            Lines(
                "Verify failed: Math.Abs(actual - 120.5) <= 0.01",
                "  actual differs from 120.5 by 0.0625, tolerance 0.01",
                "  Math.Abs(actual - 120.5) → 0.0625",
                "  actual - 120.5           → 0.0625",
                "  actual                   → 120.5625",
                $"  at Tests/Tolerance.cs:{LineOf("AtMost")}"),
            failure.Message);
    }

    [VerifyTest]
    public void A_constant_on_the_left_of_the_subtraction_is_named_second()
    {
        Assert.Contains("\n  actual differs from 120.5 by 0.0625, tolerance 0.01\n", Fail("ConstantFirst", 120.4375).Message);
    }

    [VerifyTest]
    public void The_tolerance_can_come_first()
    {
        Assert.Contains("\n  actual differs from 120.5 by 0.0625, tolerance 0.01\n", Fail("ToleranceFirst", 120.5625).Message);
    }

    [VerifyTest]
    public void Less_than_gives_the_line()
    {
        Assert.Contains("\n  actual differs from expected by 0.25, tolerance 0.25\n", Fail("LessThan", 1.25, 1.0, 0.25).Message);
    }

    [VerifyTest]
    public void Two_variables_keep_their_order_in_the_subtraction()
    {
        Assert.Contains("\n  expected differs from actual by 0.5, tolerance 0.25\n", Fail("ToleranceFirstLessThan", 1.5, 1.0, 0.25).Message);
    }

    [VerifyTest]
    public void Floats_give_the_line()
    {
        Assert.Contains("\n  actual differs from expected by 0.75, tolerance 0.5\n", Fail("Floats", 1.75f, 1f).Message);
    }

    [VerifyTest]
    public void Decimals_give_the_line()
    {
        Assert.Contains("\n  actual differs from 2.5m by 0.25, tolerance 0.1\n", Fail("Decimals", 2.75m).Message);
    }

    [VerifyTest]
    public void Check_gives_the_line()
    {
        Assert.Contains("\n  actual differs from 1 by 0.5, tolerance 0.25\n", Fail("Checked", 1.5).Message);
    }

    [VerifyTest]
    public void NaN_on_the_left_is_called_out()
    {
        Assert.Contains("\n  actual is NaN, so it is not within tolerance 0.25 of expected\n", Fail("LessThan", double.NaN, 1.0, 0.25).Message);
    }

    [VerifyTest]
    public void NaN_on_the_right_is_called_out()
    {
        Assert.Contains("\n  expected is NaN, so it is not within tolerance 0.25 of actual\n", Fail("LessThan", 1.0, double.NaN, 0.25).Message);
    }

    [VerifyTest]
    public void NaN_on_both_sides_is_called_out()
    {
        Assert.Contains("\n  actual and expected are NaN, so they are not within tolerance 0.5\n", Fail("Floats", float.NaN, float.NaN).Message);
    }

    [VerifyTest]
    public void NaN_against_a_constant_is_called_out()
    {
        Assert.Contains("\n  actual is NaN, so it is not within tolerance 0.01 of 120.5\n", Fail("AtMost", double.NaN).Message);
    }

    [VerifyTest]
    public void Another_condition_keeps_its_message()
    {
        Assert.Equal(
            Lines(
                "Verify failed: Math.Abs(actual) <= 1",
                "  Math.Abs(actual) → 2",
                "  actual           → 2",
                $"  at Tests/Tolerance.cs:{LineOf("NoSubtraction")}"),
            Fail("NoSubtraction", 2.0).Message);
        Assert.DoesNotContain(" differs from ", Fail("Integers", 5).Message);
        Assert.DoesNotContain(" differs from ", Fail("NotATolerance", 1.5).Message);
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
