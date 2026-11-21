using System.Xml.Linq;

namespace Inventory.Specs;

public class RunReportSpecs
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string First = ReportFixture.File + "#ReportFixture.First";
    private const string Second = ReportFixture.File + "#ReportFixture.Second";
    private const string Seeded = ReportFixture.File + "#ReportFixture.Seeded";

    private static readonly RunEnvironment Machine = new() { Os = "Ubuntu 24.04", Architecture = "x64", Runtime = ".NET 10.0.0", Sdk = "10.0.403", Runner = "GitHub Actions ubuntu24" };

    private static readonly ContractInventory Inventory = new()
    {
        Baseline = Commit,
        AllowGaps = true,
        Areas = [new Area { Prefix = "MIX", Name = "Mix" }],
        Decisions = [new Decision { Id = "CD-99", Question = "q", ProposedOwner = "the user", Approved = false, Scenarios = ["MIX-004"] }],
        Scenarios =
        [
            Scenario("MIX-001", First, Seeded),
            Scenario("MIX-002", ReportFixture.File),
            Scenario("MIX-003", "samples/PackageConsumer"),
            Scenario("MIX-004", Second),
        ],
    };

    private static readonly ConfigurationMatrix Matrix = new()
    {
        Factors =
        [
            new Factor { Name = "os", Values = ["linux-x64", "macos-arm64"] },
            new Factor { Name = "runtime", Values = ["jit", "native-aot"] },
            new Factor { Name = "host", Values = ["a", "b"] },
        ],
        Exclusions = [new Exclusion { Pair = ["os=macos-arm64", "runtime=native-aot"], Reason = "Native AOT runs on linux-x64 only." }],
        Profiles = [new Profile { Name = "fixture", Project = "test/Inventory.Specs", Values = new Dictionary<string, string> { ["runtime"] = "jit", ["host"] = "a" }, Os = ["linux-x64"] }],
    };

    private static readonly string[] FixtureConfiguration = ["os=linux-x64", "runtime=jit", "host=a"];

    [VerifyTest]
    public void Each_exercised_Scenario_ID_gets_its_status_commit_configuration_environment_and_seed()
    {
        using var run = new FakeRun();
        run.Trx("step", "Completed", ("First", "Passed"), ("Second", "Passed"), ("Seeded", "Passed"));
        run.Seed("step", """{ "test": "Seeded", "seed": 20261119, "cases": 300, "budget": 300, "case": null }""");

        var report = run.Build(("step", "success"));

        Assert.True(report.Complete, string.Join("\n", report.Incomplete));
        Assert.Equal(["MIX-001", "MIX-002", "MIX-004"], report.Scenarios.Select(entry => entry.Id));
        var mix1 = report.Scenarios[0];
        Assert.Equal(ScenarioStatus.Passed, mix1.Status);
        Assert.Equal(Commit, mix1.Commit);
        Assert.Equal(FixtureConfiguration, mix1.Configuration);
        Assert.Equal(Machine, mix1.Environment);
        Assert.Equal([First, Seeded], mix1.Tests.Select(test => test.Test));
        var seed = Assert.Single(mix1.Seeds!);
        Assert.Equal(("Seeded", 20261119, 300), (seed.Test, seed.Seed, seed.Cases));
        Assert.Equal([First, Second, Seeded], report.Scenarios[1].Tests.Select(test => test.Test));
        Assert.Empty(report.Validate(Inventory));
    }

    [VerifyTest]
    public void An_entry_whose_decision_is_not_approved_is_blocked_even_when_its_tests_pass()
    {
        using var run = new FakeRun();
        run.Trx("step", "Completed", ("First", "Passed"), ("Second", "Passed"), ("Seeded", "Passed"));

        var entry = run.Build(("step", "success")).Scenarios.Single(entry => entry.Id == "MIX-004");

        Assert.Equal((ScenarioStatus.Blocked, "the contract decision CD-99 is not approved"), (entry.Status, entry.Detail));
    }

    [VerifyTest]
    public void A_failed_test_fails_its_scenario_and_its_combination()
    {
        using var run = new FakeRun();
        run.Trx("step", "Failed", ("First", "Passed"), ("Second", "Failed"), ("Seeded", "Passed"));

        var report = run.Build(("step", "failure"));
        var coverage = new CoverageReport(report, Matrix, Inventory);

        Assert.True(report.Complete);
        Assert.Equal(ScenarioStatus.Failed, report.Scenarios.Single(entry => entry.Id == "MIX-002").Status);
        Assert.Equal(CoverageReport.Failed, coverage.Combinations()[0].Status);
        Assert.False(coverage.Passed);
        Assert.Contains("- `MIX-002` in os=linux-x64 runtime=jit host=a: 1 of 3 tests failed: " + Second, coverage.Markdown(), StringComparison.Ordinal);
    }

    [VerifyTest]
    [Row("cancelled")]
    [Row("skipped")]
    public void A_step_that_did_not_finish_makes_the_run_incomplete_and_its_scenarios_blocked(string outcome)
    {
        using var run = new FakeRun();
        run.Trx("done", "Completed", ("First", "Passed"));

        var report = run.Build(("done", "success"), ("cut", outcome));
        var coverage = new CoverageReport(report, Matrix, Inventory);

        Assert.False(report.Complete);
        Assert.Equal([$"Step cut: its outcome is {outcome}; it wrote no TRX file."], report.Incomplete);
        Assert.Equal(ScenarioStatus.Blocked, report.Scenarios.Single(entry => entry.Id == "MIX-001").Status);
        Assert.Equal(CoverageReport.Unverified, coverage.Combinations()[0].Status);
        Assert.False(coverage.Passed);
        Assert.StartsWith("## Hardening coverage\n\n**Coverage is incomplete.**", coverage.Markdown(), StringComparison.Ordinal);
    }

    [VerifyTest]
    [Row("""{ "test": "Seeded", "seed": 7, "cases": 30, "budget": 300, "case": null }""", "Seeded ran 30 of its 300 cases")]
    [Row("""{ "test": "Seeded", "seed": 7, "cases": 300, "budget": 300, "case": 57 }""", "Seeded replayed only case 57 of seed 7")]
    public void A_seeded_run_with_fewer_cases_than_its_budget_is_incomplete(string seed, string detail)
    {
        using var run = new FakeRun();
        run.Trx("step", "Completed", ("First", "Passed"), ("Second", "Passed"), ("Seeded", "Passed"));
        run.Seed("step", seed);

        var report = run.Build(("step", "success"));

        Assert.Equal([$"Step step: {detail}."], report.Incomplete);
        Assert.Equal(CoverageReport.Unverified, new CoverageReport(report, Matrix, Inventory).Combinations()[0].Status);
    }

    [VerifyTest]
    public void A_test_the_inventory_names_that_no_step_ran_makes_the_run_incomplete()
    {
        using var run = new FakeRun();
        run.Trx("step", "Completed", ("First", "Passed"), ("Seeded", "Passed"));

        var report = run.Build(("step", "success"));

        Assert.False(report.Complete);
        Assert.Contains($"MIX-002: the inventory names {Second}, and no step of fixture ran it.", report.Incomplete);
        Assert.Equal(ScenarioStatus.Blocked, report.Scenarios.Single(entry => entry.Id == "MIX-002").Status);
    }

    [VerifyTest]
    public void An_aborted_TRX_file_makes_its_step_incomplete()
    {
        using var run = new FakeRun();
        run.Trx("step", "Aborted", ("First", "Passed"), ("Second", "Passed"), ("Seeded", "Passed"));

        var report = run.Build(("step", "success"));

        Assert.Equal(["Step step: results.trx says its run is Aborted."], report.Incomplete);
    }

    [VerifyTest]
    public void The_coverage_report_lists_covered_and_unverified_combinations_exclusions_and_failures()
    {
        using var run = new FakeRun();
        run.Trx("step", "Completed", ("First", "Passed"), ("Second", "Passed"), ("Seeded", "Passed"));

        var coverage = new CoverageReport(run.Build(("step", "success")), Matrix, Inventory);
        var markdown = coverage.Markdown();

        Assert.True(coverage.Passed);
        Assert.Equal([CoverageReport.Covered], coverage.Combinations().Take(1).Select(combination => combination.Status));
        Assert.All(coverage.Combinations().Skip(1), combination => Assert.Equal(CoverageReport.Unverified, combination.Status));
        Assert.Contains("| 1 | linux-x64 | jit | a | fixture | covered | step |", markdown, StringComparison.Ordinal);
        Assert.Contains("| `os=macos-arm64` `runtime=native-aot` | Native AOT runs on linux-x64 only. |", markdown, StringComparison.Ordinal);
        Assert.Contains("No scenario and no step failed.", markdown, StringComparison.Ordinal);
        Assert.Contains("3 of the inventory's 4 Scenario IDs have evidence in the projects of this run.", markdown, StringComparison.Ordinal);
    }

    [VerifyTest]
    public void A_run_report_written_as_JSON_reads_back_the_same()
    {
        using var run = new FakeRun();
        run.Trx("step", "Completed", ("First", "Passed"), ("Second", "Passed"), ("Seeded", "Passed"));
        run.Seed("step", """{ "test": "Seeded", "seed": 20261119, "cases": 300, "budget": 300, "case": null }""");
        var report = run.Build(("step", "success"));

        var read = RunReport.Parse(report.Json());

        Assert.Equal(report.Json(), read.Json());
    }

    private static Scenario Scenario(string id, params string[] evidence) => new()
    {
        Id = id,
        Contract = "c",
        Source = "docs/design/hardening.md",
        Support = "supported",
        Evidence = evidence,
    };

    // A folder with a subfolder for each step, which holds the step's TRX file and seeds.
    private sealed class FakeRun : IDisposable
    {
        private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("vg-run-report-");

        public void Trx(string step, string outcome, params (string Method, string Outcome)[] results)
        {
            var ids = results.Select(_ => Guid.NewGuid().ToString()).ToList();
            var document = new XDocument(new XElement(Ns + "TestRun",
                new XElement(Ns + "Results", results.Select((result, i) => new XElement(Ns + "UnitTestResult",
                    new XAttribute("testId", ids[i]),
                    new XAttribute("testName", result.Method),
                    new XAttribute("outcome", result.Outcome)))),
                new XElement(Ns + "TestDefinitions", results.Select((result, i) => new XElement(Ns + "UnitTest",
                    new XAttribute("id", ids[i]),
                    new XElement(Ns + "TestMethod", new XAttribute("className", "Inventory.Specs.ReportFixture"), new XAttribute("name", result.Method))))),
                new XElement(Ns + "ResultSummary", new XAttribute("outcome", outcome))));
            Directory.CreateDirectory(Path.Combine(directory.FullName, step));
            document.Save(Path.Combine(directory.FullName, step, "results.trx"));
        }

        public void Seed(string step, string json)
        {
            var seeds = Path.Combine(directory.FullName, step, RunReportBuilder.SeedsDirectory);
            Directory.CreateDirectory(seeds);
            File.WriteAllText(Path.Combine(seeds, "Seeded.json"), json);
        }

        public RunReport Build(params (string Name, string Outcome)[] steps) =>
            new RunReportBuilder(RepositoryRoot.Path, Inventory, Matrix).Build(
                [.. steps.Select(step => new StepInput(step.Name, "fixture", step.Outcome, Path.Combine(directory.FullName, step.Name)))],
                Commit,
                Machine,
                "linux-x64",
                "https://github.com/ipjohnson/VerificationGlue/actions/runs/1/attempts/1",
                directory.FullName);

        public void Dispose() => directory.Delete(recursive: true);
    }
}
