namespace VerificationGlue.Tests.AspNetCore.Specs;

/// <summary>
/// <c>[AppSetting]</c>, <c>[AppEnvironment]</c> and <c>[WatchAppConfiguration]</c>, checked through the TRX
/// results of the tests in test/VerificationGlue.Tests.AspNetCore.Fixture/ConfigurationTests.cs.
/// </summary>
public class ConfigurationSpecs
{
    [VerifyTest]
    public void A_test_value_is_what_the_app_reads_and_replaces_the_value_from_appsettings_json()
    {
        AssertPassed("Reads_the_value_from_appsettings_json");
        AssertPassed("A_test_value_replaces_the_value_from_appsettings_json");
    }

    [VerifyTest]
    public void The_app_reads_appsettings_json_from_the_test_output_directory_when_the_current_directory_is_another()
    {
        AssertPassed("The_content_root_is_the_test_output_directory");
        AssertPassed("Reads_the_value_from_appsettings_json");
    }

    [VerifyTest]
    public void The_narrowest_value_of_a_key_wins_and_values_of_other_keys_from_each_level_are_present()
    {
        AssertPassed("The_narrowest_value_of_a_key_wins_and_the_other_keys_add_up");
        AssertPassed("Without_a_method_value_the_class_value_wins");
    }

    [VerifyTest]
    public void The_environment_name_is_the_narrowest_one_and_defaults_to_Development()
    {
        AssertPassed("The_default_environment_is_Development");
        AssertPassed("A_test_sets_the_environment_name");
        AssertPassed("A_class_sets_the_environment_name");
        AssertPassed("A_test_name_replaces_the_class_name");
        AssertPassed("The_narrowest_of_the_assembly_class_and_method_wins");
    }

    [VerifyTest]
    public void An_app_that_does_not_pass_args_to_its_builder_reads_a_test_value_after_Build()
    {
        AssertPassed("An_app_that_does_not_pass_args_reads_the_value_after_Build");
        AssertPassed("An_app_that_does_not_pass_args_keeps_its_own_environment_name");
        AssertPassed("An_app_that_does_not_pass_args_watches_its_configuration_files");
    }

    [VerifyTest]
    public void The_app_watches_its_configuration_files_only_with_WatchAppConfiguration()
    {
        AssertPassed("Does_not_watch_its_configuration_files");
        AssertPassed("Watches_its_configuration_files");
    }

    private static void AssertPassed(string test)
    {
        var result = Fixture.Results[test];
        Assert.True(result.Outcome == "Passed", $"{test}: {result.Outcome}\n{result.Message}\n{result.Output}");
    }
}
