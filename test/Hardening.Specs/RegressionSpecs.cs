namespace Hardening.Specs;

/// <summary>
/// The deterministic suite. Each row runs one expression in every form, as a generated case does. A
/// shrunk mismatch from a generated run prints the row to add here.
/// </summary>
public class RegressionSpecs
{
    [VerifyTest]
    [Row("p.B(1, false) && p.Fail(2)")]
    [Row("p.B(1, true) || p.Fail(2)")]
    [Row("p.B(1, true) ? p.B(2, false) : p.Fail(3)")]
    [Row("(p.N(1, null) ?? p.Bump(2)) == 1")]
    [Row("(p.N(1, 2) ?? p.Bump(2)) == 2")]
    [Row("(p.S(1, null)?.Length ?? 2) > p.State")]
    [Row("p.O(1, 2) is { V: 2 } && p.S(2, \"ab\") is { Length: > 1 }")]
    [Row("p.Try(1, 2, out var o1) && o1 > 1")]
    [Row("p.Bump(1) + p.Bump(2) == 3 && p.State == 2")]
    [Row("p.I(1, 4) / p.I(2, 0) > 0")]
    [Row("p.I(1, 1) + n++ == n")]
    [Row("await p.A(1, 2) == 2 && await p.A(2, 3) > 3")]
    [Row("p.B(1, false) && await p.A(2, 1) > 0")]
    [Row("p.B(1, true) && await p.Cancelled(2) > 0")]
    [Row("p.Bump(1) == 1")]
    [Row("p.I(1, 0) == n++")]
    public async Task A_regression_case_keeps_plain_CSharp_behavior(string expression)
    {
        var results = await CaseRunner.RunAsync([new ExpressionCase("Regression", expression)]);

        Assert.All(results, result => Assert.True(result.Classification != Classification.Mismatch, $"{result.Form}: {result.MismatchKind} mismatch: {result.Detail}"));
    }
}
