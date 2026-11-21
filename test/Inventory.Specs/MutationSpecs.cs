namespace Inventory.Specs;

public class MutationSpecs
{
    public const string TableHeading = "### Targeted mutations as built";

    private static readonly Mutation Sample = new()
    {
        Id = "MUT-99",
        Kind = "Reverse setup precedence",
        Change = "c",
        File = "src/VerificationGlue.Mocks/MockAdapter.cs",
        Find = "before",
        Replace = "after",
    };

    private static readonly RunResult[] Baseline =
    [
        Run("solution", 0, ("A.First", TestOutcome.Passed), ("A.Second", TestOutcome.Passed)),
        Run("hardening", 0, ("B.Third", TestOutcome.Passed)),
    ];

    [VerifyTest]
    public void The_checked_in_catalog_applies_to_the_current_source()
    {
        Assert.Empty(MutationCatalog.Load(RepositoryRoot.Path).Validate(RepositoryRoot.Path));
    }

    [VerifyTest]
    public void The_mutation_table_in_the_design_doc_matches_the_catalog()
    {
        var catalog = MutationCatalog.Load(RepositoryRoot.Path);
        var rows = MarkdownTable.Read(File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs/design/hardening.md")), TableHeading);

        Assert.NotNull(rows);
        Assert.Equal(
            catalog.Mutations.Select(mutation => $"{mutation.Id} | {mutation.Kind} | {mutation.Change} | {MutationReport.ExplanationText(mutation.Explanation).Replace("`", "", StringComparison.Ordinal)}"),
            rows.Select(row => $"{row[0]} | {row[1]} | {row[2]} | {row[4]}"));
    }

    [VerifyTest]
    public void A_mutation_replaces_its_text_only_when_the_text_is_there_once()
    {
        Assert.Equal("x after y", Sample.Apply("x before y"));
        Assert.Null(Sample.Apply("x y"));
        Assert.Null(Sample.Apply("before before"));
    }

    [VerifyTest]
    public void A_catalog_needs_each_named_kind_and_text_that_is_in_its_file_once()
    {
        var catalog = new MutationCatalog { Mutations = [Sample, Sample with { Kind = "Break everything" }] };

        var problems = catalog.Validate(RepositoryRoot.Path);

        Assert.Contains("MUT-99 is used by more than one mutation.", problems);
        Assert.Contains("No mutation is of the kind 'Evaluate a skipped operand'.", problems);
        Assert.Contains("MUT-99 has the kind 'Break everything', which docs/design/hardening.md does not name.", problems);
        Assert.Contains("MUT-99: the text it replaces is not in src/VerificationGlue.Mocks/MockAdapter.cs exactly once.", problems);
    }

    [VerifyTest]
    public void A_case_that_fails_or_no_longer_runs_detects_the_mutation_and_is_named()
    {
        RunResult[] mutated =
        [
            Run("solution", 1, ("A.First", TestOutcome.Failed), ("A.Second", TestOutcome.Passed)),
            Run("hardening", 1),
        ];

        var result = MutationResult.Classify(Sample, Baseline, mutated);

        Assert.Equal(MutationStatus.Detected, result.Status);
        Assert.Equal(["A.First", "B.Third (not run)"], result.FailingCases);
        Assert.Equal(["solution exited with 1", "hardening exited with 1"], result.FailedRuns);
        Assert.Empty(result.Problems);
        Assert.Equal("MUT-99 Reverse setup precedence: detected by `A.First`, `B.Third (not run)`", MutationReport.Line(result));
    }

    [VerifyTest]
    public void A_command_that_fails_or_times_out_detects_the_mutation_without_a_case()
    {
        RunResult[] mutated =
        [
            Run("solution", 0, ("A.First", TestOutcome.Passed), ("A.Second", TestOutcome.Passed)),
            new("hardening", -1, true, [new CaseResult("B.Third", TestOutcome.Passed)], []),
        ];

        var result = MutationResult.Classify(Sample, Baseline, mutated);

        Assert.Equal(MutationStatus.Detected, result.Status);
        Assert.Empty(result.FailingCases);
        Assert.Equal(["hardening timed out"], result.FailedRuns);
    }

    [VerifyTest]
    public void A_mutation_no_case_catches_survives_and_fails_the_run_without_an_explanation()
    {
        var result = MutationResult.Classify(Sample, Baseline, Baseline);

        Assert.Equal(MutationStatus.Survived, result.Status);
        Assert.Equal(["MUT-99 survived, and docs/design/hardening-mutations.json records no explanation."], result.Problems);

        var equivalent = Sample with { Explanation = new Explanation { Kind = Explanation.Equivalent, Reason = "r" } };
        Assert.Empty(MutationResult.Classify(equivalent, Baseline, Baseline).Problems);
    }

    [VerifyTest]
    public void An_explanation_must_agree_with_the_result()
    {
        RunResult[] caught = [Run("solution", 1, ("A.First", TestOutcome.Failed), ("A.Second", TestOutcome.Passed)), Baseline[1]];
        var missing = Sample with { Explanation = new Explanation { Kind = Explanation.MissingAssertion, Case = "A.First", Reason = "r" } };
        var elsewhere = Sample with { Explanation = new Explanation { Kind = Explanation.MissingAssertion, Case = "A.Second", Reason = "r" } };
        var equivalent = Sample with { Explanation = new Explanation { Kind = Explanation.Equivalent, Reason = "r" } };

        Assert.Empty(MutationResult.Classify(missing, Baseline, caught).Problems);
        Assert.Equal(["MUT-99 is not caught by A.Second, the case its explanation names."], MutationResult.Classify(elsewhere, Baseline, caught).Problems);
        Assert.Equal(["MUT-99 is not caught by A.First, the case its explanation names."], MutationResult.Classify(missing, Baseline, Baseline).Problems);
        Assert.Equal(["MUT-99 is caught, so the explanation that it is an equivalent mutation is out of date."], MutationResult.Classify(equivalent, Baseline, caught).Problems);
    }

    [VerifyTest]
    public void A_case_name_matches_with_or_without_its_namespace_and_rows()
    {
        Assert.True(MutationResult.Names("Ns.Specs.Adds(a: 1)", "Specs.Adds"));
        Assert.True(MutationResult.Names("Ns.Specs.Adds (not run)", "Ns.Specs.Adds"));
        Assert.False(MutationResult.Names("Ns.Specs.Adds_more", "Specs.Adds"));
    }

    [VerifyTest]
    public void A_failure_without_a_mutation_stops_the_run()
    {
        RunResult[] failing = [Run("solution", 1, ("A.First", TestOutcome.Failed)), Baseline[1]];

        Assert.Empty(MutationResult.BaselineProblems(Baseline));
        Assert.Equal(["Without a mutation, solution exited with 1.", "Without a mutation, A.First fails."], MutationResult.BaselineProblems(failing));
    }

    private static RunResult Run(string name, int exitCode, params (string Name, string Outcome)[] cases) =>
        new(name, exitCode, false, [.. cases.Select(result => new CaseResult(result.Name, result.Outcome))], []);
}
