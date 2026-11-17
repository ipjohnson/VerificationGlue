using System.Xml.Linq;

namespace VerificationGlue.Tests.Specs;

/// <summary>
/// The fixture's tests, run the ways a user runs them: the executable, <c>dotnet test</c> and
/// <c>dotnet run</c>, with <c>--filter-uid</c> and <c>--report-trx</c>.
/// </summary>
public class RunnerSpecs
{
    private static readonly string[] Passing =
    [
        "A_creates_an_instance",
        "B_sees_the_first_instance_disposed",
        "Gets_the_cancellation_token",
        "Passes",
        "Passes_async",
        "Sees_its_attributes",
        "Static_test",
        "Uses_a_mock",
    ];

    private static readonly string[] Failing =
    [
        "Fails_in_the_original",
        "Fails_in_the_rewritten_copy",
        "Has_an_attribute_that_throws",
        "Needs_a_service",
        "Throws",
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

        Assert.Equal(Passing.Concat(Failing).Order(), results.Keys.Order());
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

    private static Dictionary<string, TrxResult> Trx()
    {
        var directory = Directory.CreateTempSubdirectory("vg-trx-");
        try
        {
            var result = Fixture.Run("--report-trx", "--report-trx-filename", "fixture.trx", "--results-directory", directory.FullName);
            Assert.True(result.ExitCode == 2, result.Output);

            XNamespace trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var document = XDocument.Load(Path.Combine(directory.FullName, "fixture.trx"));
            var classes = document.Descendants(trx + "UnitTest").ToDictionary(
                test => test.Attribute("id")!.Value,
                test => test.Element(trx + "TestMethod")!.Attribute("className")!.Value);

            return document.Descendants(trx + "UnitTestResult").ToDictionary(
                test => test.Attribute("testName")!.Value,
                test => new TrxResult(
                    test.Attribute("outcome")!.Value,
                    classes[test.Attribute("testId")!.Value],
                    test.Descendants(trx + "Message").FirstOrDefault()?.Value ?? "",
                    test.Descendants(trx + "StackTrace").FirstOrDefault()?.Value ?? ""));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed record TrxResult(string Outcome, string ClassName, string Message, string StackTrace);
}
