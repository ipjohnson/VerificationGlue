using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerificationGlue.Tests;

namespace VerificationGlue.Tests.AspNetCore;

/// <summary>
/// A started app, or a child of an app that a class or an assembly shares. Each has its own client and
/// service scope. Only the app's own container stops the app.
/// </summary>
/// <param name="host">The app's host.</param>
/// <param name="entryPoint">The running entry point, or null for a child.</param>
internal sealed class WebAppContainer(IHost host, Task<int>? entryPoint) : ITestContainer
{
    private readonly AsyncServiceScope _scope = host.Services.CreateAsyncScope();
    private readonly HttpClient _client = CreateClient(host);

    public IServiceProvider Services => _scope.ServiceProvider;

    internal IHost Host => host;

    internal HttpClient Client => _client;

    public bool TryResolve(TestParameter parameter, out object? value)
    {
        value = parameter.Type == typeof(HttpClient) ? _client
            : parameter.Type == typeof(IHost) ? host
            : parameter.ServiceKey is null ? _scope.ServiceProvider.GetService(parameter.Type)
            : (_scope.ServiceProvider as IKeyedServiceProvider)?.GetKeyedService(parameter.Type, parameter.ServiceKey);
        return value is not null;
    }

    /// <summary>
    /// A child for one test of a shared app. The context holds no overrides, because the bootstrap does
    /// not implement <see cref="ISupportsChildOverrides"/>.
    /// </summary>
    public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context) =>
        new(new WebAppContainer(host, null));

    /// <summary>
    /// A TestServer client, or under <see cref="KestrelAttribute"/> a client whose base address is the
    /// address Kestrel bound, which holds the real port once the server has started.
    /// </summary>
    private static HttpClient CreateClient(IHost host)
    {
        var server = host.Services.GetRequiredService<IServer>();
        if (server is TestServer testServer)
        {
            return testServer.CreateClient();
        }

        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException($"The app's server {server.GetType().Name} has no address in IServerAddressesFeature after the app started.");
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    /// <summary>
    /// Disposes the client and the scope. The app's own container then stops the app and returns when
    /// its entry point has returned.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _scope.DisposeAsync();
        if (entryPoint is null)
        {
            return;
        }

        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        await entryPoint;
    }
}
