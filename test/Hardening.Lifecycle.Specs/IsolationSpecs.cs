namespace Hardening.Lifecycle.Specs;

/// <summary>A later test inherits nothing from an earlier one (LIFE-015).</summary>
public class IsolationSpecs
{
    private const string First = "Lifecycle.StaleState.A_leaves_pending_state(Lifecycle.ICalculator,VerificationGlue.Tests.ITestOutput)";
    private const string Second = "Lifecycle.StaleState.B_sees_no_pending_state(Lifecycle.ICalculator,VerificationGlue.Tests.TestContext)";

    /// <summary>
    /// The first test fails a Check, leaves a mock call with no Returns, two queued matchers and a
    /// pending Received on a mock the class container shares, writes output and throws. The second
    /// test runs after it ends and passes only when it sees none of that.
    /// </summary>
    [Fact]
    public void A_later_test_sees_no_failed_checks_pending_mock_state_output_or_context_of_an_earlier_one()
    {
        var run = LifecycleFixture.Run("", "name:Lifecycle.StaleState.*");

        Assert.True(run.ExitCode == 2, run.ToString());
        var first = run.Results["Lifecycle.StaleState/A_leaves_pending_state"];
        Assert.Equal("Failed", first.Outcome);
        Assert.StartsWith("A left pending state.", first.Message, StringComparison.Ordinal);
        Assert.Contains("VerifyException: 1 check failed", first.Message, StringComparison.Ordinal);
        Assert.Contains("output of A", first.Output, StringComparison.Ordinal);

        var second = run.Results["Lifecycle.StaleState/B_sees_no_pending_state"];
        Assert.True(second.Outcome == "Passed", second.Message);
        Assert.DoesNotContain("output of A", second.Output, StringComparison.Ordinal);

        var released = Array.IndexOf(run.Log, "release listener test " + First);
        var started = Array.IndexOf(run.Log, "acquire listener test " + Second);
        Assert.True(released >= 0 && started > released, "The second test must start after the first one ends.\n" + run);
    }
}
