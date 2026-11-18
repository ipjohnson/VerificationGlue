namespace VerificationGlue.Tests.Specs;

/// <summary>
/// The fixture's <c>[Row]</c> tests: each row is its own result with its own container, a UID filter
/// selects one row, and display names come from the default format or a <c>[NameFormatter&lt;T&gt;]</c>.
/// </summary>
public class RowSpecs
{
    private const string Method = "Fixture.RowTests.Totals(System.Int32,System.Int32,Fixture.Database)";
    private const string First = Method + "#row(System.Int32:2,System.Int32:10)";
    private const string Second = Method + "#row(System.Int32:3,System.Int32:5)";

    private static readonly Lazy<Dictionary<string, TrxResult>> Results = new(TrxReport.Run);

    [Fact]
    public void Each_row_is_its_own_result_with_its_own_container()
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>(), "--filter-uid", First, "--filter-uid", Second);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("total: 2", result.Output);
        Assert.Contains("succeeded: 2", result.Output);
        LifecycleLog.AssertInOrder(log, "container built: " + First, "Totals(2, 10) used database 1", "database 1 disposed", "container disposed: " + First);
        LifecycleLog.AssertInOrder(log, "container built: " + Second, "Totals(3, 5) used database 2", "database 2 disposed", "container disposed: " + Second);
    }

    [Fact]
    public void A_uid_filter_on_one_row_runs_only_that_row()
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>(), "--filter-uid", Second);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("total: 1", result.Output);
        Assert.Contains("Totals(3, 5) used database 1", log);
        Assert.DoesNotContain(log, line => line.StartsWith("Totals(2, 10)", StringComparison.Ordinal));
    }

    [Fact]
    public void The_default_name_shows_each_row_value_with_its_parameter_name()
    {
        var results = Results.Value;

        Assert.Equal("Passed", results["Totals(qty: 2, price: 10)"].Outcome);
        Assert.Equal("Passed", results["Totals(qty: 3, price: 5)"].Outcome);
        Assert.Equal("Fixture.RowTests", results["Totals(qty: 2, price: 10)"].ClassName);
    }

    [Fact]
    public void A_name_formatter_on_the_class_writes_its_display_names()
    {
        Assert.Equal("Passed", Results.Value["WRAPS gift"].Outcome);
    }
}
