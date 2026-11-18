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

/// <summary>A started app. The test gets its own client and service scope.</summary>
internal sealed class WebAppContainer(IHost host, Task<int> entryPoint) : ITestContainer
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

    public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context) =>
        throw new NotSupportedException("[WebApp<TEntry>] supports only Scope = ContainerScope.Test. An app shared by a class or an assembly is not built yet.");

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

    /// <summary>Stops the app and returns when its entry point has returned.</summary>
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _scope.DisposeAsync();
        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        await entryPoint;
    }
}
