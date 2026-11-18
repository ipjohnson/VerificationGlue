namespace VerificationGlue.Tests.Specs;

/// <summary>
/// The Phase 3 data-row gate from docs/design/tests.md, run on the fixture's RowSourceTests. Each test ID
/// selects the same row in a fresh process, including a retry by Microsoft.Testing.Extensions.Retry, after
/// its source reorders its rows. A source that throws, is empty, repeats a key or has unkeyed rows without
/// deterministic replay fails discovery and runs no row.
/// </summary>
public class RowSourceSpecs
{
    private const string Ships = "Fixture.RowSourceTests.Ships(System.Int32,System.String)";
    private const string Bills = "Fixture.RowSourceTests.Bills(Fixture.Customer)";
    private const string Pays = "Fixture.RowSourceTests.Pays(Fixture.Payment)";
    private const string Packs = "Fixture.RowSourceTests.Packs(System.String)";

    /// <summary>The row IDs of each test, the first row first. The fixture fails the first row on its first attempt.</summary>
    private static readonly Dictionary<string, string[]> Rows = new()
    {
        ["orders"] = [Ships + "#row(System.Int32:1,System.String:\"pen\")", Ships + "#row(System.Int32:2,System.String:\"ink\")", Ships + "#row(System.Int32:3,System.String:\"pad\")"],
        ["customers"] = [Bills + "#key(\"ann\")", Bills + "#key(\"bob\")", Bills + "#key(\"cy\")"],
        ["payments"] = [Pays + "#index(0)", Pays + "#index(1)"],
        ["literal"] = [Packs + "#row(System.String:\"gift\")", Packs + "#row(System.String:\"box\")"],
    };

    /// <summary>The value each test logs for its first row.</summary>
    private static readonly Dictionary<string, string> FirstRow = new()
    {
        ["orders"] = "1 pen",
        ["customers"] = "ann",
        ["payments"] = "card",
        ["literal"] = "gift",
    };

    [Theory]
    [InlineData("orders")]
    [InlineData("customers")]
    public void A_uid_filter_selects_the_same_row_after_the_source_reorders(string source)
    {
        var (result, log) = Run(source + "=reorder", from: 1, failFirst: false, "--filter-uid", Rows[source][0]);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("total: 1", result.Output);
        Assert.Equal([$"{source} ran {FirstRow[source]} in attempt 1"], Ran(log));
    }

    [Theory]
    [InlineData("orders", "reorder")]
    [InlineData("customers", "reorder")]
    [InlineData("payments", null)]
    [InlineData("literal", null)]
    public void A_fresh_process_retry_runs_the_intended_row(string source, string? mode)
    {
        var (result, log) = Run(mode is null ? null : source + "=" + mode, from: 2, failFirst: true, [.. Filter(source), "--retry-failed-tests", "2"]);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("Passed! after 2/3 attempts", result.Output);
        Assert.Equal(Rows[source].Length + 1, Ran(log).Length);
        Assert.Equal([$"{source} ran {FirstRow[source]} in attempt 2"], Ran(log).Where(line => line.EndsWith(" in attempt 2", StringComparison.Ordinal)));
        if (mode == "reorder")
        {
            Assert.NotEqual(Order(log, source, 1), Order(log, source, 2));
        }
    }

    [Theory]
    [MemberData(nameof(InvalidSources))]
    public void An_invalid_source_fails_discovery_and_runs_no_row(string source, string mode, string message)
    {
        var (listed, _) = Run(source + "=" + mode, from: 1, failFirst: false, "--list-tests");
        var (result, log) = Run(source + "=" + mode, from: 1, failFirst: false, "--filter-uid", Rows[source][0]);

        Assert.True(listed.ExitCode == 2, listed.Output);
        Assert.Contains(message, listed.Output);
        Assert.True(result.ExitCode == 2, result.Output);
        Assert.Contains(message, result.Output);
        Assert.Empty(Ran(log));
    }

    [Theory]
    [MemberData(nameof(InvalidSources))]
    public void A_fresh_process_retry_fails_when_the_source_becomes_invalid(string source, string mode, string message)
    {
        var (result, log) = Run(source + "=" + mode, from: 2, failFirst: true, [.. Filter(source), "--retry-failed-tests", "2"]);

        Assert.True(result.ExitCode == 2, result.Output);
        Assert.Contains("Failed! after 3/3 attempts", result.Output);
        Assert.Contains(message, result.Output);
        Assert.All(Ran(log), line => Assert.EndsWith(" in attempt 1", line));
    }

    public static TheoryData<string, string, string> InvalidSources => new()
    {
        { "customers", "throw", $"The data source CustomersAttribute of {Bills} threw while it listed its rows: The customers source is offline." },
        { "customers", "empty", $"The data source CustomersAttribute of {Bills} gave no rows." },
        { "customers", "duplicate", $"The data source CustomersAttribute of {Bills} gives the key \"ann\" to more than one row." },
        { "customers", "unkeyed", $"Row 0 of the data source CustomersAttribute of {Bills} has a value that is not a primitive, a string or an enum, and the row has no TestRow.Key." },
        { "orders", "throw", $"The data source [RowSource(\"Orders\")] of {Ships} threw while it listed its rows: The orders source is offline." },
        { "orders", "empty", $"The data source [RowSource(\"Orders\")] of {Ships} gave no rows." },
    };

    private static (ProcessResult Result, string[] Log) Run(string? mode, int from, bool failFirst, params string[] arguments)
    {
        var environment = new Dictionary<string, string> { ["VG_FIXTURE_SOURCE_FROM"] = from.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        if (mode is not null)
        {
            environment["VG_FIXTURE_SOURCE_MODE"] = mode;
        }

        if (failFirst)
        {
            environment["VG_FIXTURE_SOURCE_FAIL_FIRST"] = "1";
        }

        return Fixture.RunLogged(environment, arguments);
    }

    private static string[] Filter(string source) => [.. Rows[source].SelectMany(uid => new[] { "--filter-uid", uid })];

    /// <summary>The lines the fixture's tests log when a row runs.</summary>
    private static string[] Ran(string[] log) => [.. log.Where(line => line.Contains(" ran ", StringComparison.Ordinal))];

    /// <summary>The rows in the order the source gave them in <paramref name="process"/>.</summary>
    private static string Order(string[] log, string source, int process)
    {
        var prefix = $"source {source} read in process {process}: ";
        return log.Single(line => line.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];
    }
}
