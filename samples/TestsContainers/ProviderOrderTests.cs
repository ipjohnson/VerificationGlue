using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

[assembly: TestsContainers.Setting("assembly")]

namespace TestsContainers;

/// <summary>
/// Providers on the parameter, the method, the class and the assembly all offer a value. The narrowest
/// one that has a value supplies the parameter.
/// </summary>
[Setting("class")]
public partial class ProviderOrderTests
{
    [VerifyTest]
    [Setting("method")]
    public void The_parameter_provider_comes_first([Setting("parameter")] string value) => Verify(value == "parameter");

    [VerifyTest]
    [Setting("method")]
    public void The_method_provider_comes_before_the_class_and_the_assembly(string value) => Verify(value == "method");

    [VerifyTest]
    public void The_class_provider_comes_before_the_assembly(string value) => Verify(value == "class");
}

public partial class AssemblyProviderTests
{
    [VerifyTest]
    public void The_assembly_provider_supplies_what_nothing_narrower_does(string value) => Verify(value == "assembly");
}
