namespace VerificationGlue.Tests.Specs;

/// <summary>
/// The fixture's tests, run the ways a user runs them: the executable, <c>dotnet test</c> and
/// <c>dotnet run</c>, with <c>--filter-uid</c> and <c>--report-trx</c>.
/// </summary>
public class RunnerSpecs
{
    internal static readonly string[] Passing =
    [
        "A_creates_an_instance",
        "B_sees_the_first_instance_disposed",
        "Gets_the_cancellation_token",
        "Passes",
        "Passes_async",
        "Sees_its_attributes",
        "Static_test",
        "Uses_a_mock",
        "Class_provider_supplies_the_parameter",
        "Constructor_gets_the_mock_and_the_service",
        "Is_cancelled_while_its_container_is_created",
        "Is_created_by_the_container",
        "Listener_override_reaches_the_service",
        "Mock_reaches_the_assembly_scoped_service",
        "Mock_reaches_the_class_scoped_service",
        "Mock_reaches_the_test_scoped_service",
        "Narrowest_provider_wins",
        "Next_test_shares_the_class_database",
        "Other_class_shares_the_assembly_database",
        "Passes_before_its_class_container_fails_to_dispose",
        "Passes_on_the_second_attempt",
        "Service_without_a_mock_gets_the_registered_clock",
        "Starts_with_no_failed_checks",
        "Writes_its_output_in_order",
        "Totals(qty: 2, price: 10)",
        "Totals(qty: 3, price: 5)",
        "WRAPS gift",
        "Database_first",
        "Database_second",
        "Database_third",
        "Database_fourth",
        "Report_first",
        "Report_second",
        "Refunds_through_the_sandbox",
        "Charges_through_the_sandbox",
        "Meets_its_partner_first",
        "Meets_its_partner_second",
    ];

    /// <summary>The node the runner publishes when the class container of FailingClassDisposalTests fails to dispose.</summary>
    internal const string ScopeNode = "Disposing the class scope Fixture.FailingClassDisposalTests";

    internal static readonly string[] Failing =
    [
        "Fails_in_the_original",
        "Fails_in_the_rewritten_copy",
        "Has_an_attribute_that_throws",
        "Needs_a_service",
        "Throws",
        "Broken_class_first",
        "Broken_class_second",
        "Fails_before_the_test_runs",
        "Keeps_every_failure",
        "Reports_every_failed_check",
    ];

    [Fact]
    public void Discovers_every_test()
    {
        var result = Fixture.Run("--list-tests");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains($"found {Passing.Length + Failing.Length} test(s)", result.Output);
        Assert.All(Passing.Concat(Failing), name => Assert.Contains("  " + name + "\n", result.Output.Replace("\r\n", "\n")));
    }

    [Fact]
    public void Runs_only_the_tests_the_uid_filter_selects()
    {
        var result = Fixture.Run("--filter-uid", "Fixture.PassingTests.Passes()", "--filter-uid", "Fixture.PassingTests.Gets_the_cancellation_token(System.Threading.CancellationToken)");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("total: 2", result.Output);
        Assert.Contains("succeeded: 2", result.Output);
    }

    [Fact]
    public void Reports_each_outcome_in_the_trx_file()
    {
        var results = Trx();

        Assert.Equal(Passing.Concat(Failing).Append(ScopeNode).Order(), results.Keys.Order());
        Assert.All(Passing, name => Assert.Equal("Passed", results[name].Outcome));
        Assert.All(Failing, name => Assert.Equal("Failed", results[name].Outcome));
        Assert.Equal("Fixture.PassingTests", results["Passes"].ClassName);
        Assert.Contains("Test parameter 'clock' of type Fixture.IClock has no value.", results["Needs_a_service"].Message);
        Assert.Contains("The attribute constructor failed.", results["Has_an_attribute_that_throws"].Message);
    }

    [Fact]
    public void Calls_the_rewritten_copy_when_WillRewrite_is_true()
    {
        var results = Trx();

        var rewritten = results["Fails_in_the_rewritten_copy"];
        Assert.Contains("Total(quantity) → 3", rewritten.Message);
        Assert.Contains("Fails_in_the_rewritten_copy__Verified", rewritten.StackTrace);

        var original = results["Fails_in_the_original"];
        Assert.Contains("expected Total(quantity) to equal 5", original.Message);
        Assert.DoesNotContain("__Verified", original.StackTrace);
    }

    [Fact]
    public void Exits_with_2_when_a_test_fails()
    {
        var result = Fixture.Run("--filter-uid", "Fixture.FailingTests.Throws()");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("The test threw.", result.Output);
    }

    [Fact]
    public void Dotnet_test_runs_the_tests_the_uid_filter_selects()
    {
        var result = Fixture.Dotnet(
            Fixture.RepositoryRoot,
            "test", "--project", Fixture.ProjectPath, "-c", Fixture.Configuration, "--no-build",
            "--filter-uid", "Fixture.PassingTests.Passes_async()");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("total: 1", result.Output);
        Assert.Contains("succeeded: 1", result.Output);
    }

    [Fact]
    public void Dotnet_run_runs_the_tests_the_uid_filter_selects()
    {
        var result = Fixture.Dotnet(
            Fixture.RepositoryRoot,
            "run", "--project", Fixture.ProjectPath, "-c", Fixture.Configuration, "--no-build", "--",
            "--filter-uid", "Fixture.PassingTests.Static_test()");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("total: 1", result.Output);
        Assert.Contains("succeeded: 1", result.Output);
    }

    [Fact]
    public void Dotnet_test_without_the_runner_setting_reports_VGT1001()
    {
        var directory = Directory.CreateTempSubdirectory("vgt1001-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "global.json"), SdkOnlyGlobalJson());

            var result = Fixture.Dotnet(directory.FullName, "test", Fixture.ProjectPath, "-c", Fixture.Configuration, "--no-build", "--no-restore");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("error VGT1001", result.Output);
            Assert.Contains("\"test\": { \"runner\": \"Microsoft.Testing.Platform\" }", result.Output);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static string SdkOnlyGlobalJson()
    {
        var json = File.ReadAllText(Path.Combine(Fixture.RepositoryRoot, "global.json"));
        var sdk = System.Text.Json.Nodes.JsonNode.Parse(json)!["sdk"]!.ToJsonString();
        return "{ \"sdk\": " + sdk + " }";
    }

    private static Dictionary<string, TrxResult> Trx() => TrxReport.Run();
}
