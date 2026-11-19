using System.Reflection;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Interpolation holes in a condition that the check copies with qualified names, such as
/// <c>global::GlobalRepro.Esc</c>. The first ':' of such a name would start the hole's format specifier.
/// </summary>
public class InterpolationHoleSpecs
{
    private const string Types = """
        using System;
        using System.Linq;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace GlobalRepro;

        public enum Kind { Word, Arrow }

        public sealed record Token(Kind Kind, string Text);

        public static class Esc
        {
            public static string Escape(string s) => s.Replace("\r", "\\r").Replace("\n", "\\n");
            public static int Count(string s) => s.Length;
            public static string Marker => "#";
            public static string Name<T>() => typeof(T).Name;
        }

        """;

    private const string Members = """
            static readonly Token[] Tokens = [new(Kind.Word, "A"), new(Kind.Arrow, "\r\n")];
            static string Prefix => ">";
            static string Wrap(string s) => "[" + s + "]";

            public static void EnumHole() => Verify(() => string.Join(",", Tokens.Select(t => $"{t.Kind}")) == "Word,Arrow");
            public static void MethodHole() => Verify(() => string.Join(",", Tokens.Select(t => $"{Esc.Escape(t.Text)}")) == "A,\\r\\n");
            public static void Both() => Verify(() => string.Join(",", Tokens.Select(t => $"{t.Kind}:{Esc.Escape(t.Text)}")) == "Word:A,Arrow:\\r\\n");

            public static void VerifyPasses()
            {
                var text = "ab";
                Verify(() => $"{Esc.Escape(text)}" == "ab");
                Verify(() => $"{Esc.Marker}{text}" == "#ab");
                Verify(() => $"{Esc.Name<Kind>()}" == "Kind");
                Verify(() => $"{Esc.Escape(text),4}" == "  ab");
                Verify(() => $"{Esc.Count(text):D3}" == "002");
                Verify(() => $"{Prefix}{Wrap(text)}" == ">[ab]");
            }

            public static void CheckPasses()
            {
                var text = "ab";
                Check(() => $"{Esc.Escape(text)}" == "ab");
                Check(() => $"{Esc.Marker}{text}" == "#ab");
                Check(() => $"{Esc.Name<Kind>()}" == "Kind");
                Check(() => $"{Esc.Escape(text),4}" == "  ab");
                Check(() => $"{Esc.Count(text):D3}" == "002");
                Check(() => $"{Prefix}{Wrap(text)}" == ">[ab]");
            }

            public static void VerifyFails()
            {
                var text = "ab";
                Verify(() => $"{Esc.Escape(text),4}|{Esc.Count(text):D3}|{Esc.Marker}|{Esc.Name<Kind>()}|{Prefix}{Wrap(text)}" == "");
            }

            public static void CheckFails()
            {
                var text = "ab";
                Check(() => $"{Esc.Escape(text),4}|{Esc.Count(text):D3}|{Esc.Marker}|{Esc.Name<Kind>()}|{Prefix}{Wrap(text)}" == "");
            }
        """;

    private const string Source = Types + "public class Tests\n{\n" + Members + "\n}\n";

    private const string RewrittenSource = Types + "public partial class Tests\n{\n" + Members + "\n}\n";

    private const string Condition = "$\"{Esc.Escape(text),4}|{Esc.Count(text):D3}|{Esc.Marker}|{Esc.Name<Kind>()}|{Prefix}{Wrap(text)}\" == \"\"";

    private static readonly Lazy<Assembly> Lambdas = new(() => GeneratorHarness.Load(("/repo/Project/Tests/Holes.cs", Source)));

    private static readonly Lazy<Assembly> Rewritten = new(() => GeneratorHarness.Load(rewrite: true, ("/repo/Project/Tests/Holes.cs", RewrittenSource.Replace("    public static void", "    [Verify]\n    public static void", StringComparison.Ordinal))));

    public static IEnumerable<string> Passing => ["EnumHole", "MethodHole", "Both", "VerifyPasses"];

    [VerifyTest]
    [RowSource(nameof(Passing))]
    public void A_lambda_with_a_qualified_name_in_a_hole_passes(string method) => Invoke(Lambdas.Value, method);

    [VerifyTest]
    [RowSource(nameof(Passing))]
    public void A_rewritten_method_with_a_qualified_name_in_a_hole_passes(string method) => Invoke(Rewritten.Value, method);

    [VerifyTest]
    public void A_check_lambda_with_a_qualified_name_in_a_hole_passes() => InScope(Lambdas.Value, "CheckPasses");

    [VerifyTest]
    public void A_check_in_a_rewritten_method_with_a_qualified_name_in_a_hole_passes() => InScope(Rewritten.Value, "CheckPasses");

    [VerifyTest]
    public void A_failed_lambda_shows_the_text_of_its_holes()
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Invoke(Lambdas.Value, "VerifyFails"));

        Assert.Equal(Expected("Verify", "VerifyFails"), Assert.IsType<VerifyException>(thrown.InnerException).Message);
    }

    [VerifyTest]
    public void A_failed_check_lambda_shows_the_text_of_its_holes()
    {
        var failure = Assert.Throws<VerifyException>(() => InScope(Lambdas.Value, "CheckFails"));

        Assert.Equal("1 check failed\n\n" + Expected("Check", "CheckFails"), failure.Message);
    }

    [VerifyTest]
    public void A_failed_rewritten_method_shows_the_text_of_its_holes()
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Invoke(Rewritten.Value, "VerifyFails"));

        Assert.Equal(Expected("Verify", "VerifyFails", rewritten: true), Assert.IsType<VerifyException>(thrown.InnerException).Message);
    }

    // The text a hand-written $"{"ab",4}|{2:D3}|{"#"}|{"Kind"}|{">"}{"[ab]"}" gives.
    private static string Expected(string kind, string method, bool rewritten = false) =>
        $"{kind} failed: {Condition}\n" +
        $"  {Condition[..^6]} → \"{$"{"ab",4}|{2:D3}|{"#"}|{"Kind"}|{">"}{"[ab]"}"}\"\n" +
        $"  at Tests/Holes.cs:{LineOf(method, rewritten)}";

    private static void Invoke(Assembly assembly, string method) =>
        assembly.GetType("GlobalRepro.Tests", throwOnError: true)!.GetMethod(method)!.Invoke(null, null);

    private static void InScope(Assembly assembly, string method)
    {
        var scope = VerifyScope.Begin();
        try
        {
            Invoke(assembly, method);
        }
        finally
        {
            scope.Dispose();
        }
    }

    private static int LineOf(string method, bool rewritten)
    {
        var source = rewritten ? RewrittenSource.Replace("    public static void", "    [Verify]\n    public static void", StringComparison.Ordinal) : Source;
        var lines = source.Split('\n');
        return Array.FindIndex(lines, line => line.Contains($"void {method}(", StringComparison.Ordinal)) + 4;
    }
}
