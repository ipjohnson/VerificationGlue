using VerificationGlue.Mocks;

namespace MocksInTUnit;

#region mocks-hook-tunit
/// <summary>Passes a generated mock to each parameter of the test.</summary>
public sealed class MockDataAttribute : UntypedDataSourceGeneratorAttribute
{
    protected override IEnumerable<Func<object?[]?>> GenerateDataSources(DataGeneratorMetadata dataGeneratorMetadata)
    {
        yield return () => dataGeneratorMetadata.MembersToGenerate
            .OfType<ParameterMetadata>()
            .Select(parameter => (object?)MockRegistry.Create(parameter.Type))
            .ToArray();
    }
}
#endregion
