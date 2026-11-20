using System.Globalization;
using System.Text;

namespace Hardening.Specs;

/// <summary>The assertion and placement each expression runs in.</summary>
public enum Form
{
    /// <summary><c>Verify(e)</c> in a <c>[Verify]</c> method, called directly so the redirect runs its copy.</summary>
    VerifyRewritten,

    /// <summary><c>Check(e)</c> in a <c>[Verify]</c> method, called directly so the redirect runs its copy.</summary>
    CheckRewritten,

    /// <summary><c>Verify(e)</c> in an ordinary method.</summary>
    VerifyPlain,

    /// <summary><c>Check(e)</c> in an ordinary method.</summary>
    CheckPlain,

    /// <summary><c>Verify(() =&gt; e)</c> in an ordinary method.</summary>
    VerifyLambda,

    /// <summary><c>Check(() =&gt; e)</c> in an ordinary method.</summary>
    CheckLambda,

    /// <summary><c>Verify(e)</c> in a local function of a <c>[Verify]</c> method.</summary>
    VerifyLocalFunction,

    /// <summary><c>Verify(e)</c> twice, in an <c>if</c> without braces in a loop of a <c>[Verify]</c> method.</summary>
    VerifyLoop,

    /// <summary><c>Check(e)</c> twice, in an <c>if</c> without braces in a loop of a <c>[Verify]</c> method.</summary>
    CheckLoop,
}

/// <summary>The tier a form ran under. <see cref="Tier3Lambda"/> is the lambda check, which lists values like tier 3.</summary>
public enum Tier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
    Tier3Lambda = 4,
}

/// <summary>
/// The tier a form should run under with the generator and tier 3 on. <see cref="Fallback"/> names the
/// documented rule when the form falls back to tier 2 on purpose.
/// </summary>
public sealed record Expectation(Tier Tier, string? Fallback)
{
    public bool RequiresRewriting => Tier is Tier.Tier3 or Tier.Tier3Lambda;
}

public static class Forms
{
    public static IEnumerable<Form> For(ExpressionCase expression) =>
        Enum.GetValues<Form>().Where(form => !expression.IsAsync || form is not (Form.VerifyLambda or Form.CheckLambda));

    public static bool IsCheck(Form form) => form is Form.CheckRewritten or Form.CheckPlain or Form.CheckLambda or Form.CheckLoop;

    /// <summary>
    /// The baseline method a form compares with: <c>Once</c> evaluates the expression once,
    /// <c>LoopVerify</c> twice and stops at the first false, and <c>LoopCheck</c> twice and counts each false.
    /// </summary>
    public static string Baseline(Form form) => form switch
    {
        Form.VerifyLoop => "LoopVerify",
        Form.CheckLoop => "LoopCheck",
        _ => "Once",
    };

    /// <summary>The rules come from docs/guide/tiers.md.</summary>
    public static Expectation Expect(Form form, ExpressionCase expression) => form switch
    {
        Form.VerifyRewritten or Form.CheckRewritten or Form.VerifyLoop or Form.CheckLoop => new(Tier.Tier3, null),
        Form.VerifyPlain or Form.CheckPlain => new(Tier.Tier2, null),
        Form.VerifyLambda or Form.CheckLambda when expression.WritesLocal => new(Tier.Tier2, "a lambda that writes a captured variable gives the tier 2 message"),
        Form.VerifyLambda or Form.CheckLambda => new(Tier.Tier3Lambda, null),
        Form.VerifyLocalFunction => new(Tier.Tier2, "a call in a local function keeps its tier 2 message"),
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    public static string Describe(Tier tier) => tier == Tier.Tier3Lambda ? "tier 3 (lambda)" : $"tier {(int)tier}";
}

/// <summary>
/// Writes the two files a batch compiles: the cases with their assertions, and the plain C# baseline
/// with no VerificationGlue. Each method is one line, so a call site's line and column are known.
/// </summary>
public sealed class CaseSource
{
    public const string CasesPath = "/repo/Project/Cases.cs";
    public const string BaselinePath = "/repo/Baseline/Baseline.cs";

    private readonly Dictionary<(string Case, Form Form), (int Line, int Column)> _sites = [];

    public CaseSource(IReadOnlyList<ExpressionCase> cases, Func<string, string>? assertedText = null)
    {
        var text = new StringBuilder();
        var baseline = new StringBuilder();
        var line = 0;

        void Add(string value)
        {
            text.Append(value).Append('\n');
            line++;
        }

        Add("using System;");
        Add("using System.Threading.Tasks;");
        Add("using Hardening.Specs;");
        Add("using VerificationGlue;");
        Add("using static VerificationGlue.VerifySyntax;");
        Add("namespace Generated;");
        baseline.Append("using System;\nusing System.Threading.Tasks;\nusing Hardening.Specs;\nnamespace Baseline;\n");

        foreach (var expression in cases)
        {
            var e = assertedText?.Invoke(expression.Text) ?? expression.Text;
            var async = expression.IsAsync;
            Add($"public static partial class {expression.Name}");
            Add("{");
            foreach (var form in Forms.For(expression))
            {
                var method = Method(form, e, async);
                Add(method);
                _sites[(expression.Name, form)] = (line, method.LastIndexOf(Call(form, e), StringComparison.Ordinal) + 1);
                Add(async
                    ? $"    public static Task Run{form}(Probe p) => {form}(p);"
                    : $"    public static void Run{form}(Probe p) => {form}(p);");
            }

            Add("}");

            var signature = async ? "public static async Task" : "public static void";
            var x = expression.Text;
            baseline.Append(CultureInfo.InvariantCulture, $"public static class {expression.Name}\n{{\n")
                .Append(CultureInfo.InvariantCulture, $"    {signature} Once(Probe p) {{ var n = 0; try {{ if (!({x})) p.Failures++; }} finally {{ p.Local = n; }} }}\n")
                .Append(CultureInfo.InvariantCulture, $"    {signature} LoopVerify(Probe p) {{ var n = 0; try {{ for (var i = 0; i < 2; i++) if (i < 2) {{ if (!({x})) {{ p.Failures++; break; }} }} }} finally {{ p.Local = n; }} }}\n")
                .Append(CultureInfo.InvariantCulture, $"    {signature} LoopCheck(Probe p) {{ var n = 0; try {{ for (var i = 0; i < 2; i++) if (i < 2) {{ if (!({x})) p.Failures++; }} }} finally {{ p.Local = n; }} }}\n")
                .Append("}\n");
        }

        Text = text.ToString();
        Baseline = baseline.ToString();
    }

    public string Text { get; }

    public string Baseline { get; }

    /// <summary>The line and column of the <c>Verify</c> or <c>Check</c> call, which name its interceptor.</summary>
    public (int Line, int Column) Site(string name, Form form) => _sites[(name, form)];

    /// <summary>The source of one case's class, for a failure report.</summary>
    public string CaseText(string name)
    {
        var lines = Text.Split('\n');
        var start = Array.IndexOf(lines, $"public static partial class {name}");
        var end = Array.IndexOf(lines, "}", start);
        return string.Join('\n', lines[start..(end + 1)]);
    }

    private static string Method(Form form, string e, bool async)
    {
        var signature = async ? "public static async Task" : "public static void";
        var body = $"var n = 0; try {{ {Call(form, e)} }} finally {{ p.Local = n; }}";
        return form switch
        {
            Form.VerifyRewritten or Form.CheckRewritten => $"    [Verify] {signature} {form}(Probe p) {{ {body} }}",
            Form.VerifyLoop or Form.CheckLoop => $"    [Verify] {signature} {form}(Probe p) {{ var n = 0; try {{ for (var i = 0; i < 2; i++) if (i < 2) {Call(form, e)} }} finally {{ p.Local = n; }} }}",
            Form.VerifyLocalFunction when async => $"    [Verify] {signature} {form}(Probe p) {{ var n = 0; try {{ await Run(); }} finally {{ p.Local = n; }} async Task Run() {{ Verify({e}); }} }}",
            Form.VerifyLocalFunction => $"    [Verify] {signature} {form}(Probe p) {{ var n = 0; try {{ Run(); }} finally {{ p.Local = n; }} void Run() {{ Verify({e}); }} }}",
            _ => $"    {signature} {form}(Probe p) {{ {body} }}",
        };
    }

    private static string Call(Form form, string e) => form switch
    {
        Form.VerifyLambda => $"Verify(() => {e});",
        Form.CheckLambda => $"Check(() => {e});",
        _ when Forms.IsCheck(form) => $"Check({e});",
        _ => $"Verify({e});",
    };
}
