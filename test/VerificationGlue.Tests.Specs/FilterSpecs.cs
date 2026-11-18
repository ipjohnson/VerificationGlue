namespace VerificationGlue.Tests.Specs;

/// <summary>
/// <c>--vg-filter</c> on the fixture: selection by name, trait and schedule group, an expression that
/// matches nothing, an invalid expression, and a retry of a filtered run.
/// </summary>
public class FilterSpecs
{
    private static readonly string[] Database = ["Database_first", "Database_fourth", "Database_second", "Database_third"];

    [Theory]
    [InlineData("name:Checkout_*", new[] { "Checkout_adds_tax", "Checkout_charges_the_card" })]
    [InlineData("name:Fixture.RefundFilterTests.*", new[] { "Refund_reverses_the_charge" })]
    [InlineData("name:Totals(qty: 2, *)", new[] { "Totals(qty: 2, price: 10)" })]
    public void A_name_filter_selects_by_method_full_or_display_name(string filter, string[] expected) =>
        Assert.Equal(expected, Listed(filter));

    [Theory]
    [InlineData("trait:Category=Checkout", new[] { "Checkout_adds_tax", "Checkout_charges_the_card" })]
    [InlineData("trait:Speed=Slow", new[] { "Checkout_charges_the_card", "Refund_reverses_the_charge" })]
    [InlineData("trait:Category", new[] { "Checkout_adds_tax", "Checkout_charges_the_card", "Refund_reverses_the_charge" })]
    [InlineData("trait:Category=Checkout & !trait:Speed=Slow", new[] { "Checkout_adds_tax" })]
    public void A_trait_filter_selects_by_the_traits_of_the_class_and_the_method(string filter, string[] expected) =>
        Assert.Equal(expected, Listed(filter));

    [Fact]
    public void A_group_filter_selects_the_tests_of_a_schedule_group() =>
        Assert.Equal(Database, Listed("group:data*"));

    [Fact]
    public void Alternatives_run_every_test_that_matches_one_of_them()
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>(), "--vg-filter", "group:reports | name:Checkout_adds_tax");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("total: 3", result.Output);
        Assert.Equal(["filter ran Checkout_adds_tax in attempt 1"], log.Where(line => line.StartsWith("filter ran ", StringComparison.Ordinal)));
        Assert.Equal(2, log.Count(line => line.StartsWith("end reports ", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_filter_that_matches_nothing_runs_no_test_and_fails_the_run()
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>(), "--vg-filter", "name:No_such_test");

        Assert.True(result.ExitCode == 8, result.Output);
        Assert.Contains("Zero tests ran", result.Output);
        Assert.Empty(log);
    }

    [Theory]
    [InlineData("colour:red", "the term 'colour:red' does not start with name:, trait: or group:.")]
    [InlineData("name:Checkout* &", "a term is empty.")]
    [InlineData("trait:=Slow", "the term 'trait:=Slow' has no trait name after 'trait:'.")]
    [InlineData("group:", "the term 'group:' has no group after 'group:'.")]
    public void An_invalid_filter_fails_the_run_with_a_message_and_runs_no_test(string filter, string message)
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>(), "--vg-filter", filter);

        Assert.True(result.ExitCode == 5, result.Output);
        Assert.Contains($"The --vg-filter value '{filter}' is not valid: {message}", result.Output);
        Assert.Empty(log);
    }

    [Fact]
    public void A_retry_of_a_filtered_run_runs_only_the_failed_test_of_the_selection()
    {
        var (result, log) = Fixture.RunLogged(
            new Dictionary<string, string> { ["VG_FIXTURE_FILTER_FAIL_FIRST"] = "1" },
            "--vg-filter", "trait:Category=Checkout", "--retry-failed-tests", "2");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("Passed! after 2/3 attempts", result.Output);
        Assert.Equal(
            ["filter ran Checkout_adds_tax in attempt 1", "filter ran Checkout_charges_the_card in attempt 1", "filter ran Checkout_charges_the_card in attempt 2"],
            log.Where(line => line.StartsWith("filter ran ", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }

    /// <summary>The display names that <c>--list-tests</c> prints for <paramref name="filter"/>, in order.</summary>
    private static string[] Listed(string filter)
    {
        var result = Fixture.Run("--list-tests", "--vg-filter", filter);
        Assert.True(result.ExitCode == 0, result.Output);
        return result.Output.Split('\n')
            .TakeWhile(line => !line.StartsWith("Test discovery summary", StringComparison.Ordinal))
            .Where(line => line.StartsWith("  ", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
