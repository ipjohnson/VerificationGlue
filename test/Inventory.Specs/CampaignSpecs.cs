using System.Globalization;
using System.Text.RegularExpressions;

namespace Inventory.Specs;

public partial class CampaignSpecs
{
    public const string TableHeading = "### The scheduled campaign as built";

    private const string Workflow = ".github/workflows/hardening-campaign.yml";

    private static readonly Generator Sequences = new()
    {
        Name = "mock-sequences",
        Test = "A_fixed_seed_run_of_mock_sequences_matches_the_reference_model",
        Cases = 100000,
        LimitMinutes = 15,
    };

    private static readonly CampaignConfig Config = new()
    {
        Owner = "owner",
        Label = "campaign-label",
        RetentionDays = 30,
        Generators = [Sequences],
        Mutations = new MutationRun { LimitMinutes = 60 },
    };

    private const int Seed = 20261123;

    [VerifyTest]
    public void The_checked_in_configuration_names_an_owner_a_label_and_a_limit_for_each_run()
    {
        var config = CampaignConfig.Load(RepositoryRoot.Path);

        Assert.Empty(config.Validate());
        Assert.Equal(30, config.RetentionDays);
    }

    [VerifyTest]
    public void Each_generator_runs_more_cases_than_the_pull_request_run()
    {
        foreach (var generator in CampaignConfig.Load(RepositoryRoot.Path).Generators)
        {
            Assert.True(generator.Cases > PullRequestCases(generator.Test), $"{generator.Name} runs {generator.Cases} cases, and the pull request run already runs {PullRequestCases(generator.Test)}.");
        }
    }

    [VerifyTest]
    public void The_seed_is_the_date_of_the_run()
    {
        Assert.Equal(20261123, CampaignConfig.SeedFor(new DateOnly(2026, 11, 23)));
        Assert.Equal(20261130, CampaignConfig.SeedFor(new DateOnly(2026, 11, 30)));
    }

    [VerifyTest]
    public void A_generator_that_runs_its_cases_within_its_limit_passes_and_files_no_issue()
    {
        var run = CampaignRun.ForGenerator(Sequences, Seed, Finished(), Trx(TestOutcome.Passed), Seeds(Sequences.Cases), []);
        var report = Report(run);

        Assert.Equal(RunStatus.Passed, run.Status);
        Assert.True(report.Complete);
        Assert.Empty(report.Issues(Config, _ => null));
        Assert.Contains("Coverage is complete.", report.Markdown(Config));
    }

    // The main path: a generated failure reaches the owner with what it needs to replay and judge it.
    [VerifyTest]
    public void A_failing_case_files_an_issue_with_its_replay_command_seed_sequence_and_observations()
    {
        const string message = """
            1 sequences do not match the reference model.

            Case_7 of seed 20261123: observation mismatch: observation 2: expected set up, got throws r1
               0: root0.Echo<string>(null).Throws(r1);
              model:      set up / set up / set up
              mocks:      set up / set up / throws r1
              replay:     HARDENING_SEED=20261123 HARDENING_CASES=100000 HARDENING_CASE=7 dotnet test --project test/Hardening.Specs
            """;
        var run = CampaignRun.ForGenerator(Sequences, Seed, Finished(exitCode: 2), Trx(TestOutcome.Failed, message), Seeds(Sequences.Cases), ["mock-sequences/Case_7.txt"]);

        var issues = Report(run).Issues(Config, file => file == "mock-sequences/Case_7.txt" ? "Environment: Ubuntu 24.04" : null);

        Assert.Equal(RunStatus.Failed, run.Status);
        var issue = Assert.Single(issues);
        Assert.Equal($"Hardening campaign: {Sequences.Test} fails", issue.Title);
        Assert.Contains("| Seed | 20261123 |", issue.Body);
        Assert.Contains("| Cases | 100000 |", issue.Body);
        Assert.Contains($"HARDENING_SEED=20261123 HARDENING_CASES=100000 dotnet test --project test/Hardening.Specs -c Release --vg-filter \"name:{Sequences.Test}\"", issue.Body);
        Assert.Contains("HARDENING_CASE=7", issue.Body);
        Assert.Contains("0: root0.Echo<string>(null).Throws(r1);", issue.Body);
        Assert.Contains("model:      set up / set up / set up", issue.Body);
        Assert.Contains("mocks:      set up / set up / throws r1", issue.Body);
        Assert.Contains("Environment: Ubuntu 24.04", issue.Body);
        Assert.Contains("owner triages it", issue.Body);
        Assert.Contains("`hardening-campaign`, kept 30 days", issue.Body);
    }

    [VerifyTest]
    public void A_run_stopped_at_its_limit_reports_the_overrun_and_incomplete_coverage()
    {
        var run = CampaignRun.ForGenerator(Sequences, Seed, new ProcessOutcome(-1, Stopped: true, TimeSpan.FromMinutes(15)), null, Seeds(Sequences.Cases), []);
        var report = Report(run);

        Assert.Equal(RunStatus.Incomplete, run.Status);
        Assert.True(run.Overrun);
        Assert.False(report.Complete);
        Assert.Equal(["it ran over its 15-minute limit and was stopped after 15.0 minutes", "it wrote no TRX file"], run.Incomplete);
        var markdown = report.Markdown(Config);
        Assert.Contains("Coverage is incomplete:", markdown);
        Assert.Contains("| mock-sequences | 100,000 | 15 min | 900.0 s | incomplete, over its limit |", markdown);
        var issue = Assert.Single(report.Issues(Config, _ => null));
        Assert.Equal("Hardening campaign: the mock-sequences run is incomplete", issue.Title);
        Assert.Contains("ran over a limit", issue.Body);
    }

    [VerifyTest]
    public void A_run_of_fewer_cases_or_another_seed_is_incomplete()
    {
        var fewer = CampaignRun.ForGenerator(Sequences, Seed, Finished(), Trx(TestOutcome.Passed), Seeds(2000), []);
        var replay = CampaignRun.ForGenerator(Sequences, Seed, Finished(), Trx(TestOutcome.Passed), Seeds(Sequences.Cases) with { Case = 7 }, []);
        var missing = CampaignRun.ForGenerator(Sequences, Seed, Finished(), Trx(TestOutcome.Passed), null, []);

        Assert.Equal(["it ran 2000 cases of seed 20261123, not the 100000 cases of seed 20261123"], fewer.Incomplete);
        Assert.Equal(["it ran case 7 of seed 20261123, not the 100000 cases of seed 20261123"], replay.Incomplete);
        Assert.Equal(["it wrote no seed file, so it did not start its cases"], missing.Incomplete);
    }

    [VerifyTest]
    public void A_mutation_that_does_not_match_the_catalog_files_an_issue_with_its_replay_command()
    {
        var survived = new MutationResult
        {
            Id = "MUT-03",
            Kind = "Reverse setup precedence",
            Change = "A call takes the first matching response instead of the last.",
            Status = MutationStatus.Survived,
            FailingCases = [],
            FailedRuns = [],
            Problems = ["MUT-03 survived, and docs/design/hardening-mutations.json records no explanation."],
        };
        var run = CampaignRun.ForMutations(60, Finished(exitCode: 1), Mutations(survived), catalogCount: 1);

        var issue = Assert.Single(Report(run).Issues(Config, _ => null));

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("Hardening campaign: MUT-03 does not match the catalog", issue.Title);
        Assert.Contains("expected:   detected by a failing case", issue.Body);
        Assert.Contains("actual:     survived", issue.Body);
        Assert.Contains("dotnet run --project test/Hardening.Mutations -c Release -- --output artifacts/mutations --only MUT-03", issue.Body);
    }

    [VerifyTest]
    public void A_mutation_run_that_stops_early_is_incomplete()
    {
        var stopped = CampaignRun.ForMutations(60, new ProcessOutcome(-1, Stopped: true, TimeSpan.FromMinutes(60)), null, catalogCount: 6);
        var partial = CampaignRun.ForMutations(60, Finished(), Mutations(), catalogCount: 6);

        Assert.True(stopped.Overrun);
        Assert.Equal(["it ran over its 60-minute limit and was stopped after 60.0 minutes", "it wrote no mutations.json"], stopped.Incomplete);
        Assert.Equal(["it ran 0 of the 6 mutations"], partial.Incomplete);
    }

    [VerifyTest]
    public void The_campaign_runs_on_a_schedule_outside_the_pull_request_run_and_keeps_its_artifacts()
    {
        var config = CampaignConfig.Load(RepositoryRoot.Path);
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot.Path, Workflow));
        var ci = File.ReadAllText(Path.Combine(RepositoryRoot.Path, ".github/workflows/ci.yml"));

        Assert.Matches(@"(?m)^  schedule:\n    - cron: ""[^""]+""$", workflow);
        Assert.DoesNotContain("pull_request", workflow);
        Assert.Contains("dotnet run --project test/Hardening.Campaign", workflow);
        Assert.DoesNotContain("Hardening.Campaign", ci);
        Assert.DoesNotContain("Hardening.Mutations", ci);
        Assert.Equal([config.RetentionDays], RetentionDays().Matches(workflow).Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct());
        Assert.Contains($"name: {CampaignReport.ArtifactName}", workflow);

        var timeout = int.Parse(CampaignTimeout().Match(workflow).Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.True(timeout >= config.TotalLimitMinutes + 15, $"The campaign job's timeout of {timeout} minutes leaves no room for the build after the {config.TotalLimitMinutes} minutes of limits.");
    }

    [VerifyTest]
    public void The_triage_job_files_issues_for_the_owner_and_label_of_the_configuration()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot.Path, Workflow));

        Assert.Contains($"config={CampaignConfig.RelativePath}", workflow);
        Assert.Contains("owner=$(jq -r .owner \"$config\")", workflow);
        Assert.Contains("label=$(jq -r .label \"$config\")", workflow);
        Assert.Contains($"issues=artifacts/campaign/{CampaignReport.IssuesFileName}", workflow);
        Assert.Contains("gh issue create --title \"$title\" --label \"$label\" --assignee \"$owner\"", workflow);
        Assert.Contains("gh issue comment \"$number\"", workflow);
    }

    [VerifyTest]
    public void The_design_doc_names_the_triage_owner_the_failure_destination_and_each_run()
    {
        var config = CampaignConfig.Load(RepositoryRoot.Path);
        var doc = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs/design/hardening.md"));
        var rows = MarkdownTable.Read(doc, TableHeading);

        Assert.Contains($"The triage owner is `{config.Owner}`.", doc);
        Assert.Contains($"labelled `{config.Label}`", doc);
        Assert.NotNull(rows);
        Assert.Equal(
            [
                .. config.Generators.Select(generator => string.Create(CultureInfo.InvariantCulture, $"{generator.Name} | {generator.Test} | {PullRequestCases(generator.Test):N0} | {generator.Cases:N0} | {generator.LimitMinutes} minutes")),
                $"{CampaignConfig.MutationsName} | Hardening.Mutations | none | every mutation | {config.Mutations.LimitMinutes} minutes",
            ],
            rows.Select(row => string.Join(" | ", row)));
    }

    // The case budget of the spec in the pull request run: DefaultCases in the file that names the spec in SpecName.
    private static int PullRequestCases(string test)
    {
        var file = Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "test/Hardening.Specs"), "*.cs", SearchOption.AllDirectories)
            .Single(path => File.ReadAllText(path).Contains($"SpecName = \"{test}\"", StringComparison.Ordinal));
        return int.Parse(DefaultCases().Match(File.ReadAllText(file)).Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static ProcessOutcome Finished(int exitCode = 0) => new(exitCode, Stopped: false, TimeSpan.FromSeconds(42));

    private static TrxFile Trx(string outcome, string? message = null) =>
        new("Completed", [new TrxResult("Hardening.Specs.Mocks.MockSequenceSpecs", Sequences.Test, Sequences.Test, outcome, message)]);

    private static SeedRun Seeds(int cases) => new() { Test = Sequences.Test, Seed = Seed, Cases = cases, Budget = 2000 };

    private static MutationReport Mutations(params MutationResult[] results) => new()
    {
        Commit = "abc",
        Date = DateTimeOffset.UnixEpoch,
        BaselineCases = 10,
        BaselineProblems = [],
        Mutations = results,
    };

    private static CampaignReport Report(params CampaignRun[] runs) => new()
    {
        Commit = "abc",
        Date = DateTimeOffset.UnixEpoch,
        Seed = Seed,
        Environment = "Ubuntu",
        Runs = runs,
    };

    [GeneratedRegex(@"retention-days: (\d+)")]
    private static partial Regex RetentionDays();

    [GeneratedRegex(@"(?m)^  campaign:\n    runs-on: [^\n]+\n    timeout-minutes: (\d+)$")]
    private static partial Regex CampaignTimeout();

    [GeneratedRegex(@"DefaultCases = (\d+);")]
    private static partial Regex DefaultCases();
}
