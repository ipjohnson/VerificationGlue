using System.Xml.Linq;

namespace Inventory.Specs;

public class CompletionGateSpecs
{
    private const string Current = "0123456789abcdef0123456789abcdef01234567";
    private const string Earlier = "fedcba9876543210fedcba9876543210fedcba98";
    private const string First = ReportFixture.File + "#ReportFixture.First";
    private const string Second = ReportFixture.File + "#ReportFixture.Second";
    private const string Case = "test/NSubstituteComparison/Cases/Matching.cs#Matching.Exact_arguments";
    private const string Consumer = "samples/PackageConsumer";

    private static readonly RunEnvironment Machine = new() { Os = "Ubuntu 24.04", Architecture = "x64", Runtime = ".NET 10.0.0", Sdk = "10.0.403" };

    private static readonly ContractInventory Inventory = new()
    {
        Baseline = Current,
        AllowGaps = false,
        Areas = [new Area { Prefix = "MIX", Name = "Mix" }],
        Decisions = [],
        Scenarios =
        [
            Scenario("MIX-001", "supported", First),
            Scenario("MIX-002", "unsupported", Consumer),
            Scenario("MIX-003", "supported", Case),
            Scenario("MIX-004", "supported", Second, Consumer),
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
        Exclusions = [],
        Profiles =
        [
            new Profile { Name = "fixture", Projects = ["test/Inventory.Specs"], Values = new Dictionary<string, string> { ["runtime"] = "jit", ["host"] = "a" }, Os = ["linux-x64"] },
            new Profile { Name = "cases", Projects = ["test/NSubstituteComparison.Specs", "test/NSubstituteComparison", "test/VerificationGlue.Mocks.Generator.Specs"], Values = new Dictionary<string, string> { ["runtime"] = "jit", ["host"] = "b" }, Os = ["linux-x64"] },
            new Profile { Name = "packed", Projects = [Consumer], Values = new Dictionary<string, string> { ["runtime"] = "native-aot", ["host"] = "a" }, Os = ["linux-x64"] },
        ],
    };

    [VerifyTest]
    public void Every_entry_with_passing_evidence_at_the_commit_passes_the_gate_across_reports()
    {
        using var run = new FakeRun();
        run.Trx("specs", "Inventory.Specs", ("ReportFixture", "First", "First", "Passed"), ("ReportFixture", "Second", "Second", "Passed"));
        run.Trx("specs", "NSubstituteComparison.Specs", ("ComparisonSpecs", "Each_case", "Each_case(id: \"Matching.Exact_arguments\")", "Passed"));

        var gate = Gate(Current,
            run.Build(Current, ("specs", "fixture", "success", "specs"), ("specs", "cases", "success", "specs")),
            run.Build(Current, ("pack", "packed", "success", StepInput.NoResults)));

        Assert.True(gate.Passed, gate.Markdown());
        Assert.All(gate.Entries, entry => Assert.Equal(CompletionGate.Verified, entry.Status));
        Assert.Equal(["os=linux-x64 runtime=jit host=a", "os=linux-x64 runtime=native-aot host=a"], gate.Entries.Single(entry => entry.Id == "MIX-004").Configurations);
        Assert.StartsWith("## Completion gate\n\nThe gate passed. Every one of the inventory's 4 entries", gate.Markdown(), StringComparison.Ordinal);
    }

    [VerifyTest]
    public void A_pass_in_a_report_from_another_commit_does_not_count()
    {
        using var run = new FakeRun();
        run.Trx("specs", "Inventory.Specs", ("ReportFixture", "First", "First", "Passed"), ("ReportFixture", "Second", "Second", "Passed"));
        run.Trx("specs", "NSubstituteComparison.Specs", ("ComparisonSpecs", "Each_case", "Each_case(id: \"Matching.Exact_arguments\")", "Passed"));

        var gate = Gate(Current,
            run.Build(Current, ("specs", "fixture", "success", "specs"), ("specs", "cases", "success", "specs")),
            run.Build(Earlier, ("pack", "packed", "success", StepInput.NoResults)));

        Assert.False(gate.Passed);
        Assert.Equal([$"report-1 was made at {Earlier}, not at the current commit {Current}, so its entries do not count."], gate.Rejected);
        Assert.Equal((CompletionGate.Unverified, "no run report of this commit has an entry for it"), Decision(gate, "MIX-002"));
        Assert.Equal((CompletionGate.Unverified, $"no step of this commit ran {Consumer} on linux-x64"), Decision(gate, "MIX-004"));
        Assert.Equal(CompletionGate.Verified, Decision(gate, "MIX-001").Status);
        Assert.Contains("| `MIX-002` | unsupported | unverified | no run report of this commit has an entry for it |", gate.Markdown(), StringComparison.Ordinal);
    }

    [VerifyTest]
    public void A_failed_step_without_TRX_files_fails_every_entry_it_holds()
    {
        using var run = new FakeRun();
        run.Trx("specs", "Inventory.Specs", ("ReportFixture", "First", "First", "Passed"), ("ReportFixture", "Second", "Second", "Passed"));

        var gate = Gate(Current, run.Build(Current, ("specs", "fixture", "success", "specs"), ("pack", "packed", "failure", StepInput.NoResults)));

        Assert.False(gate.Passed);
        Assert.Equal((CompletionGate.Failed, $"os=linux-x64 runtime=native-aot host=a: 1 of 1 tests failed: {Consumer}"), Decision(gate, "MIX-002"));
        Assert.Equal(CompletionGate.Failed, Decision(gate, "MIX-004").Status);
    }

    [VerifyTest]
    public void A_result_counts_only_for_the_test_assembly_of_its_project()
    {
        using var run = new FakeRun();
        run.Trx("specs", "Inventory.Specs", ("ReportFixture", "First", "First", "Passed"), ("ReportFixture", "Second", "Second", "Passed"));
        run.Trx("specs", "VerifyInXunit", ("ReportFixture", "First", "First", "Failed"));

        var report = run.Build(Current, ("specs", "fixture", "success", "specs"));

        Assert.Equal(ScenarioStatus.Passed, report.Scenarios.Single(entry => entry.Id == "MIX-001").Status);
        Assert.Equal((2, 0), (report.Steps[0].Passed, report.Steps[0].Failed));
    }

    [VerifyTest]
    [Row("Passed", ScenarioStatus.Passed)]
    [Row("Failed", ScenarioStatus.Failed)]
    public void A_case_file_that_declares_no_test_takes_the_row_named_after_it(string outcome, string status)
    {
        const string Shape = "test/VerificationGlue.Mocks.Generator.Specs/Shapes/StaticAbstract.cs";
        var inventory = Inventory with { Scenarios = [Scenario("MIX-001", "unsupported", Shape)] };
        using var run = new FakeRun();
        run.Trx("specs", "VerificationGlue.Mocks.Generator.Specs",
            ("ShapeSpecs", "Mocks_compile", "Mocks_compile(file: \"Members.cs\")", "Passed"),
            ("ShapeSpecs", "Mocks_compile", "Mocks_compile(file: \"StaticAbstract.cs\")", outcome));

        var report = new RunReportBuilder(RepositoryRoot.Path, inventory, Matrix).Build(
            [new StepInput("specs", "cases", "success", Path.Combine(run.Root, "specs"))], Current, Machine, "linux-x64", null, run.Root);

        var entry = Assert.Single(report.Scenarios);
        Assert.Equal((status, Shape), (entry.Status, Assert.Single(entry.Tests).Test));
    }

    [VerifyTest]
    public void A_folder_of_TRX_files_with_none_of_the_profile_makes_its_step_incomplete()
    {
        using var run = new FakeRun();
        run.Trx("specs", "VerifyInXunit", ("ReportFixture", "First", "First", "Passed"));

        var report = run.Build(Current, ("specs", "fixture", "success", "specs"));

        Assert.Equal(["Step specs: it wrote no TRX file."], report.Incomplete);
    }

    [VerifyTest]
    public void An_unclassified_entry_never_passes_the_gate()
    {
        var inventory = Inventory with { Scenarios = [Scenario("MIX-001", "unclassified", First)] };
        using var run = new FakeRun();
        run.Trx("specs", "Inventory.Specs", ("ReportFixture", "First", "First", "Passed"));

        var gate = new CompletionGate(inventory, Matrix, Current).Decide([("report", run.Build(Current, ("specs", "fixture", "success", "specs")))]);

        Assert.False(gate.Passed);
        Assert.Equal(CompletionGate.Unverified, gate.Entries[0].Status);
    }

    [VerifyTest]
    public void An_entry_needs_a_pass_on_every_OS_its_profile_lists()
    {
        var matrix = Matrix with { Profiles = [.. Matrix.Profiles.Select(profile => profile.Name == "fixture" ? profile with { Os = ["linux-x64", "macos-arm64"] } : profile)] };
        using var run = new FakeRun();
        run.Trx("specs", "Inventory.Specs", ("ReportFixture", "First", "First", "Passed"), ("ReportFixture", "Second", "Second", "Passed"));

        var gate = new CompletionGate(Inventory, matrix, Current).Decide([("linux", run.Build(Current, ("specs", "fixture", "success", "specs")))]);

        Assert.Equal((CompletionGate.Unverified, $"no step of this commit ran {First} on macos-arm64"), Decision(gate, "MIX-001"));
    }

    [VerifyTest]
    public void A_reference_that_no_profile_runs_is_unverified()
    {
        var inventory = Inventory with { Scenarios = [Scenario("MIX-001", "supported", First, "samples/VerifyAot")] };
        using var run = new FakeRun();
        run.Trx("specs", "Inventory.Specs", ("ReportFixture", "First", "First", "Passed"));

        var gate = new CompletionGate(inventory, Matrix, Current).Decide([("report", run.Build(Current, ("specs", "fixture", "success", "specs")))]);

        Assert.Equal((CompletionGate.Unverified, "no step of this commit ran samples/VerifyAot, which no profile of the matrix runs"), Decision(gate, "MIX-001"));
    }

    [VerifyTest]
    public void The_gate_fails_when_it_reads_no_report()
    {
        var gate = Gate(Current);

        Assert.False(gate.Passed);
        Assert.Equal(["The gate read no run report."], gate.Problems);
    }

    private static CompletionGate Gate(string commit, params RunReport[] reports) =>
        new CompletionGate(Inventory, Matrix, commit).Decide([.. reports.Select((report, i) => ($"report-{i}", report))]);

    private static (string Status, string? Detail) Decision(CompletionGate gate, string id)
    {
        var entry = gate.Entries.Single(entry => entry.Id == id);
        return (entry.Status, entry.Detail);
    }

    private static Scenario Scenario(string id, string support, params string[] evidence) => new()
    {
        Id = id,
        Contract = "c",
        Source = "docs/design/hardening.md",
        Support = support,
        Outcome = support == "unsupported" ? new Outcome { Kind = "diagnostic", Detail = "d" } : null,
        Evidence = evidence,
    };

    // A folder with a subfolder for each step. Each TRX file holds the results of one test assembly, as a
    // solution run writes them.
    private sealed class FakeRun : IDisposable
    {
        private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("vg-completion-gate-");

        public string Root => directory.FullName;

        public void Trx(string step, string assembly, params (string Type, string Method, string Name, string Outcome)[] results)
        {
            var ids = results.Select(_ => Guid.NewGuid().ToString()).ToList();
            var document = new XDocument(new XElement(Ns + "TestRun",
                new XElement(Ns + "Results", results.Select((result, i) => new XElement(Ns + "UnitTestResult",
                    new XAttribute("testId", ids[i]),
                    new XAttribute("testName", result.Name),
                    new XAttribute("outcome", result.Outcome)))),
                new XElement(Ns + "TestDefinitions", results.Select((result, i) => new XElement(Ns + "UnitTest",
                    new XAttribute("id", ids[i]),
                    new XElement(Ns + "TestMethod",
                        new XAttribute("codeBase", $"/work/{assembly}/bin/Release/net10.0/{assembly}.dll"),
                        new XAttribute("className", "Some.Namespace." + result.Type),
                        new XAttribute("name", result.Method))))),
                new XElement(Ns + "ResultSummary", new XAttribute("outcome", "Completed"))));
            Directory.CreateDirectory(Path.Combine(directory.FullName, step));
            document.Save(Path.Combine(directory.FullName, step, $"{assembly}_net10.0_x64.trx"));
        }

        public RunReport Build(string commit, params (string Name, string Profile, string Outcome, string Folder)[] steps) =>
            new RunReportBuilder(RepositoryRoot.Path, Inventory, Matrix).Build(
                [.. steps.Select(step => new StepInput(step.Name, step.Profile, step.Outcome, step.Folder == StepInput.NoResults ? StepInput.NoResults : Path.Combine(directory.FullName, step.Folder)))],
                commit,
                Machine,
                "linux-x64",
                null,
                directory.FullName);

        public void Dispose() => directory.Delete(recursive: true);
    }
}
