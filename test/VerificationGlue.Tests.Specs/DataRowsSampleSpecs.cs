namespace VerificationGlue.Tests.Specs;

/// <summary>
/// Runs samples/TestsDataRows and checks the display names that docs/guide/data-rows.md and
/// docs/guide/extending.md show.
/// </summary>
public class DataRowsSampleSpecs
{
    [Fact]
    public void Lists_one_test_per_row_with_the_names_the_guides_show()
    {
        var project = Path.Combine(Fixture.RepositoryRoot, "samples", "TestsDataRows", "TestsDataRows.csproj");
        var build = Fixture.Dotnet(Fixture.RepositoryRoot, "build", project, "-c", Fixture.Configuration);
        Assert.True(build.ExitCode == 0, build.Output);

        var output = Path.Combine(Fixture.RepositoryRoot, "samples", "TestsDataRows", "bin", Fixture.Configuration, "net10.0");
        var run = Fixture.Dotnet(output, Path.Combine(output, "TestsDataRows.dll"), "--list-tests");

        Assert.True(run.ExitCode == 0, run.Output);
        Assert.Contains("found 6 test(s)", run.Output);
        Assert.All(
            [
                "Totals(qty: 2, price: 10, expected: 20)",
                "Totals(qty: 3, price: 5, expected: 15)",
                "Discounts(code: \"SAVE10\", expected: 90)",
                "Discounts(code: \"HALF\", expected: 50)",
                "Ships 1 / \"standard\"",
                "Ships 5 / \"express\"",
            ],
            name => Assert.Contains("  " + name + "\n", run.Output.Replace("\r\n", "\n")));
    }
}
