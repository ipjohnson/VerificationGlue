namespace VerificationGlue.Tests.AspNetCore.Specs;

/// <summary>
/// <c>[Kestrel]</c>, checked through the TRX results of the tests in
/// test/VerificationGlue.Tests.AspNetCore.Fixture/KestrelTests.cs.
/// </summary>
public class KestrelSpecs
{
    [Fact]
    public void A_test_with_Kestrel_gets_an_app_on_a_free_loopback_port_that_its_client_names()
    {
        AssertPassed("Listens_on_a_free_loopback_port_that_the_client_names");
    }

    [Fact]
    public void A_client_the_test_makes_reaches_the_app_at_that_address()
    {
        AssertPassed("A_client_the_test_makes_reaches_the_app");
    }

    [Fact]
    public void A_mock_parameter_reaches_the_app_on_Kestrel()
    {
        AssertPassed("The_mock_reaches_the_app_on_Kestrel");
    }

    [Fact]
    public void Kestrel_on_the_class_or_the_assembly_applies_to_every_test_under_it()
    {
        AssertPassed("Kestrel_on_the_class_applies_to_each_test");
        AssertPassed("Kestrel_on_the_class_applies_to_another_test");
        AssertPassed("Kestrel_on_the_assembly_applies_to_the_tests_under_it");
    }

    [Fact]
    public void A_test_without_Kestrel_stays_on_TestServer()
    {
        AssertPassed("A_test_without_Kestrel_stays_on_TestServer");
    }

    [Fact]
    public void An_app_that_does_not_pass_args_to_its_builder_listens_on_the_loopback_port()
    {
        AssertPassed("An_app_that_does_not_pass_args_listens_on_the_loopback_port");
    }

    private static void AssertPassed(string test)
    {
        var result = Fixture.Results[test];
        Assert.True(result.Outcome == "Passed", $"{test}: {result.Outcome}\n{result.Message}\n{result.Output}");
    }
}
