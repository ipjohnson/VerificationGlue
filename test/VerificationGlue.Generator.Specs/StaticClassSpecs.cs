using System.Reflection;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Conditions in a static class that read its private static members. A static class cannot be the
/// type of an <c>[UnsafeAccessor]</c>'s self parameter, so the accessor names it with <c>[UnsafeAccessorType]</c>.
/// </summary>
public class StaticClassSpecs
{
    private const string Path = "/repo/Project/Tests/Statics.cs";

    private const string Source = """
        using System.Linq;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Sample;

        public sealed record Token(string Text);

        public static class Tests
        {
            private static readonly Token[] Tokens = [new("a"), new("b")];
            private static int Limit => 3;
            private static int Twice(int value) => value * 2;

            public static void VerifyPasses()
            {
                var count = 2;
                Verify(() => Tokens.Length == count);
                Verify(() => Limit == 3);
                Verify(() => Twice(count) == 4);
                Verify(() => string.Join(",", Tokens.Select(t => $"{t.Text}{Twice(count)}")) == "a4,b4");
                Verify(() => Nested.Secret == 9);
            }

            public static void CheckPasses()
            {
                var count = 2;
                Check(() => Tokens.Length == count);
                Check(() => Limit == 3);
                Check(() => Twice(count) == 4);
                Check(() => string.Join(",", Tokens.Select(t => $"{t.Text}{Twice(count)}")) == "a4,b4");
                Check(() => Nested.Secret == 9);
            }

            public static void Field() => Verify(() => Tokens.Length == 3); /*Field*/

            public static void Property()
            {
                var count = 4;
                Verify(() => count <= Limit); /*Property*/
            }

            public static void Method()
            {
                var count = 2;
                Verify(() => Twice(count) == 5); /*Method*/
            }

            public static void NestedField() => Verify(() => Nested.Secret == 8); /*NestedField*/

            public static void CheckFails()
            {
                var count = 2;
                Check(() => Twice(count) == Limit); /*CheckFails*/
            }

            public static class Nested
            {
                private static int secret = 9;

                public static int Secret => secret;
            }
        }
        """;

    private static readonly Lazy<Assembly> Compiled = new(() => GeneratorHarness.Load((Path, Source)));

    public static IEnumerable<(string Method, string Expected)> Messages =>
    [
        ("Field", "Verify failed: Tokens.Length == 3\n  Tokens.Length → 2\n  Tokens        → 2 items\n                    Token { Text = a }\n                    Token { Text = b }"),
        ("Property", "Verify failed: count <= Limit\n  count → 4\n  Limit → 3"),
        ("Method", "Verify failed: Twice(count) == 5\n  Twice(count) → 4"),
        ("NestedField", "Verify failed: Nested.Secret == 8\n  Nested.Secret → 9"),
    ];

    [VerifyTest]
    public void A_lambda_that_reads_private_static_members_of_a_static_class_passes() => Invoke("VerifyPasses");

    [VerifyTest]
    public void A_check_that_reads_private_static_members_of_a_static_class_passes() => InScope("CheckPasses");

    [VerifyTest]
    [RowSource(nameof(Messages))]
    public void A_failed_lambda_in_a_static_class_lists_the_value_of_each_part(string method, string expected)
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Invoke(method));

        Assert.Equal(expected + $"\n  at Tests/Statics.cs:{LineOf(method)}", Assert.IsType<VerifyException>(thrown.InnerException).Message);
    }

    [VerifyTest]
    public void A_failed_check_in_a_static_class_lists_the_value_of_each_part()
    {
        var failure = Assert.Throws<VerifyException>(() => InScope("CheckFails"));

        Assert.Equal(
            "1 check failed\n\nCheck failed: Twice(count) == Limit\n  Twice(count) → 4\n  Limit        → 3\n" +
            $"  at Tests/Statics.cs:{LineOf("CheckFails")}",
            failure.Message);
    }

    private static void Invoke(string method) =>
        Compiled.Value.GetType("Sample.Tests", throwOnError: true)!.GetMethod(method)!.Invoke(null, null);

    private static void InScope(string method)
    {
        var scope = VerifyScope.Begin();
        try
        {
            Invoke(method);
        }
        finally
        {
            scope.Dispose();
        }
    }

    private static int LineOf(string method) =>
        Array.FindIndex(Source.Split('\n'), line => line.Contains($"/*{method}*/", StringComparison.Ordinal)) + 1;
}
