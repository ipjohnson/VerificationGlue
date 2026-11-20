namespace VerificationGlue.Tests.Specs;

/// <summary>
/// Parameter resolution when a source throws, the choice of constructor, cancellation of the test's
/// token, override ownership, and what a filtered-out class creates.
/// </summary>
public class ResolutionSpecs
{
    private const string ProviderTest = "Fixture.ThrowingSourceTests.Provider_throws_while_resolving(Fixture.Database,System.String)";
    private const string ContainerTest = "Fixture.ThrowingSourceTests.Container_throws_while_resolving(Fixture.Database,Fixture.BrokenService)";
    private const string ConstructorTest = "Fixture.ThrowingConstructorSourceTests.Class_provider_throws_for_a_constructor_parameter()";
    private const string CancelledTest = "Fixture.CancellationTests.Waits_for_the_run_to_be_cancelled(System.Threading.CancellationToken,VerificationGlue.Tests.TestContext)";
    private const string SelectedTest = "Fixture.TestContainerTests.Mock_reaches_the_test_scoped_service(Fixture.IClock,Fixture.OrderService,Fixture.Database)";
    private const string OwnershipTest = "Fixture.OverrideOwnershipTests.Override_is_left_to_its_owner(Fixture.OrderService)";

    private static readonly Lazy<Dictionary<string, TrxResult>> Results = new(TrxReport.Run);

    [Theory]
    [InlineData("Provider_throws_while_resolving", "Test parameter 'name' of type System.String has no value, because ThrowingProviderAttribute on the parameter threw InvalidOperationException: The provider failed.")]
    [InlineData("Container_throws_while_resolving", "Test parameter 'service' of type Fixture.BrokenService has no value, because the container (FixtureContainer) threw InvalidOperationException: The container failed to create BrokenService.")]
    [InlineData("Class_provider_throws_for_a_constructor_parameter", "Constructor parameter 'name' of type System.String has no value, because ThrowingProviderAttribute threw InvalidOperationException: The provider failed.")]
    public void A_source_that_throws_fails_the_test_and_names_the_parameter_and_the_source(string test, string message)
    {
        var result = Results.Value[test];

        Assert.Equal("Failed", result.Outcome);
        Assert.Equal(message, result.Message);
    }

    [Fact]
    public void A_source_that_throws_skips_the_body_and_cleanup_still_runs()
    {
        var (result, log) = Fixture.RunLogged(
            new Dictionary<string, string>(),
            "--filter-uid", ProviderTest, "--filter-uid", ContainerTest, "--filter-uid", ConstructorTest);

        Assert.True(result.ExitCode == 2, result.Output);
        Assert.DoesNotContain(log, line => line.EndsWith(" ran", StringComparison.Ordinal) || line.Contains(" ran with ", StringComparison.Ordinal));
        Assert.DoesNotContain("ThrowingConstructorSourceTests disposed", log);
        foreach (var test in new[] { ProviderTest, ContainerTest })
        {
            LifecycleLog.AssertInOrder(log, "Test scope created: " + test, "container built: " + test, "Test scope disposing: " + test, "container disposed: " + test);
        }

        LifecycleLog.AssertInOrder(log, "container built: " + ConstructorTest, "container disposed: " + ConstructorTest);
        var created = log.Where(line => line.StartsWith("database ", StringComparison.Ordinal) && line.EndsWith(" created", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, created.Length);
        Assert.All(created, line => Assert.Single(log, line.Replace(" created", " disposed", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_runner_uses_the_accessible_constructor_with_the_most_parameters()
    {
        var result = Results.Value["Uses_the_accessible_constructor_with_the_most_parameters"];

        Assert.True(result.Outcome == "Passed", result.Message);
    }

    [Fact]
    public void The_test_token_is_cancelled_when_the_run_is_cancelled()
    {
        var (result, log) = Fixture.RunLogged(
            new Dictionary<string, string> { ["VG_FIXTURE_HANG"] = "1" },
            "--filter-uid", CancelledTest, "--timeout", "2s");

        Assert.True(result.ExitCode == 3, result.Output);
        Assert.Equal(["the test waits on its token", "the test's token is cancelled: True"], log);
    }

    [Theory]
    [InlineData("--filter-uid", SelectedTest)]
    [InlineData("--vg-filter", "name:Mock_reaches_the_test_scoped_service")]
    public void A_filtered_out_class_creates_no_scope_and_no_container(string option, string value)
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>(), option, value);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Equal(
            [
                "container built: " + SelectedTest,
                "database 1 created",
                "Mock_reaches_the_test_scoped_service used database 1",
                "database 1 disposed",
                "container disposed: " + SelectedTest,
            ],
            log);
    }

    [Fact]
    public void A_disposable_override_is_disposed_by_its_owner_and_not_by_the_container()
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>(), "--filter-uid", OwnershipTest);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Single(log, "owned clock disposed");
        LifecycleLog.AssertInOrder(log, "owned clock disposed", "container disposed: " + OwnershipTest);
    }
}
