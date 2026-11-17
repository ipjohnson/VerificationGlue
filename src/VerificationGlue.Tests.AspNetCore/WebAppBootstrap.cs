using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerificationGlue.Tests;

namespace VerificationGlue.Tests.AspNetCore;

/// <summary>
/// Starts the app that <typeparamref name="TEntry"/> names: it calls the entry point on its own thread,
/// adds TestServer, the test's overrides and a logger that writes to the test's output while the host
/// builds, and returns when the app has started.
/// </summary>
/// <typeparam name="TEntry">The app's entry type.</typeparam>
public sealed class WebAppBootstrap<TEntry> : ITestContainerBootstrap
    where TEntry : IWebAppEntry
{
    /// <inheritdoc/>
    public static async ValueTask<ITestContainer> BuildAsync(TestContainerContext context)
    {
        var clock = Stopwatch.StartNew();
        var capture = new HostCapture(context.Overrides, context.Output);
        string[] args =
        [
            $"--applicationName={TEntry.ApplicationName}",
            "--environment=Development",
            // The configuration file watcher takes about 50 ms of each start.
            "--hostBuilder:reloadConfigOnChange=false",
        ];

        var entryPoint = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            HostCapture.Current = capture;
            try
            {
                entryPoint.TrySetResult(TEntry.Run(args));
            }
            catch (Exception failure)
            {
                entryPoint.TrySetException(failure);
            }
        })
        {
            IsBackground = true,
            Name = $"{TEntry.ApplicationName} for {context.Uid}",
        };
        thread.Start();

        if (await Task.WhenAny(capture.Host, entryPoint.Task).WaitAsync(context.CancellationToken) == entryPoint.Task)
        {
            throw ReturnedEarly(await entryPoint.Task, "before it built a host");
        }

        var host = await capture.Host;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() => started.TrySetResult()))
        {
            if (await Task.WhenAny(started.Task, entryPoint.Task).WaitAsync(context.CancellationToken) == entryPoint.Task)
            {
                throw ReturnedEarly(await entryPoint.Task, "before its host started");
            }
        }

        context.Output.WriteLine($"{TEntry.ApplicationName} started in {clock.Elapsed.TotalMilliseconds:F1} ms");
        return new WebAppContainer(host, entryPoint.Task);
    }

    private static InvalidOperationException ReturnedEarly(int exitCode, string when) =>
        new($"The entry point of {TEntry.ApplicationName} returned {exitCode} {when}. " +
            "What the app wrote to standard error went to the console of the test process.");
}
