# Configure the app for a test

At the end of this page you can set a configuration value, the environment name and the configuration file watcher of the app that a test starts. You set each one with an attribute on the test, its class or its assembly.

The examples start the app from [Getting started with VerificationGlue.Tests.AspNetCore](getting-started-aspnetcore.md). Its `POST /orders` endpoint rejects an order larger than `Orders:MaxQuantity`, which its `appsettings.json` sets to 10.

## Set a configuration value

`[AppSetting(key, value)]` sets one value that the app reads from `IConfiguration`. Write one attribute for each key:

<!-- snippet: app-setting -->
<a id='snippet-app-setting'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/ConfigurationTests.cs#L16-L37' title='Snippet source file'>snippet source</a> | <a href='#snippet-app-setting' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The test's value replaces the value from `appsettings.json` and from environment variables. In the first test the app reads 5, so an order of 6 is rejected.

`[AppSetting]` also goes on the class or the assembly:

<!-- snippet: app-setting-assembly -->
<a id='snippet-app-setting-assembly'></a>
```cs
[assembly: AppSetting("Orders:MaxQuantity", "20")]
```
<sup><a href='/samples/TestsAspNetCore/ConfigurationTests.cs#L10-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-app-setting-assembly' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Values add up from the assembly to the class to the method. When two levels set the same key, the narrower value wins. In the example above, the second test reads 20 from the assembly, and the first test reads 5 from its method. A value for another key at any level is still present.

## Set the environment name

`[AppEnvironment(name)]` sets the name that the app sees in `IHostEnvironment`. Without it the name is `Development`:

<!-- snippet: app-environment -->
<a id='snippet-app-environment'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/ConfigurationTests.cs#L39-L57' title='Snippet source file'>snippet source</a> | <a href='#snippet-app-environment' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`[AppEnvironment]` goes on the test, the class or the assembly. The narrowest one wins, so the method's `Production` replaces the class's `Staging`.

## Watch the configuration files

The app does not watch its configuration files for changes. The watcher takes about 50 ms of each start. `[WatchAppConfiguration]` turns it back on for a test that changes a file while the app runs:

<!-- snippet: watch-app-configuration -->
<a id='snippet-watch-app-configuration'></a>
```cs
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
```
<sup><a href='/samples/TestsAspNetCore/ConfigurationTests.cs#L59-L72' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-app-configuration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`[WatchAppConfiguration]` goes on the test, the class or the assembly.

## How the values reach the app

The app receives each value as a command-line argument, such as `--Orders:MaxQuantity=5`, and the environment name as `--environment=Staging`. Command-line arguments are the last configuration source of `WebApplication.CreateBuilder(args)` and `Host.CreateDefaultBuilder(args)`, so they replace the values from files and environment variables. The ASP.NET Core templates pass `args` to the builder.

The values also reach an app that calls `WebApplication.CreateBuilder()` without `args`. The package adds them as one more configuration source when the app calls `Build()`. Such an app does not see these:

- A configuration value that its code reads before `Build()`, such as `builder.Configuration["Orders:MaxQuantity"]`. It reads the value from its own files and environment variables.
- The environment name. `IHostEnvironment` keeps the name from the `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT` variable, or `Production`.
- The configuration watcher setting. The app watches its files.

From `Build()` on, `IConfiguration` holds the test's values. `NoArgsAppTests` in [`test/VerificationGlue.Tests.AspNetCore.Fixture/ConfigurationTests.cs`](../../test/VerificationGlue.Tests.AspNetCore.Fixture/ConfigurationTests.cs) shows both cases. Pass `args` to the builder for the app to see all of them.

## Next

- [Getting started with VerificationGlue.Tests.AspNetCore](getting-started-aspnetcore.md)
- [Containers and scopes](containers-and-scopes.md)
- [The design of VerificationGlue.Tests.AspNetCore](../design/aspnet.md)
