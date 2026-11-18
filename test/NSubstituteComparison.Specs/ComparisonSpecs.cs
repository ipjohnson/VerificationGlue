namespace NSubstituteComparison.Specs;

/// <summary>
/// The bounded comparison with NSubstitute. Each case must give the same observations under both
/// libraries, or carry a <see cref="DifferenceAttribute"/> that says why it does not.
/// </summary>
public class ComparisonSpecs
{
    public static IEnumerable<string> CaseIds => Comparison.Cases(Comparison.GlueCases).Select(@case => @case.Id);

    [VerifyTest, RowSource(nameof(CaseIds))]
    public async Task Each_case_matches_NSubstitute_or_records_why_it_differs(string id)
    {
        var result = await Comparison.Run(id);

        if (result.Differs && result.Case.Difference is null)
        {
            Assert.Fail($"The case differs from NSubstitute. Fix VerificationGlue.Mocks, or add [Difference] with the reason.\n{Comparison.Describe(result)}");
        }

        if (!result.Differs && result.Case.Difference is not null)
        {
            Assert.Fail($"The case records a difference, but both libraries now agree. Remove its [Difference].\n{Comparison.Describe(result)}");
        }
    }

    [VerifyTest]
    public void Both_case_assemblies_hold_the_same_cases()
    {
        Assert.Equal(
            Comparison.Cases(Comparison.NSubstituteCases).Select(@case => @case.Id),
            Comparison.Cases(Comparison.GlueCases).Select(@case => @case.Id));
    }

    [VerifyTest]
    public void Each_area_of_the_bounded_comparison_has_cases()
    {
        Assert.Equal(
            ["Argument matching", "Setup precedence", "Response sequences", "Async results", "Last-call hazards", "Parallel use"],
            Comparison.Cases(Comparison.GlueCases).Select(@case => @case.AreaTitle).Distinct());
    }
}
