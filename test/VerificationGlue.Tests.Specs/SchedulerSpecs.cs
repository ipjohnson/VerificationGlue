using System.Xml.Linq;

namespace VerificationGlue.Tests.Specs;

/// <summary>
/// A custom <see cref="ITestScheduler"/>: samples/TestsScheduling sets <c>[assembly: Scheduler&lt;ShardScheduler&gt;]</c>,
/// which runs every second test when <c>VG_SHARD</c> is set.
/// </summary>
public class SchedulerSpecs
{
    private static readonly string[] Tests =
    [
        "Charges_a_card",
        "Deletes_an_order",
        "Loads_an_order",
        "Refunds_a_card",
        "Saves_an_order",
        "Starts_after_the_database_tests",
    ];

    private static readonly Lazy<string> Built = new(() =>
    {
        var project = Path.Combine(Fixture.RepositoryRoot, "samples", "TestsScheduling", "TestsScheduling.csproj");
        var build = Fixture.Dotnet(Fixture.RepositoryRoot, "build", project, "-c", Fixture.Configuration);
        Assert.True(build.ExitCode == 0, build.Output);
        return Path.Combine(Fixture.RepositoryRoot, "samples", "TestsScheduling", "bin", Fixture.Configuration, "net10.0");
    });

    [Fact]
    public void Without_a_shard_the_custom_scheduler_runs_every_test()
    {
        var results = Run(null);

        Assert.Equal(Tests, results.Keys.Order());
        Assert.All(Tests, name => Assert.Equal("Passed", results[name]));
    }

    [Fact]
    public void Each_shard_runs_its_half_and_reports_the_rest_as_skipped()
    {
        var first = Run("0/2");
        var second = Run("1/2");

        var ranFirst = first.Where(result => result.Value == "Passed").Select(result => result.Key).ToHashSet();
        var ranSecond = second.Where(result => result.Value == "Passed").Select(result => result.Key).ToHashSet();
        Assert.Equal(3, ranFirst.Count);
        Assert.Equal(3, ranSecond.Count);
        Assert.Empty(ranFirst.Intersect(ranSecond));
        Assert.All(Tests.Except(ranFirst), name => Assert.Equal("NotExecuted", first[name]));
        Assert.All(Tests.Except(ranSecond), name => Assert.Equal("NotExecuted", second[name]));
    }

    /// <summary>Runs the sample with <c>VG_SHARD</c> and returns each test's TRX outcome by name.</summary>
    private static Dictionary<string, string> Run(string? shard)
    {
        var output = Built.Value;
        var results = Directory.CreateTempSubdirectory("vg-shard-");
        try
        {
            var environment = shard is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["VG_SHARD"] = shard };
            var run = Fixture.Dotnet(
                environment,
                output,
                Path.Combine(output, "TestsScheduling.dll"),
                "--report-trx", "--report-trx-filename", "shard.trx", "--results-directory", results.FullName);
            Assert.True(run.ExitCode == 0, run.Output);

            XNamespace trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            return XDocument.Load(Path.Combine(results.FullName, "shard.trx"))
                .Descendants(trx + "UnitTestResult")
                .ToDictionary(test => test.Attribute("testName")!.Value, test => test.Attribute("outcome")!.Value);
        }
        finally
        {
            results.Delete(recursive: true);
        }
    }
}
