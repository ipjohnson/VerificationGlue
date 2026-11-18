using System.Text.RegularExpressions;

namespace VerificationGlue.Tests.AspNetCore.Specs;

/// <summary>
/// <c>[WebApp&lt;TEntry&gt;]</c> with class and assembly scope, checked through the TRX results and the log of
/// the tests in test/VerificationGlue.Tests.AspNetCore.Fixture/SharedAppTests.cs. Each of those tests logs
/// "<c>test</c> reached app <c>n</c> with client <c>m</c>", and each app logs "app <c>n</c> stopped".
/// </summary>
public partial class SharedAppSpecs
{
    private static readonly string[] s_classTests =
        ["First_test_of_the_shared_class", "Second_test_of_the_shared_class", "The_shared_app_reads_the_class_and_assembly_attributes"];

    private static readonly string[] s_kestrelTests = ["First_test_of_the_shared_Kestrel_app", "Second_test_of_the_shared_Kestrel_app"];

    private static readonly string[] s_assemblyTests = ["A_class_reaches_the_assembly_app", "Another_class_reaches_the_same_assembly_app"];

    /// <summary>The fixture tests whose app is shared, so their outputs do not each hold a start line.</summary>
    internal static IEnumerable<string> Tests => [.. s_classTests, .. s_kestrelTests, .. s_assemblyTests];

    [VerifyTest]
    public void The_tests_of_a_class_reach_one_app_that_starts_in_the_first_test_and_stops_after_the_last()
    {
        AssertPassed(s_classTests);
        var app = SingleApp(s_classTests);

        Assert.Single(s_classTests, test => Fixture.Results[test].Output.Contains("FixtureShop started in ", StringComparison.Ordinal));
        Assert.All(Reached().Where(entry => !s_classTests.Contains(entry.Test)), entry => Assert.NotEqual(app, entry.App));

        var stopped = Array.IndexOf([.. Fixture.Log], $"app {app} stopped");
        Assert.True(stopped > LastReached(s_classTests), string.Join("\n", Fixture.Log));
        Assert.True(stopped < Array.IndexOf([.. Fixture.Log], $"app {SingleApp(s_assemblyTests)} stopped"), string.Join("\n", Fixture.Log));
    }

    [VerifyTest]
    public void Two_classes_that_name_the_same_entry_type_with_assembly_scope_reach_one_app_for_the_run()
    {
        AssertPassed(s_assemblyTests);
        var app = SingleApp(s_assemblyTests);

        Assert.Single(s_assemblyTests, test => Fixture.Results[test].Output.Contains("FixtureShop started in ", StringComparison.Ordinal));
        Assert.Equal($"app {app} stopped", Fixture.Log[^1]);
    }

    [VerifyTest]
    public void Each_test_of_a_shared_app_gets_its_own_HttpClient()
    {
        var clients = Reached().Where(entry => entry.Client is not null).Select(entry => entry.Client).ToList();

        Assert.Equal(6, clients.Count);
        Assert.Equal(clients.Count, clients.Distinct().Count());
    }

    [VerifyTest]
    public void A_scoped_service_of_a_shared_app_is_disposed_when_its_test_ends_and_not_with_the_app()
    {
        Assert.All(["First_test_of_the_shared_class", "Second_test_of_the_shared_class"], test =>
        {
            var lines = Lines(Fixture.Results[test].Output);
            Assert.Single(lines, "[Information] FixtureShop.Basket: Basket disposed while the app runs");
            Assert.DoesNotContain(lines, line => line.Contains("Basket disposed while the app stops", StringComparison.Ordinal));
        });
    }

    [VerifyTest]
    public void A_request_to_a_shared_app_logs_to_the_test_that_sent_it()
    {
        var first = Lines(Fixture.Results["First_test_of_the_shared_class"].Output);
        var second = Lines(Fixture.Results["Second_test_of_the_shared_class"].Output);

        Assert.Single(first, line => line.EndsWith(" of 41", StringComparison.Ordinal));
        Assert.DoesNotContain(first, line => line.EndsWith(" of 42", StringComparison.Ordinal));
        Assert.Single(second, line => line.EndsWith(" of 42", StringComparison.Ordinal));
        Assert.DoesNotContain(second, line => line.EndsWith(" of 41", StringComparison.Ordinal));
    }

    [VerifyTest]
    public void A_request_to_a_shared_app_on_Kestrel_logs_to_no_test()
    {
        AssertPassed(["Second_test_of_the_shared_Kestrel_app"]);

        Assert.DoesNotContain(Lines(Fixture.Results["Second_test_of_the_shared_Kestrel_app"].Output), line => line.Contains("Saved order", StringComparison.Ordinal));
    }

    [VerifyTest]
    public void A_shared_app_reads_AppSetting_AppEnvironment_and_Kestrel_from_its_class_and_the_assembly()
    {
        AssertPassed(["The_shared_app_reads_the_class_and_assembly_attributes"]);
        AssertPassed(s_kestrelTests);
        SingleApp(s_kestrelTests);
    }

    [VerifyTest]
    public void An_assembly_app_reads_only_the_assembly_attributes()
    {
        AssertPassed(["Another_class_reaches_the_same_assembly_app"]);
    }

    [VerifyTest]
    public void A_mock_parameter_under_a_shared_app_fails_the_build_with_VGT0008()
    {
        var (exitCode, output) = Fixture.SharedMockBuild;

        Assert.NotEqual(0, exitCode);
        Assert.Contains(
            "error VGT0008: The test 'Takes_a_mock_under_a_shared_app' has [Mock] parameters, but its container " +
            "VerificationGlue.Tests.AspNetCore.WebAppBootstrap<SharedMock.FixtureShopEntry> is shared with Scope = Class and " +
            "VerificationGlue.Tests.AspNetCore.WebAppBootstrap<SharedMock.FixtureShopEntry> does not implement ISupportsChildOverrides",
            output);
        Assert.All(ErrorIds().Matches(output), error => Assert.Equal("VGT0008", error.Groups[1].Value));
    }

    private static int SingleApp(string[] tests)
    {
        var apps = Reached().Where(entry => tests.Contains(entry.Test)).ToList();
        Assert.Equal(tests.Length, apps.Count);
        return Assert.Single(apps.Select(entry => entry.App).Distinct());
    }

    private static int LastReached(string[] tests) =>
        Fixture.Log.Select((line, index) => (line, index)).Where(entry => tests.Any(test => entry.line.StartsWith(test + " reached", StringComparison.Ordinal))).Max(entry => entry.index);

    private static IEnumerable<(string Test, int App, int? Client)> Reached() =>
        Fixture.Log.Select(line => ReachedLine().Match(line)).Where(match => match.Success).Select(match => (
            match.Groups["test"].Value,
            int.Parse(match.Groups["app"].Value),
            match.Groups["client"].Success ? int.Parse(match.Groups["client"].Value) : (int?)null));

    private static void AssertPassed(string[] tests)
    {
        foreach (var test in tests)
        {
            var result = Fixture.Results[test];
            Assert.True(result.Outcome == "Passed", $"{test}: {result.Outcome}\n{result.Message}\n{result.Output}");
        }
    }

    private static string[] Lines(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex(@"^(?<test>\w+) reached app (?<app>\d+)( with client (?<client>\d+))?$")]
    private static partial Regex ReachedLine();

    [GeneratedRegex(@"error (\w+\d+):")]
    private static partial Regex ErrorIds();
}
