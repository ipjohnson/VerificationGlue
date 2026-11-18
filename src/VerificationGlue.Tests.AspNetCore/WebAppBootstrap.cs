using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    /// <summary>
    /// Starts the app for a test, or for a class or an assembly that shares it. A shared app reads the
    /// attributes of its scope, which are the class's and the assembly's or the assembly's alone.
    /// </summary>
    /// <param name="context">The scope, its attributes and the test's overrides.</param>
    public static async ValueTask<ITestContainer> BuildAsync(TestContainerContext context) =>
        await StartAsync(context.Uid, context.Attributes, context.Overrides, context.Output, context.CancellationToken, shared: context.Scope.Kind != TestScopeKind.Test);

    /// <summary>
    /// Starts the app with the configuration that <paramref name="attributes"/> set, <paramref name="overrides"/>
    /// and its log in <paramref name="output"/>.
    /// </summary>
    internal static async Task<WebAppContainer> StartAsync(string uid, IReadOnlyList<Attribute> attributes, IReadOnlyList<TestOverride> overrides, ITestOutput output, CancellationToken cancellationToken, bool shared = false)
    {
        var clock = Stopwatch.StartNew();
        var configuration = AppConfiguration.Read(attributes);
        var capture = new HostCapture(overrides, configuration.Settings, configuration.Kestrel, shared, output);
        var args = configuration.Arguments(TEntry.ApplicationName);

        // Under Native AOT, hosts that build at once can fail in GenericWebHostBuilder.UseStartup, so
        // one host builds at a time. The lock is released on HostBuilt, and the hosts start at once.
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            await HostBuildLock.Instance.WaitAsync(cancellationToken);
        }

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
            Name = $"{TEntry.ApplicationName} for {uid}",
        };

        var built = Task.WhenAny(capture.Host, entryPoint.Task);
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            _ = built.ContinueWith(static _ => HostBuildLock.Instance.Release(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        thread.Start();

        if (await built.WaitAsync(cancellationToken) == entryPoint.Task)
        {
            throw ReturnedEarly(await entryPoint.Task, "before it built a host");
        }

        var host = await capture.Host;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() => started.TrySetResult()))
        {
            if (await Task.WhenAny(started.Task, entryPoint.Task).WaitAsync(cancellationToken) == entryPoint.Task)
            {
                throw ReturnedEarly(await entryPoint.Task, "before its host started");
            }
        }

        output.WriteLine($"{TEntry.ApplicationName} started in {clock.Elapsed.TotalMilliseconds:F1} ms");
        return new WebAppContainer(host, entryPoint.Task);
    }

    private static InvalidOperationException ReturnedEarly(int exitCode, string when) =>
        new($"The entry point of {TEntry.ApplicationName} returned {exitCode} {when}. " +
            "What the app wrote to standard error went to the console of the test process.");
}

/// <summary>The lock that lets one host build at a time when dynamic code is not supported.</summary>
internal static class HostBuildLock
{
    public static readonly SemaphoreSlim Instance = new(1, 1);
}
