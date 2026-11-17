namespace VerificationGlue.Tests.Specs;

/// <summary>
/// Containers at test, class and assembly scope, <c>[Mock]</c> parameters, parameter providers,
/// middleware and output, checked through the fixture's TRX results and its lifecycle log.
/// </summary>
public class ContainerSpecs
{
    private static readonly Lazy<string[]> FullLog = new(() =>
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>());
        Assert.True(result.ExitCode == 2, result.Output);
        return log;
    });

    private static readonly Lazy<Dictionary<string, TrxResult>> Results = new(TrxReport.Run);

    [Fact]
    public void Each_test_scoped_test_builds_and_disposes_its_own_container()
    {
        var first = DatabaseOf("Mock_reaches_the_test_scoped_service");
        var second = DatabaseOf("Service_without_a_mock_gets_the_registered_clock");

        Assert.NotEqual(first, second);
        Assert.Single(FullLog.Value, $"database {first} disposed");
        Assert.Single(FullLog.Value, $"database {second} disposed");
    }

    [Fact]
    public void A_class_container_is_shared_by_the_class_and_disposed_after_its_last_test()
    {
        var log = FullLog.Value;
        var database = DatabaseOf("Mock_reaches_the_class_scoped_service");

        Assert.Equal(database, DatabaseOf("Next_test_shares_the_class_database"));
        Assert.Single(log, $"database {database} disposed");
        LifecycleLog.AssertInOrder(
            log,
            "Class scope created: Fixture.ClassContainerTests",
            "container built: Fixture.ClassContainerTests",
            "Next_test_shares_the_class_database used database " + database,
            "Class scope disposing: Fixture.ClassContainerTests",
            $"database {database} disposed",
            "container disposed: Fixture.ClassContainerTests",
            "container built: Fixture.ContainerOwnedTests.Is_created_by_the_container()");
    }

    [Fact]
    public void An_assembly_container_is_shared_across_classes_and_disposed_last()
    {
        var log = FullLog.Value;
        var database = DatabaseOf("Mock_reaches_the_assembly_scoped_service");

        Assert.Equal(database, DatabaseOf("Other_class_shares_the_assembly_database"));
        Assert.Single(log, "container built: VerificationGlue.Tests.Fixture");
        Assert.Equal([$"database {database} disposed", "container disposed: VerificationGlue.Tests.Fixture"], log[^2..]);
    }

    [Fact]
    public void Mock_parameters_reach_the_system_under_test_at_every_scope()
    {
        var results = Results.Value;

        Assert.All(
            ["Mock_reaches_the_test_scoped_service", "Mock_reaches_the_class_scoped_service", "Mock_reaches_the_assembly_scoped_service", "Constructor_gets_the_mock_and_the_service"],
            name => Assert.True(results[name].Outcome == "Passed", name + ": " + results[name].Message));
    }

    [Fact]
    public void A_listener_override_reaches_the_service()
    {
        Assert.Equal("Passed", Results.Value["Listener_override_reaches_the_service"].Outcome);
    }

    [Fact]
    public void A_shared_container_without_child_overrides_fails_the_test_before_it_runs()
    {
        var result = Results.Value["Fails_before_the_test_runs"];

        Assert.Equal("Failed", result.Outcome);
        Assert.Contains("VGT0008", result.Message);
        Assert.Contains("PlainBootstrap is shared with Scope = Class", result.Message);
        Assert.DoesNotContain("Fails_before_the_test_runs ran", FullLog.Value);
    }

    [Fact]
    public void Parameters_come_from_the_narrowest_provider_then_the_container_then_the_default()
    {
        var results = Results.Value;

        Assert.Equal("Passed", results["Narrowest_provider_wins"].Outcome);
        Assert.Equal("Passed", results["Class_provider_supplies_the_parameter"].Outcome);
        Assert.Contains(
            "Test parameter 'clock' of type Fixture.IClock has no value. The providers tried were: the container (none, because no [Container<T>] applies), " +
            "built-in values (CancellationToken, TestContext, ITestOutput). It has no default value.",
            results["Needs_a_service"].Message);
    }

    [Fact]
    public void Output_written_while_the_container_builds_goes_to_the_test()
    {
        Assert.Equal(
            "building the Test container\nmiddleware enter\nthe test ran\nmiddleware leave\n",
            Results.Value["Writes_its_output_in_order"].Output);
    }

    [Fact]
    public void Check_failures_are_reported_with_their_own_test()
    {
        var results = Results.Value;
        var failed = results["Reports_every_failed_check"];

        Assert.Equal("Failed", failed.Outcome);
        Assert.Contains("2 checks failed", failed.Message);
        Assert.Contains("total == 3", failed.Message);
        Assert.Contains("total > 5", failed.Message);
        Assert.Equal("Passed", results["Starts_with_no_failed_checks"].Outcome);
    }

    private static string DatabaseOf(string test)
    {
        var prefix = test + " used database ";
        return Assert.Single(FullLog.Value, line => line.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];
    }
}
