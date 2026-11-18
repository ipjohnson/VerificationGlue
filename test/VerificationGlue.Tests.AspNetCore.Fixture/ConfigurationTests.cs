using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

[assembly: AppSetting("Fixture:Level", "assembly")]
[assembly: AppSetting("Fixture:Assembly", "assembly")]

namespace Fixture;

[WebAppEntry("NoArgsApp")]
public sealed partial class NoArgsAppEntry;

[WebApp<FixtureShopEntry>]
[AppSetting("Fixture:Level", "class")]
[AppSetting("Fixture:Class", "class")]
public partial class AppSettingTests
{
    [VerifyTest]
    public void Reads_the_value_from_appsettings_json(IConfiguration configuration)
    {
        Verify(configuration["Shop:Greeting"] == "from appsettings.json");
    }

    [VerifyTest]
    [AppSetting("Shop:Greeting", "from the test")]
    public void A_test_value_replaces_the_value_from_appsettings_json(IConfiguration configuration)
    {
        Verify(configuration["Shop:Greeting"] == "from the test");
    }

    [VerifyTest]
    [AppSetting("Fixture:Level", "method")]
    [AppSetting("Fixture:Method", "method")]
    public void The_narrowest_value_of_a_key_wins_and_the_other_keys_add_up(IConfiguration configuration)
    {
        Verify(configuration["Fixture:Level"] == "method");
        Verify(configuration["Fixture:Assembly"] == "assembly");
        Verify(configuration["Fixture:Class"] == "class");
        Verify(configuration["Fixture:Method"] == "method");
    }

    [VerifyTest]
    public void Without_a_method_value_the_class_value_wins(IConfiguration configuration)
    {
        Verify(configuration["Fixture:Level"] == "class");
    }
}

[WebApp<FixtureShopEntry>]
public partial class AppEnvironmentTests
{
    [VerifyTest]
    public void The_default_environment_is_Development(IHostEnvironment environment)
    {
        Verify(environment.EnvironmentName == "Development");
    }

    [VerifyTest]
    [AppEnvironment("Testing")]
    public void A_test_sets_the_environment_name(IHostEnvironment environment)
    {
        Verify(environment.EnvironmentName == "Testing");
    }

    /// <summary>
    /// An assembly attribute would change the environment of every test in this fixture, so this test
    /// passes the attributes of each scope to the bootstrap itself, widest first, as the runner does.
    /// </summary>
    [VerifyTest]
    public async Task The_narrowest_of_the_assembly_class_and_method_wins()
    {
        Verify(await EnvironmentOfAsync([new AppEnvironmentAttribute("Assembly")]) == "Assembly");
        Verify(await EnvironmentOfAsync([new AppEnvironmentAttribute("Assembly"), new AppEnvironmentAttribute("Class")]) == "Class");
        Verify(await EnvironmentOfAsync([new AppEnvironmentAttribute("Assembly"), new AppEnvironmentAttribute("Class"), new AppEnvironmentAttribute("Method")]) == "Method");
    }

    private static async Task<string> EnvironmentOfAsync(Attribute[] attributes)
    {
        await using var app = await WebAppBootstrap<FixtureShopEntry>.StartAsync("environment", attributes, [], new NoOutput(), CancellationToken.None);
        return app.Host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName;
    }

    private sealed class NoOutput : ITestOutput
    {
        public void WriteLine(string message)
        {
        }
    }
}

[WebApp<FixtureShopEntry>]
[AppEnvironment("Staging")]
public partial class ClassAppEnvironmentTests
{
    [VerifyTest]
    public void A_class_sets_the_environment_name(IHostEnvironment environment)
    {
        Verify(environment.EnvironmentName == "Staging");
    }

    [VerifyTest]
    [AppEnvironment("Testing")]
    public void A_test_name_replaces_the_class_name(IHostEnvironment environment)
    {
        Verify(environment.EnvironmentName == "Testing");
    }
}

[WebApp<FixtureShopEntry>]
public partial class WatchAppConfigurationTests
{
    [VerifyTest]
    [WatchAppConfiguration]
    public void Watches_its_configuration_files(IConfiguration configuration)
    {
        var files = ((IConfigurationRoot)configuration).Providers.OfType<FileConfigurationProvider>().ToArray();

        Verify(files.Length > 0);
        Verify(files.All(file => file.Source.ReloadOnChange));
    }
}

[WebApp<NoArgsAppEntry>]
public partial class NoArgsAppTests
{
    [VerifyTest]
    [AppSetting("Fixture:Setting", "from the test")]
    public async Task An_app_that_does_not_pass_args_reads_the_value_after_Build(HttpClient client)
    {
        Verify(await client.GetStringAsync("/setting") == "before Build: none, after Build: from the test");
    }

    [VerifyTest]
    [AppEnvironment("Testing")]
    public async Task An_app_that_does_not_pass_args_keeps_its_own_environment_name(HttpClient client)
    {
        Verify(await client.GetStringAsync("/environment") == "Production");
    }

    [VerifyTest]
    public void An_app_that_does_not_pass_args_watches_its_configuration_files(IConfiguration configuration)
    {
        var files = ((IConfigurationRoot)configuration).Providers.OfType<FileConfigurationProvider>().ToArray();

        Verify(files.Length > 0);
        Verify(files.All(file => file.Source.ReloadOnChange));
    }
}
