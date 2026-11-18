using System.Runtime.CompilerServices;
using FixtureShop;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

[WebApp<FixtureShopEntry>(Scope = ContainerScope.Class)]
[AppSetting("Fixture:Level", "class")]
[AppEnvironment("Shared")]
public partial class SharedClassTests
{
    [VerifyTest]
    public async Task First_test_of_the_shared_class(HttpClient client, IHost host, Basket basket)
    {
        AppLog.Reached(nameof(First_test_of_the_shared_class), host, client);
        await AppTests.PostAsync(client, 41);

        Verify(!basket.IsDisposed);
    }

    [VerifyTest]
    public async Task Second_test_of_the_shared_class(HttpClient client, IHost host, Basket basket)
    {
        AppLog.Reached(nameof(Second_test_of_the_shared_class), host, client);
        await AppTests.PostAsync(client, 42);

        Verify(!basket.IsDisposed);
    }

    [VerifyTest]
    public void The_shared_app_reads_the_class_and_assembly_attributes(IHost host, IConfiguration configuration, IHostEnvironment environment)
    {
        AppLog.Reached(nameof(The_shared_app_reads_the_class_and_assembly_attributes), host, null);

        Verify(configuration["Fixture:Level"] == "class");
        Verify(configuration["Fixture:Assembly"] == "assembly");
        Verify(environment.EnvironmentName == "Shared");
        Verify(host.Services.GetRequiredService<IServer>() is TestServer);
    }
}

[WebApp<FixtureShopEntry>(Scope = ContainerScope.Class)]
[Kestrel]
public partial class SharedKestrelTests
{
    [VerifyTest]
    public void First_test_of_the_shared_Kestrel_app(HttpClient client, IHost host)
    {
        AppLog.Reached(nameof(First_test_of_the_shared_Kestrel_app), host, client);

        Verify(host.Services.GetRequiredService<IServer>() is not TestServer);
        Verify(client.BaseAddress is { Host: "127.0.0.1", Port: > 0 });
    }

    [VerifyTest]
    public async Task Second_test_of_the_shared_Kestrel_app(HttpClient client, IHost host)
    {
        AppLog.Reached(nameof(Second_test_of_the_shared_Kestrel_app), host, client);

        Verify(client.BaseAddress is { Host: "127.0.0.1", Port: > 0 });
        Verify((await AppTests.PostAsync(client, 1)).Id > 0);
    }
}

[WebApp<FixtureShopEntry>(Scope = ContainerScope.Assembly)]
public partial class SharedAssemblyTests
{
    [VerifyTest]
    public void A_class_reaches_the_assembly_app(HttpClient client, IHost host, IConfiguration configuration)
    {
        AppLog.Reached(nameof(A_class_reaches_the_assembly_app), host, client);

        Verify(configuration["Fixture:Assembly"] == "assembly");
    }
}

[WebApp<FixtureShopEntry>(Scope = ContainerScope.Assembly)]
[AppSetting("Fixture:Class", "class")]
public partial class OtherSharedAssemblyTests
{
    [VerifyTest]
    public void Another_class_reaches_the_same_assembly_app(HttpClient client, IHost host, IConfiguration configuration)
    {
        AppLog.Reached(nameof(Another_class_reaches_the_same_assembly_app), host, client);

        Verify(configuration["Fixture:Assembly"] == "assembly");
        Verify(configuration["Fixture:Class"] is null);
    }
}

/// <summary>
/// Writes which app and which client each test reached, and when each app stopped, to the file that
/// VG_ASPNET_FIXTURE_LOG names. Apps and clients get a number the first time a test sees them.
/// </summary>
public static class AppLog
{
    private static readonly string? s_path = Environment.GetEnvironmentVariable("VG_ASPNET_FIXTURE_LOG");
    private static readonly Lock s_lock = new();
    private static readonly ConditionalWeakTable<object, StrongBox<int>> s_ids = new();
    private static int s_next;

    public static void Reached(string test, IHost host, HttpClient? client)
    {
        var app = Id(host, out var first);
        if (first)
        {
            host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.Register(() => Write($"app {app} stopped"));
        }

        Write(client is null ? $"{test} reached app {app}" : $"{test} reached app {app} with client {Id(client, out _)}");
    }

    private static int Id(object value, out bool first)
    {
        lock (s_lock)
        {
            first = !s_ids.TryGetValue(value, out var id);
            if (first)
            {
                id = new StrongBox<int>(++s_next);
                s_ids.Add(value, id);
            }

            return id!.Value;
        }
    }

    private static void Write(string line)
    {
        if (s_path is not null)
        {
            lock (s_lock)
            {
                File.AppendAllText(s_path, line + "\n");
            }
        }
    }
}
