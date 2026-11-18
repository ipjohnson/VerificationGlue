namespace VerificationGlue.Generator.Specs;

public class VerifyAnalyzerSpecs
{
    private const string Header = """
        using System.Collections.Generic;
        using System.Linq;
        using static VerificationGlue.VerifySyntax;

        """;

    [VerifyTest]
    [Row("Verify(true);", "true", "true")]
    [Row("Verify(false, $\"never {1}\");", "false", "false")]
    [Row("const int Limit = 3; Verify(Limit > 2);", "Limit > 2", "true")]
    public void VG0002_reports_a_constant_condition(string body, string condition, string value)
    {
        var diagnostic = Assert.Single(Analyze(body));

        Assert.Equal("VG0002", diagnostic.Id);
        Assert.Equal($"The condition '{condition}' is always {value}, so this Verify call checks nothing", diagnostic.GetMessage());
        Assert.Equal(condition, Text(diagnostic));
    }

    [VerifyTest]
    [Row("var attempts = 0; Verify(attempts++ == 0);", "attempts++")]
    [Row("var i = 0; Verify(--i == -1);", "--i")]
    [Row("var i = 0; Verify((i = 2) == 2);", "i = 2")]
    [Row("var i = 0; Verify((i += 2) == 2);", "i += 2")]
    [Row("string? s = null; Verify((s ??= \"x\") == \"x\");", "s ??= \"x\"")]
    public void VG0003_reports_a_side_effect(string body, string effect)
    {
        var diagnostic = Assert.Single(Analyze(body));

        Assert.Equal("VG0003", diagnostic.Id);
        Assert.Equal($"'{effect}' changes state inside a Verify condition. Move it to a statement before the Verify call.", diagnostic.GetMessage());
        Assert.Equal(effect, Text(diagnostic));
    }

    [VerifyTest]
    [Row("double total = 0.1 + 0.2; Verify(total == 0.3);", "total == 0.3", "double", "==")]
    [Row("float f = 1f; Verify(f != 2);", "f != 2", "float", "!=")]
    [Row("var items = new List<double> { 1 }; Verify(items[0] == 1 && items.Count == 1);", "items[0] == 1", "double", "==")]
    public void VG0004_reports_an_exact_floating_point_comparison(string body, string comparison, string type, string op)
    {
        var diagnostic = Assert.Single(Analyze(body));

        Assert.Equal("VG0004", diagnostic.Id);
        Assert.Equal($"'{comparison}' compares {type} values with '{op}'. Compare the difference with a tolerance instead.", diagnostic.GetMessage());
        Assert.Equal(comparison, Text(diagnostic));
    }

    [VerifyTest]
    [Row("var count = 3; Verify(count == 3);")]
    [Row("var count = 3; Verify(count == 3, $\"count {count}\");")]
    [Row("var items = new List<int> { 1 }; var sum = 0; Verify(items.All(item => { sum += item; return item > 0; }));")]
    [Row("double total = 0.1 + 0.2; Verify(System.Math.Abs(total - 0.3) < 0.000001);")]
    [Row("var attempts = 0; attempts++; Verify(attempts == 1);")]
    public void Plain_conditions_report_nothing(string body)
    {
        Assert.Empty(Analyze(body));
    }

    [VerifyTest]
    public void Another_method_named_Verify_reports_nothing()
    {
        var source = """
            static class Mine
            {
                static void Verify(bool condition) { }
                static void M() { var i = 0; Verify(true); Verify(i++ == 0); }
            }
            """;

        Assert.Empty(GeneratorHarness.Analyze(source));
    }

    private static IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> Analyze(string body) =>
        GeneratorHarness.Analyze(Header + "static class Tests { static void M() { " + body + " } }");

    private static string Text(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);
}
