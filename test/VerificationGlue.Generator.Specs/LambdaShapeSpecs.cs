using System.Reflection;
using System.Text.RegularExpressions;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// A failed <c>Verify(() =&gt; ...)</c> or <c>Check(() =&gt; ...)</c> of a recognised shape gives the
/// same message as the same condition at tier 3: <c>SequenceEqual</c>, <c>SetEquals</c>, <c>==</c>
/// on two strings and a tolerance check.
/// </summary>
public partial class LambdaShapeSpecs
{
    private const string Path = "/repo/Project/Tests/LambdaShapes.cs";

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Sample;

        public static partial class Cases
        {
            [Verify]
            public static void SequenceTier3(string[] a, string[] b) => Verify(a.SequenceEqual(b));

            public static void SequenceLambda(string[] a, string[] b) => Verify(() => a.SequenceEqual(b));

            [Verify]
            public static void SequenceTier3Check(string[] a, string[] b)
            {
                using (VerifyScope.Begin())
                {
                    Check(a.SequenceEqual(b));
                }
            }

            public static void SequenceLambdaCheck(string[] a, string[] b)
            {
                using (VerifyScope.Begin())
                {
                    Check(() => a.SequenceEqual(b));
                }
            }

            [Verify]
            public static void SetTier3(HashSet<string> tags, string[] expected) => Verify(tags.SetEquals(expected));

            public static void SetLambda(HashSet<string> tags, string[] expected) => Verify(() => tags.SetEquals(expected));

            [Verify]
            public static void SetTier3Check(HashSet<string> tags, string[] expected)
            {
                using (VerifyScope.Begin())
                {
                    Check(tags.SetEquals(expected));
                }
            }

            public static void SetLambdaCheck(HashSet<string> tags, string[] expected)
            {
                using (VerifyScope.Begin())
                {
                    Check(() => tags.SetEquals(expected));
                }
            }

            [Verify]
            public static void StringTier3(string actual, string expected) => Verify(actual == expected);

            public static void StringLambda(string actual, string expected) => Verify(() => actual == expected);

            [Verify]
            public static void StringTier3Check(string actual, string expected)
            {
                using (VerifyScope.Begin())
                {
                    Check(actual == expected);
                }
            }

            public static void StringLambdaCheck(string actual, string expected)
            {
                using (VerifyScope.Begin())
                {
                    Check(() => actual == expected);
                }
            }

            [Verify]
            public static void ToleranceTier3(double actual, double expected) => Verify(Math.Abs(actual - expected) <= 0.01);

            public static void ToleranceLambda(double actual, double expected) => Verify(() => Math.Abs(actual - expected) <= 0.01);

            [Verify]
            public static void ToleranceTier3Check(double actual, double expected)
            {
                using (VerifyScope.Begin())
                {
                    Check(Math.Abs(actual - expected) <= 0.01);
                }
            }

            public static void ToleranceLambdaCheck(double actual, double expected)
            {
                using (VerifyScope.Begin())
                {
                    Check(() => Math.Abs(actual - expected) <= 0.01);
                }
            }
        }
        """;

    private static readonly Lazy<Type> Cases = new(() => GeneratorHarness.Load(rewrite: true, (Path, Source)).GetType("Sample.Cases", throwOnError: true)!);

    public static IEnumerable<(string Shape, string Line)> Shapes =>
    [
        ("Sequence", "first difference at index 1: a has \"y\", b has \"q\""),
        ("Set", "tags is missing 1 item that expected has: \"beta\""),
        ("String", "first difference at line 2, column 3"),
        ("Tolerance", "actual differs from expected by 0.25, tolerance 0.01"),
    ];

    [VerifyTest]
    [RowSource(nameof(Shapes))]
    public void A_lambda_gives_the_tier_3_explanation(string shape, string line)
    {
        foreach (var check in new[] { "", "Check" })
        {
            var tier3 = Fail(shape + "Tier3" + check + "__Verified", Arguments(shape));
            var lambda = Fail(shape + "Lambda" + check, Arguments(shape));

            Assert.Contains("\n  " + line + "\n", tier3);
            Assert.Equal(WithoutLine(tier3), WithoutLine(lambda));
        }
    }

    private static object[] Arguments(string shape) => shape switch
    {
        "Sequence" => [new[] { "x", "y", "z" }, new[] { "x", "q", "z" }],
        "Set" => [new HashSet<string> { "alpha", "gamma" }, new[] { "alpha", "beta" }],
        "String" => ["id: 4\norder-17", "id: 4\norbit-17"],
        _ => [1.5, 1.25],
    };

    private static string Fail(string method, object[] arguments)
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Cases.Value.GetMethod(method)!.Invoke(null, arguments));
        return Assert.IsType<VerifyException>(thrown.InnerException).Message;
    }

    private static string WithoutLine(string message) => AtLine().Replace(message, "at Tests/LambdaShapes.cs");

    [GeneratedRegex(@"at Tests/LambdaShapes\.cs:\d+")]
    private static partial Regex AtLine();
}
