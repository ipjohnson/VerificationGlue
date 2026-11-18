using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VerificationGlue.Tests;

namespace VerificationGlue.Tests.AspNetCore;

/// <summary>
/// Receives the host that an app's entry point builds. The hosting library announces each host on a
/// <see cref="DiagnosticListener"/> named Microsoft.Extensions.Hosting, which it creates on the thread
/// that builds the host, so each app's thread sees only its own host.
/// </summary>
internal sealed class HostCapture(IReadOnlyList<TestOverride> overrides, IReadOnlyDictionary<string, string?> settings, ITestOutput output) : IObserver<KeyValuePair<string, object?>>
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
            var builder = (IHostBuilder)value.Value!;
            if (settings.Count > 0)
            {
                // The entry point also receives the settings as arguments, but an app that does not
                // pass args to its builder sees them only through this source.
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            }

            builder.ConfigureServices((_, services) =>
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

    /// <summary>Replaces the console lifetime, which would stop the app on Ctrl+C and log to the console.</summary>
    private sealed class NoopLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

/// <summary>Writes the app's log to the output of the test the app was started for.</summary>
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
                output.WriteLine(exception is null
                    ? $"[{logLevel}] {category}: {formatter(state, exception)}"
                    : $"[{logLevel}] {category}: {formatter(state, exception)}\n{exception}");
            }
        }
    }
}
