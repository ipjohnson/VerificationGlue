using VerificationGlue.Mocks.Generator;

namespace VerificationGlue.Mocks.Generator.Specs;

public class MockGeneratorSpecs
{
    [Fact]
    public void Runs_without_output_or_diagnostics()
    {
        var result = GeneratorHarness.Run(new MockGenerator(), "class C { }");

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GeneratedTrees);
    }
}
