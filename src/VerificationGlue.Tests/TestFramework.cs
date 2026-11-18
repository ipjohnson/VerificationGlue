using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;

namespace VerificationGlue.Tests;

/// <summary>
/// The MTP test framework. It discovers the tests in <see cref="TestRegistry"/>, honours the UID
/// filter, and runs the selected tests with an <see cref="ITestScheduler"/>.
/// </summary>
internal sealed class TestFramework : ITestFramework, IDataProducer
{
    private readonly TrxCapability _trx;
    private IReadOnlyList<TestDescriptor> _tests = [];

    public TestFramework(TrxCapability trx) => _trx = trx;

    public string Uid => "VerificationGlue.Tests";

    public string Version { get; } =
        typeof(TestFramework).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public string DisplayName => "VerificationGlue.Tests";

    public string Description => "Runs [VerifyTest] methods registered by the VerificationGlue.Tests generator.";

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <summary>
    /// Reads the registry and expands each test that has data sources into its rows. This runs in every
    /// process, so a fresh process that a retry starts computes the same row IDs from the same data.
    /// </summary>
    public async Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        var expanded = await RowExpansion.ExpandAsync(TestRegistry.Tests, context.CancellationToken);
        var tests = expanded.OrderBy(test => test.Uid, StringComparer.Ordinal).ToArray();
        var duplicates = tests.GroupBy(test => test.Uid, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicates.Length > 0)
        {
            return new CreateTestSessionResult
            {
                IsSuccess = false,
                ErrorMessage = "More than one test has the ID " + string.Join(", ", duplicates) + ". Each test ID must be unique.",
            };
        }

        _tests = tests;
        return new CreateTestSessionResult { IsSuccess = true };
    }

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) =>
        Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        try
        {
            switch (context.Request)
            {
                case DiscoverTestExecutionRequest discover:
                    var discoverFilter = Filter(discover.Filter);
                    foreach (var test in _tests.Where(discoverFilter))
                    {
                        if (test.SourceFailure is { } failure)
                        {
                            await Publish(context, discover.Session.SessionUid, test, new ErrorTestNodeStateProperty(failure, failure.Message), failure);
                        }
                        else
                        {
                            await Publish(context, discover.Session.SessionUid, test, DiscoveredTestNodeStateProperty.CachedInstance, null);
                        }
                    }

                    break;

                case RunTestExecutionRequest run:
                    await RunAsync(context, run.Session.SessionUid, _tests.Where(Filter(run.Filter)).ToArray());
                    break;
            }
        }
        finally
        {
            context.Complete();
        }
    }

    /// <summary>
    /// Runs <paramref name="selected"/> with the scheduler that <c>[Scheduler&lt;T&gt;]</c> names, or
    /// <see cref="DefaultTestScheduler"/>. A test the scheduler does not run is skipped, and when the
    /// scheduler throws, each test it did not run fails with its exception.
    /// </summary>
    private async Task RunAsync(ExecuteRequestContext context, SessionUid session, TestDescriptor[] selected)
    {
        var cancellationToken = context.CancellationToken;
        var runner = new TestRunner(selected, cancellationToken);
        var scheduled = ScheduledTest.From(selected);
        var started = new HashSet<TestDescriptor>(ReferenceEqualityComparer.Instance);
        Exception? schedulerFailure = null;
        try
        {
            try
            {
                await CreateScheduler().RunAsync(scheduled, RunOneAsync, cancellationToken);
            }
            catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
            {
                schedulerFailure = failure;
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                foreach (var test in selected.Where(test => !started.Contains(test)))
                {
                    if (schedulerFailure is null)
                    {
                        await Publish(context, session, test, new SkippedTestNodeStateProperty("The test scheduler did not run this test."), null);
                    }
                    else
                    {
                        var failure = new InvalidOperationException("The test scheduler failed before it ran this test: " + schedulerFailure.Message, schedulerFailure);
                        await Publish(context, session, test, new ErrorTestNodeStateProperty(failure, failure.Message), failure);
                    }
                }
            }
        }
        finally
        {
            foreach (var scopeFailure in await runner.DisposeAsync())
            {
                await PublishScope(context, session, scopeFailure);
            }
        }

        async ValueTask RunOneAsync(ScheduledTest scheduledTest)
        {
            var test = scheduledTest.Test;
            lock (started)
            {
                if (!started.Add(test))
                {
                    throw new InvalidOperationException($"The test scheduler ran {test.Uid} more than once.");
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Publish(context, session, test, InProgressTestNodeStateProperty.CachedInstance, null);
            var start = DateTimeOffset.UtcNow;
            var watch = Stopwatch.StartNew();
            var outcome = await runner.RunAsync(test);
            var timing = new TimingProperty(new TimingInfo(start, start + watch.Elapsed, watch.Elapsed));
            var (state, failure) = State(outcome.Failures);
            await Publish(context, session, test, state, failure, timing, outcome.Output);
            if (await runner.CompleteAsync(test) is { } classFailure)
            {
                await PublishScope(context, session, classFailure);
            }
        }
    }

    /// <summary>The scheduler of the last <c>[Scheduler&lt;T&gt;]</c> on the assembly, or the default one.</summary>
    private static ITestScheduler CreateScheduler()
    {
        SchedulerAttribute? chosen = null;
        try
        {
            foreach (var attribute in TestRegistry.CreateAssemblyAttributes())
            {
                if (attribute is SchedulerAttribute scheduler)
                {
                    chosen = scheduler;
                }
            }
        }
        catch (Exception)
        {
            // The assembly scope reports this failure on every test.
        }

        return chosen?.Create() ?? new DefaultTestScheduler();
    }

    /// <summary>
    /// Passed with no failures. Otherwise failed when the first failure is a <see cref="VerifyException"/>
    /// and an error for any other exception. More than one failure is reported as one, with every message.
    /// </summary>
    private static (TestNodeStateProperty State, Exception? Failure) State(IReadOnlyList<Exception> failures)
    {
        if (failures.Count == 0)
        {
            return (PassedTestNodeStateProperty.CachedInstance, null);
        }

        var failure = failures.Count == 1 ? failures[0] : new TestFailuresException(failures);
        return failures[0] is VerifyException
            ? (new FailedTestNodeStateProperty(failure, failure.Message), failure)
            : (new ErrorTestNodeStateProperty(failure, failure.Message), failure);
    }

    /// <summary>Publishes a failed node for a class or assembly scope whose disposal failed.</summary>
    private Task PublishScope(ExecuteRequestContext context, SessionUid session, ScopeFailure scopeFailure)
    {
        var (state, failure) = State(scopeFailure.Failures);
        var scope = scopeFailure.Scope;
        var properties = new PropertyBag(state!);
        if (_trx.IsEnabled)
        {
            properties.Add(new TrxFullyQualifiedTypeNameProperty(scope.Uid));
            properties.Add(new TrxExceptionProperty(failure!.Message, failure.StackTrace));
        }

        var kind = scope.Kind == TestScopeKind.Class ? "class" : "assembly";
        var node = new TestNode { Uid = scope.Uid + "#" + kind + "-scope", DisplayName = $"Disposing the {kind} scope {scope.Uid}", Properties = properties };
        return context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(session, node));
    }

    private Task Publish(ExecuteRequestContext context, SessionUid session, TestDescriptor test, TestNodeStateProperty state, Exception? failure, TimingProperty? timing = null, string? output = null)
    {
        var position = new LinePosition(test.Line, -1);
        var properties = new PropertyBag(
            state,
            new TestFileLocationProperty(test.FilePath, new LinePositionSpan(position, position)),
            new TestMethodIdentifierProperty(
                test.TestClass.Assembly.FullName ?? "",
                test.Namespace,
                test.TypeName,
                test.MethodName,
                0,
                [.. test.ParameterTypeNames],
                test.ReturnTypeName));

        if (timing is not null)
        {
            properties.Add(timing);
        }

        if (!string.IsNullOrEmpty(output))
        {
            properties.Add(new StandardOutputProperty(output));
        }

        if (_trx.IsEnabled)
        {
            properties.Add(new TrxFullyQualifiedTypeNameProperty(test.FullTypeName));
            if (failure is not null)
            {
                properties.Add(new TrxExceptionProperty(failure.Message, failure.StackTrace));
            }

            if (!string.IsNullOrEmpty(output))
            {
                properties.Add(new TrxMessagesProperty([new StandardOutputTrxMessage(output)]));
            }
        }

        var node = new TestNode { Uid = test.Uid, DisplayName = test.DisplayName, Properties = properties };
        return context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(session, node));
    }

    /// <summary>
    /// The tests a request selects. <c>--filter-uid</c> and the IDEs send a <see cref="TestNodeUidListFilter"/>.
    /// The other MTP 3 filters are experimental (TPEXP), and docs/design/tests.md rules them out. The ID
    /// of a row also selects the failed node of its method's data sources, so a retry of that row in a
    /// process where the source fails reports the failure and runs no other row.
    /// </summary>
    private static Func<TestDescriptor, bool> Filter(ITestExecutionFilter? filter)
    {
        switch (filter)
        {
            case null or NopFilter:
                return static _ => true;
            case TestNodeUidListFilter list:
                var uids = list.TestNodeUids.Select(uid => uid.Value).ToHashSet(StringComparer.Ordinal);
                return test => uids.Contains(test.Uid) || (test.SourceFailure is not null && uids.Any(uid => uid.StartsWith(test.Uid + "#", StringComparison.Ordinal)));
            default:
                throw new NotSupportedException(
                    $"VerificationGlue.Tests does not support the {filter.GetType().Name} filter. Select tests with --filter-uid.");
        }
    }
}

/// <summary>Tells the TRX extension that test nodes carry TRX properties once it enables them.</summary>
internal sealed class TrxCapability : ITrxReportCapability
{
    public bool IsEnabled { get; private set; }

    public bool IsSupported => true;

    public void Enable() => IsEnabled = true;
}

internal sealed class TestFrameworkCapabilities(TrxCapability trx) : ITestFrameworkCapabilities
{
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities { get; } = [trx];
}
