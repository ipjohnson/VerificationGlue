using Microsoft.CodeAnalysis;

namespace VerificationGlue.Generator.Specs;

public class RewriteAnalyzerSpecs
{
    private const string Header = """
        using System;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        """;

    [VerifyTest]
    [Row("public static void M(int x) => Verify(x == 1).Because(\"why\");", false, "Because")]
    [Row("public static void M(int x) => Verify(x == 1).WithMessage(\"text\");", false, "WithMessage")]
    [Row("public static void M(int x) { Action a = () => Verify(x == 1).Because(\"why\"); a(); }", true, "Because")]
    [Row("public static void M(int x) { var r = Verify(x == 1); r.Because(\"why\"); }", true, null)]
    public void VG0001_reports_Because_outside_a_rewritten_statement(string method, bool rewrite, string? name)
    {
        var diagnostics = Analyze("public static partial class Tests { [Verify] " + method + " }", rewrite);

        if (name is null)
        {
            Assert.Empty(diagnostics);
            return;
        }

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("VG0001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal($"'.{name}' adds text only in a method that tier 3 rewrites. Here Verify throws before '.{name}' runs, so the text never reaches the message.", diagnostic.GetMessage());
        Assert.Equal(name, Text(diagnostic));
    }

    [VerifyTest]
    public void VG0001_does_not_report_Because_in_a_rewritten_statement()
    {
        Assert.Empty(Analyze("public static partial class Tests { [Verify] public static void M(int x) => Verify(x == 1).Because(\"why\"); }", rewrite: true));
    }

    [VerifyTest]
    public void VG0005_reports_a_Verify_method_in_a_type_that_is_not_partial()
    {
        var diagnostic = Assert.Single(Analyze("public partial class Outer { public class Tests { [Verify] public static void M(int x) => Verify(x == 1); } }"));

        Assert.Equal("VG0005", diagnostic.Id);
        Assert.Equal("'M' cannot get a tier 3 copy because 'Tests' is not partial. Add 'partial' to each type that contains the method.", diagnostic.GetMessage());
        Assert.Equal("M", Text(diagnostic));
    }

    [VerifyTest]
    [Row("public static void M(int[] values) => Verify(values.AsSpan().Length == 0);", "values.AsSpan()", "its type is a ref struct")]
    [Row("public static Counter Shared; public static void M() => Verify(Shared.Next() == 1);", "Shared", "it is a mutable struct, and a copy would take the change")]
    [Row("public static void M(int value) { unsafe { int* p = &value; Verify(p == null); } }", "p", "its type is a pointer")]
    public void VG0006_reports_a_part_that_cannot_be_captured(string members, string part, string reason)
    {
        var source = "public struct Counter { public int Value; public int Next() => ++Value; } public static partial class Tests { " +
                     members.Replace("public static void M", "[Verify] public static void M") + " }";

        var diagnostic = Assert.Single(Analyze(source, rewrite: true));

        Assert.Equal("VG0006", diagnostic.Id);
        Assert.Equal($"'{part}' is not captured, because {reason}. The failure message shows its text with the value unavailable.", diagnostic.GetMessage());
        Assert.Equal(part, Text(diagnostic));
        Assert.Empty(Analyze(source, rewrite: false));
    }

    [VerifyTest]
    [Row("static Tests() { Check(1 + 1 == 3); }", "Check in the static constructor of 'Tests'", "Verify")]
    [Row("~Tests() { Check(1 + 1 == 3); }", "Check in the finalizer of 'Tests'", "Verify")]
    [Row("[System.Runtime.CompilerServices.ModuleInitializer] internal static void Init() { Check(1 + 1 == 3); }", "Check in the module initializer 'Init'", "Verify")]
    [Row("static Tests() { CheckEquivalent(1, 2); }", "CheckEquivalent in the static constructor of 'Tests'", "VerifyEquivalent")]
    public void VG0007_reports_Check_where_no_scope_can_be_open(string members, string where, string instead)
    {
        var diagnostic = Assert.Single(Analyze("public class Tests { " + members + " }"), diagnostic => diagnostic.Id == "VG0007");

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal($"{where} runs where no VerifyScope can be open, so a failure throws at once. Use {instead} instead.", diagnostic.GetMessage());
    }

    [VerifyTest]
    public void VG0007_does_not_report_Check_in_a_method()
    {
        Assert.Empty(Analyze("public class Tests { public void M(int x) { Check(x == 1); } }"));
    }

    [VerifyTest]
    public void VG0008_reports_a_Verify_method_whose_file_declares_a_file_type()
    {
        var diagnostic = Assert.Single(Analyze("file sealed class Hidden { } public static partial class Tests { [Verify] public static void M(int x) => Verify(x == 1); }"));

        Assert.Equal("VG0008", diagnostic.Id);
        Assert.Equal("'M' is not rewritten, because its file declares the file type 'Hidden', which a copy in another file cannot see. It gets tier 2 messages.", diagnostic.GetMessage());
    }

    private static IReadOnlyList<Diagnostic> Analyze(string source, bool rewrite = false) =>
        GeneratorHarness.Analyze(Header + source, rewrite).Where(diagnostic => diagnostic.Id != "VG0002").ToList();

    private static string Text(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);
}
