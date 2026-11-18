using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VerificationGlue.Tests.Specs;

/// <summary>
/// Runs the <c>dotnet test</c> command of samples/TestsCi/ci.yml on samples/TestsCi and checks what
/// docs/guide/ci-setup-with-trx-and-retries.md says it does.
/// </summary>
public class CiSampleSpecs
{
    [Fact]
    public void The_workflow_command_writes_trx_retries_only_the_failed_row_and_skips_manual_tests()
    {
        var results = Directory.CreateTempSubdirectory("vg-ci-");
        var log = Path.Combine(results.FullName, "rates.log");
        try
        {
            var arguments = WorkflowCommand();
            arguments[arguments.IndexOf("--project") + 1] = Path.Combine("samples", "TestsCi");
            arguments[arguments.IndexOf("--results-directory") + 1] = results.FullName;
            arguments[arguments.IndexOf("-c") + 1] = Fixture.Configuration;

            var run = Fixture.Dotnet(new Dictionary<string, string> { ["TESTSCI_LOG"] = log }, Fixture.RepositoryRoot, [.. arguments]);

            Assert.True(run.ExitCode == 0, run.Output);
            Assert.Equal(["rate EUR", "rate GBP", "rate GBP"], File.ReadAllLines(log).Order(StringComparer.Ordinal));

            var last = Outcomes(Path.Combine(results.FullName, "TestsCi_net10.0_" + Architecture + ".trx"));
            var first = Outcomes(Assert.Single(Directory.GetFiles(Path.Combine(results.FullName, "Retries"), "*.trx", SearchOption.AllDirectories)));
            Assert.Equal(["Converts(code: \"GBP\") Passed"], last);
            Assert.Equal(["Adds_tax Passed", "Converts(code: \"EUR\") Passed", "Converts(code: \"GBP\") Failed"], first);
        }
        finally
        {
            results.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("trait:Category=Checkout", new[] { "Adds_tax", "Prints_a_receipt" })]
    [InlineData("!trait:Category=Manual", new[] { "Adds_tax", "Converts(code: \"EUR\")", "Converts(code: \"GBP\")" })]
    [InlineData("name:Converts(code: \"GBP\")", new[] { "Converts(code: \"GBP\")" })]
    [InlineData("name:TestsCi.CheckoutTests.* & !trait:Category=Manual", new[] { "Adds_tax" })]
    [InlineData("name:Adds_* | name:Converts*", new[] { "Adds_tax", "Converts(code: \"EUR\")", "Converts(code: \"GBP\")" })]
    [InlineData("group:default & trait:Category", new[] { "Adds_tax", "Prints_a_receipt" })]
    public void Each_filter_in_the_guide_selects_the_tests_it_lists(string filter, string[] expected)
    {
        var output = Path.Combine(Fixture.RepositoryRoot, "samples", "TestsCi", "bin", Fixture.Configuration, "net10.0");
        var build = Fixture.Dotnet(Fixture.RepositoryRoot, "build", Path.Combine("samples", "TestsCi", "TestsCi.csproj"), "-c", Fixture.Configuration);
        Assert.True(build.ExitCode == 0, build.Output);

        var run = Fixture.Dotnet(output, Path.Combine(output, "TestsCi.dll"), "--list-tests", "--vg-filter", filter);

        Assert.True(run.ExitCode == 0, run.Output);
        Assert.Equal(
            expected,
            run.Output.Replace("\r\n", "\n").Split('\n')
                .TakeWhile(line => !line.StartsWith("Test discovery summary", StringComparison.Ordinal))
                .Where(line => line.StartsWith("  ", StringComparison.Ordinal))
                .Select(line => line.Trim())
                .Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Microsoft.Testing.Extensions.TrxReport")]
    [InlineData("Microsoft.Testing.Extensions.Retry")]
    public void The_sample_references_the_extension_versions_the_repository_uses(string package)
    {
        string Version(string file, string item) => XDocument.Load(Path.Combine(Fixture.RepositoryRoot, file)).Descendants(item)
            .Single(element => element.Attribute("Include")?.Value == package).Attribute("Version")!.Value;

        Assert.Equal(Version("Directory.Packages.props", "PackageVersion"), Version(Path.Combine("samples", "TestsCi", "TestsCi.csproj"), "PackageReference"));
    }

    private static string Architecture => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    /// <summary>The words of the workflow's <c>dotnet test</c> line after <c>dotnet</c>, with quotes removed.</summary>
    private static List<string> WorkflowCommand()
    {
        var line = File.ReadLines(Path.Combine(Fixture.RepositoryRoot, "samples", "TestsCi", "ci.yml"))
            .Select(text => text.Trim())
            .Single(text => text.StartsWith("run: dotnet test ", StringComparison.Ordinal));
        return Regex.Matches(line["run: dotnet ".Length..], "\"([^\"]*)\"|(\\S+)")
            .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
            .ToList();
    }

    private static string[] Outcomes(string trxFile)
    {
        XNamespace trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        return XDocument.Load(trxFile).Descendants(trx + "UnitTestResult")
            .Select(result => result.Attribute("testName")!.Value + " " + result.Attribute("outcome")!.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
