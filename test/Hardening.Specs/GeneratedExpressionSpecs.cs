namespace Hardening.Specs;

/// <summary>
/// The generated expression harness of docs/design/hardening.md, step 2. Each expression runs in plain
/// C# and in every form of <see cref="Form"/>, and each form must give the same outcome, trace, final
/// state and skipped calls under the tier it expects.
/// </summary>
public class GeneratedExpressionSpecs
{
    [VerifyTest]
    public async Task A_fixed_seed_run_keeps_plain_CSharp_behavior()
    {
        var settings = CampaignSettings.FromEnvironment();
        settings.RecordSeed();

        var report = await Campaign.RunAsync(settings);

        Assert.True(report.Mismatches.Count == 0, report.FailureText());
        Assert.All(report.Results, result => Assert.NotNull(result.Ran));
        if (settings.Case is null)
        {
            Assert.Contains(report.Results, result => result.Ran == Tier.Tier3 && result.Classification == Classification.Passed);
            Assert.Contains(report.Results, result => result.Ran == Tier.Tier3Lambda && result.Classification == Classification.Passed);
            Assert.Contains(report.Results, result => result.Form == Form.VerifyLambda && result.Classification == Classification.Fallback);
            Assert.Contains(report.Results, result => result.Form == Form.VerifyLocalFunction && result.Classification == Classification.Fallback);
            Assert.Contains(report.Results, result => result.Baseline.Outcome == "failed");
            Assert.Contains(report.Results, result => result.Baseline.Outcome.StartsWith("throws ", StringComparison.Ordinal));
            Assert.Contains(report.Results, result => result.Baseline.Skipped.Count > 0);
            Assert.Contains(report.Results, result => result.Case.IsAsync);
            Assert.Contains(report.Results, result => result.Baseline.Outcome.StartsWith("throws System.Threading.Tasks.TaskCanceledException", StringComparison.Ordinal));
            Assert.Contains(report.Results, result => result.Form == Form.CheckLoop && result.Baseline.Outcome == "failed 2 times");
        }
    }

    [VerifyTest]
    public async Task With_the_generator_off_every_case_that_requires_rewriting_fails()
    {
        var cases = Enumerable.Range(0, 40).Select(index => Grammar.Generate(CampaignSettings.DefaultSeed, index, 40)).ToList();

        var results = await CaseRunner.RunAsync(cases, GeneratorMode.Off);

        var required = results.Where(result => result.Expected.RequiresRewriting).ToList();
        Assert.Contains(required, result => result.Expected.Tier == Tier.Tier3);
        Assert.Contains(required, result => result.Expected.Tier == Tier.Tier3Lambda);
        Assert.All(required, result =>
        {
            Assert.Equal(Classification.Mismatch, result.Classification);
            Assert.Equal(("tier", Tier.Tier1), (result.MismatchKind, result.Ran));
        });
        Assert.All(results, result => Assert.True(result.MismatchKind is null or "tier", $"{result.Case.Text}, {result.Form}: {result.Detail}"));
    }

    [VerifyTest]
    public async Task With_tier_3_off_every_case_that_expects_tier_3_fails()
    {
        var cases = Enumerable.Range(0, 40).Select(index => Grammar.Generate(CampaignSettings.DefaultSeed, index, 40)).ToList();

        var results = await CaseRunner.RunAsync(cases, GeneratorMode.NoRewrite);

        var tier3 = results.Where(result => result.Expected.Tier == Tier.Tier3).ToList();
        Assert.NotEmpty(tier3);
        Assert.All(tier3, result => Assert.Equal(("tier", Tier.Tier2), (result.MismatchKind, result.Ran)));
        Assert.All(results.Except(tier3), result => Assert.NotEqual(Classification.Mismatch, result.Classification));
    }

    [VerifyTest]
    public async Task Intentional_fallback_is_classified_as_fallback_not_as_a_pass()
    {
        var results = await CaseRunner.RunAsync([new ExpressionCase("Writes", "p.I(1, 2) + n++ > 1"), new ExpressionCase("Reads", "p.I(1, 2) > 1")]);

        (Classification, Tier?) Of(string name, Form form) =>
            results.Single(result => result.Case.Name == name && result.Form == form) is var result ? (result.Classification, result.Ran) : default;

        Assert.Equal((Classification.Fallback, Tier.Tier2), Of("Writes", Form.VerifyLambda));
        Assert.Equal((Classification.Fallback, Tier.Tier2), Of("Writes", Form.CheckLambda));
        Assert.Equal((Classification.Fallback, Tier.Tier2), Of("Reads", Form.VerifyLocalFunction));
        Assert.Equal((Classification.Passed, Tier.Tier3Lambda), Of("Reads", Form.VerifyLambda));
        Assert.Equal((Classification.Passed, Tier.Tier3), Of("Writes", Form.VerifyRewritten));
        Assert.Equal((Classification.Passed, Tier.Tier2), Of("Reads", Form.VerifyPlain));
    }

    [VerifyTest]
    public async Task A_mismatch_shrinks_to_a_minimal_expression_and_prints_its_replay_command()
    {
        var settings = new CampaignSettings(CampaignSettings.DefaultSeed, 60);

        var report = await Campaign.RunAsync(settings, GeneratorMode.Rewrite, NoShortCircuit);

        var largest = report.Mismatches.Where(result => result.MismatchKind == "trace").MaxBy(result => result.Case.Tree!.Size)!;
        var minimal = await Campaign.ShrinkAsync(largest, GeneratorMode.Rewrite, NoShortCircuit);
        Assert.True(largest.Case.Tree!.Size > 3, largest.Case.Text);
        Assert.Equal("p.B(1, false) && p.B(2, true)", minimal.Case.Text);
        Assert.Equal((largest.Form, "trace"), (minimal.Form, minimal.MismatchKind));
        var reductions = Grammar.Reductions(minimal.Case.Tree!).Select((node, i) => new ExpressionCase($"Reduced_{i}", node)).ToList();
        var reduced = await CaseRunner.RunAsync(reductions, GeneratorMode.Rewrite, NoShortCircuit);
        Assert.DoesNotContain(reduced, result => result.Form == largest.Form && result.MismatchKind == "trace");

        var shrunk = report.Shrunk[0];
        Assert.Contains($"  replay:     HARDENING_SEED={settings.Seed} HARDENING_CASES=60 HARDENING_CASE={shrunk.Index} dotnet test --project test/Hardening.Specs", report.FailureText());
        Assert.Contains($"  regression: add [Row(\"{shrunk.Minimal.Case.Text.Replace("\"", "\\\"", StringComparison.Ordinal)}\")]", report.FailureText());
    }

    [VerifyTest]
    public async Task A_replay_runs_one_case_and_gives_the_same_mismatch()
    {
        var settings = new CampaignSettings(CampaignSettings.DefaultSeed, 60);
        var first = (await Campaign.RunAsync(settings, GeneratorMode.Rewrite, NoShortCircuit)).Shrunk[0];

        var replay = await Campaign.RunAsync(settings with { Case = first.Index }, GeneratorMode.Rewrite, NoShortCircuit);

        Assert.All(replay.Results, result => Assert.Equal(first.Original.Case.Text, result.Case.Text));
        Assert.Equal(
            (first.Original.Form, first.Original.MismatchKind, first.Original.Detail, first.Minimal.Case.Text),
            (replay.Shrunk[0].Original.Form, replay.Shrunk[0].Original.MismatchKind, replay.Shrunk[0].Original.Detail, replay.Shrunk[0].Minimal.Case.Text));
    }

    // A change to evaluation that a broken rewrite could make: the right side of && runs when the left
    // side is false.
    private static string NoShortCircuit(string text) => text.Replace(" && ", " & ", StringComparison.Ordinal);
}
