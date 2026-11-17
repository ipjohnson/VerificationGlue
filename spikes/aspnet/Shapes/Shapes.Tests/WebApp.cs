using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VerificationGlue.Tests;

namespace Shapes.Tests;

/// <summary>Names an app's entry point so the bootstrap can call it with no reflection.</summary>
public interface IWebAppEntry
{
    /// <summary>The name of the app's assembly.</summary>
    static abstract string ApplicationName { get; }

    /// <summary>Calls the app's entry point and returns its exit code. It returns when the app stops.</summary>
    static abstract int Run(string[] args);
}

/// <summary>Asks the entry generator to fill in this partial class for the named app assembly.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class WebAppEntryAttribute(string assemblyName) : Attribute
{
    public string AssemblyName => assemblyName;
}

public sealed class WebAppAttribute<TEntry> : ContainerAttribute<WebAppBootstrap<TEntry>>
    where TEntry : IWebAppEntry;

public sealed class WebAppBootstrap<TEntry> : ITestContainerBootstrap
    where TEntry : IWebAppEntry
{
    public static async ValueTask<ITestContainer> BuildAsync(TestContainerContext context) =>
        await WebApp.StartAsync<TEntry>(context.Overrides, context.Output);
}

public static class WebApp
{
    private static readonly SemaphoreSlim s_build = new(1);
    private static int s_starting;
    private static int s_mostStartingAtOnce;

    /// <summary>
    /// With WEBAPP_SERIAL_BUILD=1, one entry point at a time runs until its HostBuilt event. Under
    /// Native AOT, hosts that build at once can fail inside the framework (see ConcurrentBuild).
    /// </summary>
    public static bool SerialBuild { get; } = Environment.GetEnvironmentVariable("WEBAPP_SERIAL_BUILD") == "1";

    /// <summary>The most entry points that were between their start and their HostBuilt event at the same time.</summary>
    public static int MostStartingAtOnce => Volatile.Read(ref s_mostStartingAtOnce);

    /// <summary>Starts the app on TestServer with the overrides, and returns when the host has started.</summary>
    public static async Task<RunningApp> StartAsync<TEntry>(IReadOnlyList<TestOverride> overrides, ITestOutput output)
        where TEntry : IWebAppEntry
    {
        var clock = Stopwatch.StartNew();
        var capture = new HostCapture(overrides, output);
        string[] args =
        [
            "--environment=Development",
            $"--applicationName={TEntry.ApplicationName}",
            "--hostBuilder:reloadConfigOnChange=false",
        ];

        var main = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            HostCapture.Current = capture;
            if (SerialBuild)
            {
                s_build.Wait();
            }

            InterlockedMax(ref s_mostStartingAtOnce, Interlocked.Increment(ref s_starting));
            var counted = 1;
            capture.Host.ContinueWith(_ => Done(), TaskScheduler.Default);
            try
            {
                main.TrySetResult(TEntry.Run(args));
            }
            catch (Exception failure)
            {
                main.TrySetException(failure);
            }
            finally
            {
                Done();
            }

            void Done()
            {
                if (Interlocked.Exchange(ref counted, 0) == 1)
                {
                    Interlocked.Decrement(ref s_starting);
                    if (SerialBuild)
                    {
                        s_build.Release();
                    }
                }
            }
        })
        {
            IsBackground = true,
            Name = $"app {TEntry.ApplicationName}",
        };
        thread.Start();

        if (await Task.WhenAny(capture.Host, main.Task) == main.Task)
        {
            var code = await main.Task;
            throw new InvalidOperationException($"The entry point of {TEntry.ApplicationName} returned {code} before it built a host.");
        }

        var host = await capture.Host;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        if (await Task.WhenAny(started.Task, main.Task) == main.Task)
        {
            var code = await main.Task;
            throw new InvalidOperationException($"The entry point of {TEntry.ApplicationName} returned {code} before the host started.");
        }

        output.WriteLine($"{TEntry.ApplicationName} started in {clock.Elapsed.TotalMilliseconds:F1} ms");
        return new RunningApp(host, main.Task);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var seen = Interlocked.CompareExchange(ref target, value, current);
            if (seen == current)
            {
                return;
            }

            current = seen;
        }
    }
}

/// <summary>
/// Receives the host that the app's entry point builds. The hosting library announces each host on
/// a DiagnosticListener named Microsoft.Extensions.Hosting, on the thread that builds it.
/// </summary>
internal sealed class HostCapture(IReadOnlyList<TestOverride> overrides, ITestOutput output) : IObserver<KeyValuePair<string, object?>>
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
                services.AddSingleton<IServer, TestServer>();
                services.RemoveAll<ILoggerProvider>();
                services.AddSingleton<ILoggerProvider>(new OutputLoggerProvider(output));
                foreach (var entry in overrides)
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

/// <summary>The started app.</summary>
public sealed class RunningApp(IHost host, Task<int> main) : ITestContainer
{
    private readonly AsyncServiceScope _scope = host.Services.CreateAsyncScope();

    public IHost Host => host;

    public HttpClient Client { get; } = ((TestServer)host.Services.GetRequiredService<IServer>()).CreateClient();

    public IServiceProvider Services => _scope.ServiceProvider;

    public bool TryResolve(TestParameter parameter, out object? value)
    {
        value = parameter.Type == typeof(HttpClient) ? Client
            : parameter.Type == typeof(IHost) ? host
            : _scope.ServiceProvider.GetService(parameter.Type);
        return value is not null;
    }

    public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context) => throw new NotSupportedException();

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _scope.DisposeAsync();
        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        await main;
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
