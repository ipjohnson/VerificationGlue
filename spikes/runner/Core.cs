// Stand-in for the VerificationGlue runtime library.
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;

namespace VerificationGlue;

[AttributeUsage(AttributeTargets.Method)]
public sealed class VerifyTestAttribute : Attribute;

public enum ContainerScope { Test, Class, Assembly }

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public class ContainerAttribute<T> : Attribute where T : ITestContainerBootstrap
{
    public ContainerScope Scope { get; set; }
}

public sealed record TestParameter(string Name, Type Type, int Position, IReadOnlyList<Attribute> Attributes);

public sealed record TestContainerContext(
    string TestUid,
    IReadOnlyList<Attribute> Attributes,
    IReadOnlyList<TestParameter> Parameters,
    ITestContainer? Parent,
    CancellationToken Cancellation);

public interface ITestContainerBootstrap
{
    static abstract ValueTask<ITestContainer> BuildAsync(TestContainerContext context);
}

public interface ITestContainer : IAsyncDisposable
{
    IServiceProvider Services { get; }
    bool TryResolve(TestParameter parameter, out object? value);
}

public sealed class TestContext(string uid, ITestContainer? container, CancellationToken cancellation)
{
    public string Uid { get; } = uid;
    public ITestContainer? Container { get; } = container;
    public CancellationToken Cancellation { get; } = cancellation;

    public T Resolve<T>(TestParameter parameter)
    {
        if (Container is not null && Container.TryResolve(parameter, out var value))
        {
            return (T)value!;
        }
        throw new InvalidOperationException($"No provider for parameter '{parameter.Name}' of type {parameter.Type}.");
    }
}

public sealed record TestDescriptor(
    string Uid,
    string DisplayName,
    string Namespace,
    string ClassName,
    string MethodName,
    string FilePath,
    int Line,
    Func<TestContainerContext, ValueTask<ITestContainer>>? BuildContainer,
    Func<IReadOnlyList<Attribute>> Attributes,
    IReadOnlyList<TestParameter> Parameters,
    Func<TestContext, ValueTask> Invoke);

public static class TestRegistry
{
    public static List<TestDescriptor> Tests { get; } = [];
}

public static class VerificationGlueExtensions
{
    public static void AddVerificationGlue(this ITestApplicationBuilder builder) =>
        builder.RegisterTestFramework(
            _ => new FrameworkCapabilities(),
            (_, _) => new Framework());

    private sealed class FrameworkCapabilities : ITestFrameworkCapabilities
    {
        public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => [];
    }

    private sealed class Framework : ITestFramework, IDataProducer
    {
        public string Uid => "VerificationGlue";
        public string Version => "0.0.1";
        public string DisplayName => "VerificationGlue";
        public string Description => "spike";
        public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) =>
            Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

        public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) =>
            Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

        public async Task ExecuteRequestAsync(ExecuteRequestContext context)
        {
            try
            {
                switch (context.Request)
                {
                    case DiscoverTestExecutionRequest discover:
                        foreach (var test in TestRegistry.Tests)
                        {
                            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                                discover.Session.SessionUid, Node(test, DiscoveredTestNodeStateProperty.CachedInstance)));
                        }
                        break;

                    case RunTestExecutionRequest run:
                        HashSet<string>? uids = run.Filter is TestNodeUidListFilter list
                            ? [.. list.TestNodeUids.Select(u => u.Value)]
                            : null;

                        await Parallel.ForEachAsync(
                            TestRegistry.Tests.Where(t => uids is null || uids.Contains(t.Uid)),
                            context.CancellationToken,
                            async (test, token) =>
                            {
                                TestNodeStateProperty state;
                                try
                                {
                                    ITestContainer? container = null;
                                    try
                                    {
                                        if (test.BuildContainer is not null)
                                        {
                                            container = await test.BuildContainer(new TestContainerContext(
                                                test.Uid, test.Attributes(), test.Parameters, null, token));
                                        }
                                        await test.Invoke(new TestContext(test.Uid, container, token));
                                    }
                                    finally
                                    {
                                        if (container is not null)
                                        {
                                            await container.DisposeAsync();
                                        }
                                    }
                                    state = PassedTestNodeStateProperty.CachedInstance;
                                }
                                catch (Exception e)
                                {
                                    state = new FailedTestNodeStateProperty(e);
                                }
                                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                                    run.Session.SessionUid, Node(test, state)));
                            });
                        break;
                }
            }
            finally
            {
                context.Complete();
            }
        }

        private static TestNode Node(TestDescriptor test, TestNodeStateProperty state) =>
            new()
            {
                Uid = test.Uid,
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(
                    state,
                    new TestFileLocationProperty(test.FilePath, new LinePositionSpan(new LinePosition(test.Line, 0), new LinePosition(test.Line, 0)))),
            };
    }
}
