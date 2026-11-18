using VerificationGlue.Generator;

namespace VerificationGlue.Generator.Specs;

public class VerificationGlueGeneratorSpecs
{
    [VerifyTest]
    public void Runs_without_output_or_diagnostics()
    {
        var result = GeneratorHarness.Run(new VerificationGlueGenerator(), "class C { }");

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GeneratedTrees);
    }
}
