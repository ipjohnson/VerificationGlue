namespace VerificationGlue.Tests.AspNetCore.Specs;

/// <summary>
/// <c>[WebApp&lt;TEntry&gt;]</c> at test scope, checked through the TRX results of
/// test/VerificationGlue.Tests.AspNetCore.Fixture, whose tests start test/VerificationGlue.Tests.AspNetCore.FixtureShop.
/// </summary>
public class WebAppSpecs
{
    private const string Started = "FixtureShop started in ";
    private const string StartsAtOnce = "Apps_that_start_at_once_get_their_own_host_and_overrides";
    private const string Returned = "[Information] FixtureShop: The entry point of FixtureShop is returning";

    [Fact]
    public void A_test_calls_an_endpoint_with_its_HttpClient_and_reads_the_response()
    {
        AssertPassed("Reads_the_response_of_an_endpoint");
    }

    [Fact]
    public void Each_test_gets_its_own_app()
    {
        AssertPassed("First_app_lists_only_its_own_order");
        AssertPassed("Second_app_lists_only_its_own_order");
        Assert.All(
            Fixture.Results.Where(result => result.Value.Outcome == "Passed" && result.Key != StartsAtOnce),
            result => Assert.Single(Lines(result.Value.Output), line => line.StartsWith(Started, StringComparison.Ordinal)));
    }

    [Fact]
    public void A_mock_parameter_is_the_instance_that_an_endpoint_and_a_controller_receive()
    {
        AssertPassed("Minimal_api_endpoint_receives_the_mock");
        AssertPassed("Controller_receives_the_mock");
    }

    [Fact]
    public void A_test_takes_a_service_of_the_app()
    {
        AssertPassed("Takes_a_service_of_the_app");
    }

    [Fact]
    public void A_scoped_service_parameter_is_disposed_when_the_test_ends_while_the_app_runs()
    {
        var name = "Takes_a_scoped_service_that_is_disposed_when_the_test_ends";
        AssertPassed(name);

        var lines = Lines(Fixture.Results[name].Output);
        var disposed = Array.IndexOf(lines, "[Information] FixtureShop.Basket: Basket disposed while the app runs");
        Assert.True(disposed >= 0, Fixture.Results[name].Output);
        Assert.DoesNotContain(lines, line => line.Contains("Basket disposed while the app stops", StringComparison.Ordinal));
        Assert.True(disposed < Array.IndexOf(lines, Returned));
    }

    [Fact]
    public void The_app_does_not_watch_its_configuration_files()
    {
        AssertPassed("Does_not_watch_its_configuration_files");
    }

    [Fact]
    public void Apps_that_start_at_once_get_their_own_host_and_overrides()
    {
        AssertPassed(StartsAtOnce);
        Assert.Contains("Dynamic code: True.", Fixture.Results[StartsAtOnce].Output);
    }

    [Fact]
    public void Without_dynamic_code_apps_that_start_at_once_build_their_hosts_one_at_a_time()
    {
        var result = Fixture.SerialResults[StartsAtOnce];

        Assert.True(result.Outcome == "Passed", $"{result.Outcome}\n{result.Message}\n{result.Output}");
        Assert.Contains("Dynamic code: False. Most entry points before Build at once: 1.", result.Output);
    }

    [Fact]
    public void Without_dynamic_code_the_other_tests_have_the_same_outcomes()
    {
        Assert.Equal(
            Fixture.Results.ToDictionary(result => result.Key, result => result.Value.Outcome),
            Fixture.SerialResults.ToDictionary(result => result.Key, result => result.Value.Outcome));
    }

    [Fact]
    public void A_failing_test_lists_the_log_of_its_own_app()
    {
        var result = Fixture.Results["Fails_after_saving_an_order"];

        Assert.Equal("Failed", result.Outcome);
        Assert.Contains("order.Quantity == 8", result.Message);
        Assert.Contains("[Information] FixtureShop.Orders: Saved order 1 of 7", Lines(result.Output));
        Assert.DoesNotContain(Lines(result.Output), line => line.Contains("of 2", StringComparison.Ordinal));
        Assert.Single(Lines(result.Output), line => line.StartsWith(Started, StringComparison.Ordinal) && line.EndsWith(" ms", StringComparison.Ordinal));
    }

    [Fact]
    public void The_entry_point_has_returned_before_the_result_is_published()
    {
        Assert.All(
            ["Reads_the_response_of_an_endpoint", "Minimal_api_endpoint_receives_the_mock", "Fails_after_saving_an_order"],
            name => Assert.Equal(Returned, Lines(Fixture.Results[name].Output)[^1]));
    }

    [Fact]
    public void An_app_that_throws_while_it_starts_fails_its_test_with_the_exception_of_the_app()
    {
        var result = Fixture.Results["Fails_with_the_exception_of_the_app"];

        Assert.Equal("Failed", result.Outcome);
        Assert.Equal("The connection string 'Shop' is missing.", result.Message);
        Assert.Contains("FixtureShop/Program.cs:line 8", result.StackTrace.Replace('\\', '/'));
    }

    [Fact]
    public void An_app_that_returns_before_it_builds_a_host_fails_its_test_with_the_exit_code()
    {
        var result = Fixture.Results["Fails_with_the_exit_code_of_the_app"];

        Assert.Equal("Failed", result.Outcome);
        Assert.Contains(
            "The entry point of FixtureShop returned 2 before it built a host. What the app wrote to standard error went to the console of the test process.",
            result.Message);
    }

    [Fact]
    public void The_other_tests_run_after_an_app_fails_to_start()
    {
        Assert.Equal(12, Fixture.Results.Count);
        Assert.Equal(9, Fixture.Results.Values.Count(result => result.Outcome == "Passed"));
    }

    private static void AssertPassed(string test)
    {
        var result = Fixture.Results[test];
        Assert.True(result.Outcome == "Passed", $"{test}: {result.Outcome}\n{result.Message}\n{result.Output}");
    }

    private static string[] Lines(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
