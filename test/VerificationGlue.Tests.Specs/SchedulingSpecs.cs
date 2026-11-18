namespace VerificationGlue.Tests.Specs;

/// <summary>
/// Schedule groups, resources and <c>[RunAfter]</c> under the default scheduler, read from the
/// "start" and "end" lines that the fixture's timed tests log.
/// </summary>
public class SchedulingSpecs
{
    private static readonly Lazy<string[]> FullLog = new(() =>
    {
        var (result, log) = Fixture.RunLogged(new Dictionary<string, string>());
        Assert.True(result.ExitCode == 2, result.Output);
        return log;
    });

    [Fact]
    public void A_group_never_runs_more_tests_at_once_than_its_limit()
    {
        Assert.Equal(4, FullLog.Value.Count(line => line.StartsWith("end database ", StringComparison.Ordinal)));
        Assert.Equal(2, MostAtOnce(FullLog.Value, "database"));
    }

    [Fact]
    public void Tests_that_share_a_resource_never_overlap()
    {
        Assert.Equal(2, FullLog.Value.Count(line => line.StartsWith("end payments ", StringComparison.Ordinal)));
        Assert.Equal(1, MostAtOnce(FullLog.Value, "payments"));
    }

    [Fact]
    public void RunAfter_holds_a_group_until_the_named_group_finishes()
    {
        var log = FullLog.Value;
        var lastDatabaseEnd = Array.FindLastIndex(log, line => line.StartsWith("end database ", StringComparison.Ordinal));
        var firstReportStart = Array.FindIndex(log, line => line.StartsWith("start reports ", StringComparison.Ordinal));

        Assert.True(lastDatabaseEnd >= 0 && firstReportStart > lastDatabaseEnd, string.Join('\n', log));
        Assert.Equal(2, log.Count(line => line.StartsWith("end reports ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Tests_of_the_default_group_run_at_the_same_time()
    {
        var results = TrxReport.Run();

        Assert.Equal("Passed", results["Meets_its_partner_first"].Outcome);
        Assert.Equal("Passed", results["Meets_its_partner_second"].Outcome);
    }

    /// <summary>The most tests of <paramref name="group"/> that were between their start and end lines at once.</summary>
    private static int MostAtOnce(string[] log, string group)
    {
        int running = 0, most = 0;
        foreach (var line in log)
        {
            if (line.StartsWith($"start {group} ", StringComparison.Ordinal))
            {
                most = Math.Max(most, ++running);
            }
            else if (line.StartsWith($"end {group} ", StringComparison.Ordinal))
            {
                running--;
            }
        }

        return most;
    }
}
