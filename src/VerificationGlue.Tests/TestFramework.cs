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
/// filter, and runs the selected tests one at a time.
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

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        var tests = TestRegistry.Tests.OrderBy(test => test.Uid, StringComparer.Ordinal).ToArray();
        var duplicates = tests.GroupBy(test => test.Uid, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicates.Length > 0)
        {
            return Task.FromResult(new CreateTestSessionResult
            {
                IsSuccess = false,
                ErrorMessage = "More than one test has the ID " + string.Join(", ", duplicates) + ". Each test ID must be unique.",
            });
        }

        _tests = tests;
        return Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
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
                        await Publish(context, discover.Session.SessionUid, test, DiscoveredTestNodeStateProperty.CachedInstance, null);
                    }

                    break;

                case RunTestExecutionRequest run:
                    var runFilter = Filter(run.Filter);
                    foreach (var test in _tests.Where(runFilter))
                    {
                        await Publish(context, run.Session.SessionUid, test, InProgressTestNodeStateProperty.CachedInstance, null);
                        var start = DateTimeOffset.UtcNow;
                        var watch = Stopwatch.StartNew();
                        var (state, failure) = await RunAsync(test, context.CancellationToken);
                        var timing = new TimingProperty(new TimingInfo(start, start + watch.Elapsed, watch.Elapsed));
                        await Publish(context, run.Session.SessionUid, test, state, failure, timing);
                    }

                    break;
            }
        }
        finally
        {
            context.Complete();
        }
    }

    private static async Task<(TestNodeStateProperty State, Exception? Failure)> RunAsync(TestDescriptor test, CancellationToken cancellationToken)
    {
        try
        {
            await test.Invoke(new TestContext(test, cancellationToken));
            return (PassedTestNodeStateProperty.CachedInstance, null);
        }
        catch (VerifyException failure)
        {
            return (new FailedTestNodeStateProperty(failure, failure.Message), failure);
        }
        catch (Exception error)
        {
            return (new ErrorTestNodeStateProperty(error, error.Message), error);
        }
    }

    private Task Publish(ExecuteRequestContext context, SessionUid session, TestDescriptor test, TestNodeStateProperty state, Exception? failure, TimingProperty? timing = null)
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

        if (_trx.IsEnabled)
        {
            properties.Add(new TrxFullyQualifiedTypeNameProperty(test.FullTypeName));
            if (failure is not null)
            {
                properties.Add(new TrxExceptionProperty(failure.Message, failure.StackTrace));
            }
        }

        var node = new TestNode { Uid = test.Uid, DisplayName = test.DisplayName, Properties = properties };
        return context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(session, node));
    }

    /// <summary>
    /// The tests a request selects. <c>--filter-uid</c> and the IDEs send a <see cref="TestNodeUidListFilter"/>.
    /// The other MTP 3 filters are experimental (TPEXP), and docs/design/tests.md rules them out.
    /// </summary>
    private static Func<TestDescriptor, bool> Filter(ITestExecutionFilter? filter)
    {
        switch (filter)
        {
            case null or NopFilter:
                return static _ => true;
            case TestNodeUidListFilter list:
                var uids = list.TestNodeUids.Select(uid => uid.Value).ToHashSet(StringComparer.Ordinal);
                return test => uids.Contains(test.Uid);
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
