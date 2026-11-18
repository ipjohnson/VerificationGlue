namespace VerificationGlue.Mocks.Generator.Specs;

public class ShapeSpecs
{
    public static IEnumerable<string> Cases => ShapeCase.Files();

    [VerifyTest]
    [RowSource(nameof(Cases))]
    public void Mocks_compile_and_report_the_expected_diagnostics(string file)
    {
        var shape = ShapeCase.Load(file);

        var result = shape.Compile();

        Assert.True(result.Errors.Count == 0, string.Join("\n", result.Errors));
        Assert.Equal(shape.Expected, result.DiagnosticIds);
    }

    [VerifyTest]
    public void Every_case_mocks_at_least_one_type()
    {
        var empty = ShapeCase.Files().Where(file => ShapeCase.Load(file).Compile().Outcomes.Count == 0).ToList();

        Assert.Empty(empty);
    }
}
