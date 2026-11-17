using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VerificationGlue.Tests;

namespace ShopApi.Tests;

/// <summary>Names an app's entry point so the bootstrap can call it with no reflection.</summary>
public interface IWebAppEntry
{
    /// <summary>The name of the app's assembly. MVC finds the app's controllers by it.</summary>
    static abstract string ApplicationName { get; }

    static abstract void Run(string[] args);
}

public interface IWebAppOptions
{
    bool Kestrel { get; }
}

public sealed class WebAppAttribute<TEntry> : ContainerAttribute<WebAppBootstrap<TEntry>>, IWebAppOptions
    where TEntry : IWebAppEntry
{
    /// <summary>Listens on a free loopback port with Kestrel. The default is TestServer, which uses no socket.</summary>
    public bool Kestrel { get; set; }
}

/// <summary>Starts the app for the scope, on TestServer, with the scope's overrides.</summary>
public sealed class WebAppBootstrap<TEntry> : ITestContainerBootstrap
    where TEntry : IWebAppEntry
{
    public static async ValueTask<ITestContainer> BuildAsync(TestContainerContext context)
    {
        var clock = Stopwatch.StartNew();
        var kestrel = context.Attributes.OfType<IWebAppOptions>().LastOrDefault()?.Kestrel ?? false;
        var capture = new HostCapture(context, kestrel);
        List<string> arguments = ["--environment=Development", $"--applicationName={TEntry.ApplicationName}"];
        if (Environment.GetEnvironmentVariable("WEBAPP_WATCH_CONFIG") != "1")
        {
            // The default builder watches appsettings.json for changes, and setting up the watcher takes about 50 ms a start.
            arguments.Add("--hostBuilder:reloadConfigOnChange=false");
        }

        if (kestrel)
        {
            arguments.Add("--urls=http://127.0.0.1:0");
        }

        string[] args = [.. arguments];
        var main = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            HostCapture.Current = capture;
            try
            {
                TEntry.Run(args);
                main.TrySetResult();
            }
            catch (Exception failure)
            {
                main.TrySetException(failure);
            }
        })
        {
            IsBackground = true,
            Name = $"app {context.Uid}",
        };
        thread.Start();

        var built = await Task.WhenAny(capture.Host, main.Task);
        if (built == main.Task)
        {
            await main.Task;
            throw new InvalidOperationException("The app's entry point returned before it built a host.");
        }

        var host = await capture.Host;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        if (await Task.WhenAny(started.Task, main.Task) == main.Task)
        {
            await main.Task;
            throw new InvalidOperationException("The app's entry point returned before the host started.");
        }

        WebAppStats.Record(clock.Elapsed);
        context.Output.WriteLine($"App started in {clock.Elapsed.TotalMilliseconds:F1} ms");
        return new RunningApp(host, main.Task);
    }
}

public static class WebAppStats
{
    private static readonly List<TimeSpan> s_starts = [];

    static WebAppStats() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Console.Error.WriteLine(Summary());

    public static void Record(TimeSpan elapsed)
    {
        lock (s_starts)
        {
            s_starts.Add(elapsed);
        }
    }

    public static string Summary()
    {
        lock (s_starts)
        {
            var sorted = s_starts.Select(start => start.TotalMilliseconds).Order().ToArray();
            return sorted.Length == 0
                ? "no app starts"
                : $"{sorted.Length} app starts: first {s_starts[0].TotalMilliseconds:F1} ms, median {sorted[sorted.Length / 2]:F1} ms, max {sorted[^1]:F1} ms";
        }
    }
}

/// <summary>
/// Receives the host that the app's entry point builds. The hosting library announces each host on
/// a DiagnosticListener named Microsoft.Extensions.Hosting, on the thread that builds it.
/// </summary>
internal sealed class HostCapture(TestContainerContext context, bool kestrel) : IObserver<KeyValuePair<string, object?>>
{
    private static readonly AsyncLocal<HostCapture?> s_current = new();
    private static readonly IDisposable s_subscription = DiagnosticListener.AllListeners.Subscribe(new Listeners());
    private readonly TaskCompletionSource<IHost> _host = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static HostCapture? Current
    {
        get => s_current.Value;
        set
        {
            GC.KeepAlive(s_subscription);
            s_current.Value = value;
        }
    }

    public Task<IHost> Host => _host.Task;

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == "HostBuilding")
        {
            ((IHostBuilder)value.Value!).ConfigureServices((_, services) =>
            {
                services.AddSingleton<IHostLifetime, NoopLifetime>();
                if (!kestrel)
                {
                    services.AddSingleton<IServer, TestServer>();
                }

                services.RemoveAll<ILoggerProvider>();
                services.AddSingleton<ILoggerProvider>(new OutputLoggerProvider(context.Output));
                foreach (var entry in context.Overrides)
                {
                    if (entry.Key is null)
                    {
                        services.AddSingleton(entry.ServiceType, entry.Instance);
                    }
                    else
                    {
                        services.AddKeyedSingleton(entry.ServiceType, entry.Key, entry.Instance);
                    }
                }
            });
        }
        else if (value.Key == "HostBuilt")
        {
            _host.TrySetResult((IHost)value.Value!);
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    private sealed class Listeners : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.Extensions.Hosting" && s_current.Value is { } capture)
            {
                listener.Subscribe(capture);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }

    private sealed class NoopLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

/// <summary>The started app. A child of it shares the app and has its own service scope.</summary>
internal sealed class RunningApp(IHost host, Task main) : ITestContainer
{
    private readonly AsyncServiceScope _scope = host.Services.CreateAsyncScope();
    private readonly HttpClient _client = CreateClient(host);

    public static HttpClient CreateClient(IHost host)
    {
        var server = host.Services.GetRequiredService<IServer>();
        return server is TestServer test
            ? test.CreateClient()
            : new HttpClient { BaseAddress = new Uri(server.Features.Get<IServerAddressesFeature>()!.Addresses.First()) };
    }

    public IServiceProvider Services => _scope.ServiceProvider;

    public bool TryResolve(TestParameter parameter, out object? value)
    {
        value = parameter.Type == typeof(HttpClient) ? _client
            : parameter.Type == typeof(IHost) ? host
            : parameter.ServiceKey is null ? _scope.ServiceProvider.GetService(parameter.Type)
            : (_scope.ServiceProvider as IKeyedServiceProvider)?.GetKeyedService(parameter.Type, parameter.ServiceKey);
        return value is not null;
    }

    public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context) => new(new Child(host));

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _scope.DisposeAsync();
        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        await main;
    }

    private sealed class Child(IHost host) : ITestContainer
    {
        private readonly AsyncServiceScope _scope = host.Services.CreateAsyncScope();
        private readonly HttpClient _client = CreateClient(host);

        public IServiceProvider Services => _scope.ServiceProvider;

        public bool TryResolve(TestParameter parameter, out object? value)
        {
            value = parameter.Type == typeof(HttpClient) ? _client : _scope.ServiceProvider.GetService(parameter.Type);
            return value is not null;
        }

        public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context) => throw new NotSupportedException();

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _scope.DisposeAsync();
        }
    }
}

internal sealed class OutputLoggerProvider(ITestOutput output) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new OutputLogger(output, categoryName);

    public void Dispose()
    {
    }

    private sealed class OutputLogger(ITestOutput output, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                output.WriteLine($"[{logLevel}] {category}: {formatter(state, exception)}");
            }
        }
    }
}
