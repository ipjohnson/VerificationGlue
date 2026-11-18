using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using ShopApi;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

#region app-setting-assembly
[assembly: AppSetting("Orders:MaxQuantity", "20")]
#endregion

namespace TestsAspNetCore;

#region app-setting
[WebApp<ShopApiEntry>]
public partial class MaxQuantityTests
{
    [VerifyTest]
    [AppSetting("Orders:MaxQuantity", "5")]
    public async Task Post_rejects_more_than_the_maximum(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(6));

        Verify(response.StatusCode == HttpStatusCode.BadRequest);
    }

    [VerifyTest]
    public async Task Post_takes_the_maximum_of_the_assembly(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(20));

        Verify(response.IsSuccessStatusCode);
    }
}
#endregion

[WebApp<ShopApiEntry>]
public partial class AppSettingsFileTests
{
    [VerifyTest]
    public void The_app_reads_its_appsettings_json(IConfiguration configuration)
    {
        var file = ((IConfigurationRoot)configuration).Providers.OfType<JsonConfigurationProvider>()
            .First(provider => provider.Source.Path == "appsettings.json");

        Verify(file.TryGet("Orders:MaxQuantity", out var maxQuantity) && maxQuantity == "10");
    }
}

#region app-environment
[WebApp<ShopApiEntry>]
[AppEnvironment("Staging")]
public partial class EnvironmentTests
{
    [VerifyTest]
    public void The_class_sets_the_environment_name(IHostEnvironment environment)
    {
        Verify(environment.IsStaging());
    }

    [VerifyTest]
    [AppEnvironment("Production")]
    public void The_method_name_wins_over_the_class_name(IHostEnvironment environment)
    {
        Verify(environment.IsProduction());
    }
}
#endregion

#region watch-app-configuration
[WebApp<ShopApiEntry>]
public partial class WatchTests
{
    [VerifyTest]
    [WatchAppConfiguration]
    public void The_app_reloads_appsettings_json_when_it_changes(IConfiguration configuration)
    {
        var files = ((IConfigurationRoot)configuration).Providers.OfType<FileConfigurationProvider>();

        Verify(files.All(file => file.Source.ReloadOnChange));
    }
}
#endregion
