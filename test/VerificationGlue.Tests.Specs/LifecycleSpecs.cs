namespace VerificationGlue.Tests.Specs;

/// <summary>The Phase 2 lifecycle acceptance cases from docs/design/tests.md.</summary>
public class LifecycleSpecs
{
    private const string CancelledTest = "Fixture.CancelledSetupTests.Is_cancelled_while_its_container_is_created()";
    private const string RetriedTest = "Fixture.RetryTests.Passes_on_the_second_attempt(Fixture.Database)";

    private static readonly Lazy<Dictionary<string, TrxResult>> Results = new(TrxReport.Run);

    [Fact]
    public void Container_owned_and_runner_owned_instances_are_disposed_exactly_once()
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>());

        Assert.True(result.ExitCode == 2, result.Output);
        Assert.Single(log, "runner-owned instance disposed");
        Assert.Single(log, "container-owned instance disposed");
        LifecycleLog.AssertInOrder(
            log,
            "runner-owned instance disposed",
            "container disposed: Fixture.RunnerOwnedTests.Constructor_gets_the_mock_and_the_service(Fixture.IClock)");
        LifecycleLog.AssertInOrder(
            log,
            "container-owned instance disposed",
            "container disposed: Fixture.ContainerOwnedTests.Is_created_by_the_container()");
    }

    [Fact]
    public void Cancellation_during_setup_disposes_what_setup_acquired()
    {
        var (result, log) = Fixture.RunLogged(
            new Dictionary<string, string> { ["VG_FIXTURE_HANG"] = "1" },
            "--filter-uid", CancelledTest, "--timeout", "2s");

        Assert.True(result.ExitCode == 3, result.Output);
        Assert.Equal(
            [
                "Class scope created: Fixture.CancelledSetupTests",
                "Test scope created: " + CancelledTest,
                "container built: Fixture.CancelledSetupTests",
                "child container waits",
                "Test scope disposing: " + CancelledTest,
                "Class scope disposing: Fixture.CancelledSetupTests",
                "container disposed: Fixture.CancelledSetupTests",
            ],
            log);
    }

    [Fact]
    public void Invocation_cleanup_and_check_failures_are_all_kept_in_order()
    {
        var result = Results.Value["Keeps_every_failure"];

        Assert.Equal("Failed", result.Outcome);
        var positions = new[]
        {
            "The test threw after a failed check.",
            "InvalidOperationException: The test instance failed to dispose.",
            "InvalidOperationException: The middleware failed on leaving.",
            "InvalidOperationException: The container failed to dispose.",
            "VerifyException: 1 check failed",
            "total == 3",
        }.Select(text => result.Message.IndexOf(text, StringComparison.Ordinal)).ToArray();

        Assert.True(positions[0] == 0, result.Message);
        Assert.True(positions.All(position => position >= 0), result.Message);
        Assert.Equal(positions.Order().ToArray(), positions);
        Assert.Contains("4 more failures after it:", result.Message);
    }

    [Fact]
    public void A_class_scope_that_fails_to_dispose_is_reported_as_its_own_node()
    {
        var results = Results.Value;

        Assert.Equal("Passed", results["Passes_before_its_class_container_fails_to_dispose"].Outcome);
        var scope = results[RunnerSpecs.ScopeNode];
        Assert.Equal("Failed", scope.Outcome);
        Assert.Contains("The container failed to dispose.", scope.Message);
    }

    [Fact]
    public void An_attribute_that_throws_fails_only_the_tests_it_belongs_to()
    {
        var results = Results.Value;

        Assert.All(["Broken_class_first", "Broken_class_second"], name =>
        {
            Assert.Equal("Failed", results[name].Outcome);
            Assert.Contains("The class scope Fixture.BrokenClassTests could not be created: The class attribute constructor failed.", results[name].Message);
        });
        Assert.Contains("The attribute constructor failed.", results["Has_an_attribute_that_throws"].Message);
        Assert.All(RunnerSpecs.Passing, name => Assert.Equal("Passed", results[name].Outcome));
    }

    [Fact]
    public void Each_retry_attempt_gets_fresh_state_and_its_own_cleanup()
    {
        var (result, log) = Fixture.RunLogged(
            new Dictionary<string, string> { ["VG_FIXTURE_RETRY"] = "1" },
            "--filter-uid", RetriedTest, "--retry-failed-tests", "2");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("Passed! after 2/3 attempts", result.Output);
        Assert.Equal(
            [
                "container built: " + RetriedTest,
                "database 1 created",
                "retry attempt 1: runs in this process 1, database 1",
                "database 1 disposed",
                "container disposed: " + RetriedTest,
                "container built: " + RetriedTest,
                "database 1 created",
                "retry attempt 2: runs in this process 1, database 1",
                "database 1 disposed",
                "container disposed: " + RetriedTest,
            ],
            log);
    }
}

internal static class LifecycleLog
{
    /// <summary>Asserts that each of <paramref name="lines"/> is in <paramref name="log"/>, in this order.</summary>
    public static void AssertInOrder(string[] log, params string[] lines)
    {
        var previous = -1;
        foreach (var line in lines)
        {
            var index = Array.IndexOf(log, line, previous + 1);
            Assert.True(index > previous, $"The log has no line '{line}' after line {previous}:\n" + string.Join('\n', log));
            previous = index;
        }
    }
}
