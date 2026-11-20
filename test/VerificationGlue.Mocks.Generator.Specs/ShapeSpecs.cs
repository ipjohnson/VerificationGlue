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
    public void A_type_nested_in_a_generic_type_with_extern_aliased_type_arguments_is_refused_with_its_own_reason()
    {
        var outcomes = ShapeCase.Load("ExternAliasNested.cs").Compile().Outcomes.ToDictionary(o => o.TypeName);

        const string Reason = "is nested in a generic type whose type arguments come from an extern-aliased assembly, and the generator cannot write its name";
        Assert.Equal(
            $"'Shapes.ExternAliasNested.IReadsNested' cannot be mocked: 'Vendor.Ledger<Vendor.Payment>.Line' {Reason}",
            Assert.Single(outcomes["Shapes.ExternAliasNested.IReadsNested"].Diagnostics, d => d.Id == "VGM0001").Message);
        Assert.Equal(
            $"'Shapes.ExternAliasNested.IEnumeratesAliased' cannot be mocked: 'System.Collections.Generic.List<Vendor.Payment>.Enumerator' {Reason}",
            Assert.Single(outcomes["Shapes.ExternAliasNested.IEnumeratesAliased"].Diagnostics, d => d.Id == "VGM0001").Message);
        Assert.Equal(
            $"'Vendor.Ledger<Vendor.Payment>.IEntry' cannot be mocked: 'Vendor.Ledger<Vendor.Payment>.IEntry' {Reason}",
            Assert.Single(outcomes["Vendor.Ledger<Vendor.Payment>.IEntry"].Diagnostics, d => d.Id == "VGM0001").Message);

        var local = outcomes["Vendor.Ledger<Shapes.ExternAliasNested.Local>.IEntry"];
        Assert.False(local.Refused);
        Assert.Empty(local.Diagnostics);
    }

    [VerifyTest]
    public void Every_case_mocks_at_least_one_type()
    {
        var empty = ShapeCase.Files().Where(file => ShapeCase.Load(file).Compile().Outcomes.Count == 0).ToList();

        Assert.Empty(empty);
    }
}
