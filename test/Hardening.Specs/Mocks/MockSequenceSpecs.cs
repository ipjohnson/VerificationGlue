namespace Hardening.Specs.Mocks;

/// <summary>
/// The generated mock sequences of docs/design/hardening.md, step 3. Each sequence runs on the
/// reference model and on mocks the public generator built, and both must give the same observations.
/// </summary>
[Trait("Step", "3")]
public class MockSequenceSpecs
{
    [VerifyTest]
    public async Task A_fixed_seed_run_of_mock_sequences_matches_the_reference_model()
    {
        var settings = MockCampaign.Settings();

        var report = await MockCampaign.RunCampaignAsync(settings);

        Assert.True(report.Mismatches.Count == 0, report.FailureText());
        if (settings.Case is null)
        {
            var coverage = report.Coverage;
            foreach (var member in Enum.GetValues<Member>())
            {
                Assert.True(coverage.ContainsKey("Invoke " + member), $"No sequence calls {member}.");
            }

            foreach (var kind in new[]
            {
                "SetupNs", "SetupNs with an await", "SetupTyped", "When", "Check", "CountCalls", "Snapshot", "Fetch", "SetProperty",
                "Subscribe", "Unsubscribe", "RaiseEvent", "Yield", "ChildTask", "FinishInChild", "AbandonInChild",
                "Interleave BeforeCall", "Interleave BeforeReturns", "Interleave BeforeReturns awaited", "ParallelCalls", "ParallelSetups",
            })
            {
                Assert.True(coverage.ContainsKey(kind), $"No sequence holds {kind}.");
            }

            var observations = report.Results.SelectMany(result => result.Expected).ToList();
            foreach (var outcome in new[] { "throws r", "awaits faulted r", "awaits cancelled", "check fails", "check passes", "[c", "raised [h", "= m", ", out 1", "ref 1", "\"\"" })
            {
                Assert.True(observations.Any(observation => observation.Contains(outcome, StringComparison.Ordinal)), $"No observation holds {outcome}.");
            }
        }
    }

    [VerifyTest]
    public async Task A_broken_rule_in_the_model_shrinks_to_a_minimal_sequence_and_prints_its_replay_command()
    {
        var settings = new CampaignSettings(CampaignSettings.DefaultSeed, 60, Spec: MockCampaign.SpecName);

        var report = await MockCampaign.RunCampaignAsync(settings, ModelFault.FirstSetupWins);

        var shrunk = report.Shrunk[0];
        Assert.True(shrunk.Original.Sequence.Size > shrunk.Minimal.Sequence.Size, shrunk.Original.Sequence.ToString());
        Assert.Equal("observation", shrunk.Minimal.MismatchKind);
        Assert.True(shrunk.Minimal.Sequence.Ops.Count <= 3, shrunk.Minimal.Sequence.ToString());
        var reductions = await MockCampaign.RunAllAsync([.. SequenceGenerator.Reductions(shrunk.Minimal.Sequence)], ModelFault.FirstSetupWins);
        Assert.All(reductions, result => Assert.True(result.MismatchKind != "observation", result.Sequence.ToString()));
        Assert.Contains($"  replay:     HARDENING_SEED={settings.Seed} HARDENING_CASES=60 HARDENING_CASE={shrunk.Index} dotnet test --project test/Hardening.Specs -c Release --vg-filter \"name:{MockCampaign.SpecName}\"", report.FailureText());
        Assert.Contains($"  regression: add [Row(\"\"\"{shrunk.Minimal.Sequence.Json()}\"\"\")]", report.FailureText());

        var replayed = Sequence.FromJson("Replayed", shrunk.Minimal.Sequence.Json());
        Assert.Equal("observation", (await MockCampaign.RunAsync(replayed, ModelFault.FirstSetupWins)).MismatchKind);
        Assert.True((await MockCampaign.RunAsync(replayed)).Passed);
    }

    [VerifyTest]
    public async Task A_replay_runs_one_sequence_and_gives_the_same_mismatch()
    {
        var settings = new CampaignSettings(CampaignSettings.DefaultSeed, 60, Spec: MockCampaign.SpecName);
        var first = (await MockCampaign.RunCampaignAsync(settings, ModelFault.CheckConsumesSequences)).Shrunk[0];

        var replay = await MockCampaign.RunCampaignAsync(settings with { Case = first.Index }, ModelFault.CheckConsumesSequences);

        Assert.Equal(first.Original.Sequence.Json(), Assert.Single(replay.Results).Sequence.Json());
        Assert.Equal(
            (first.Original.Detail, first.Minimal.Sequence.Json()),
            (replay.Shrunk[0].Original.Detail, replay.Shrunk[0].Minimal.Sequence.Json()));
    }
}
