// Stand-in for the VerificationGlue, VerificationGlue.Tests and VerificationGlue.Mocks runtimes.
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;

namespace VerificationGlue
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class VerifyTestAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class VerifyAttribute : Attribute;

    public static class VerifySyntax
    {
        public static void Verify(
            [System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition,
            [CallerArgumentExpression(nameof(condition))] string? expression = null,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (!condition)
            {
                throw VerifyFailure.Create(expression ?? "", file, line).Build();
            }
        }
    }

    public sealed class VerifyException(string message) : Exception(message);

    public sealed class VerifyFailure
    {
        private readonly StringBuilder _text = new();

        private VerifyFailure(string expression, string file, int line) =>
            _text.Append("Verify failed: ").Append(expression).Append(" at ").Append(Path.GetFileName(file)).Append(':').Append(line);

        public static VerifyFailure Create(string expression, string file, int line) => new(expression, file, line);

        public VerifyFailure With<T>(string expression, T value)
        {
            _text.Append("\n  ").Append(expression).Append(" -> ").Append(value?.ToString() ?? "null");
            return this;
        }

        public VerifyException Build() => new(_text.ToString());
    }

    public sealed record TestDescriptor(
        string Uid,
        string DisplayName,
        string Namespace,
        string ClassName,
        string MethodName,
        string FilePath,
        int Line,
        Func<ValueTask> Invoke);

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
            public string Description => "IDE spike";
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

                            foreach (var test in TestRegistry.Tests.Where(t => uids is null || uids.Contains(t.Uid)))
                            {
                                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                                    run.Session.SessionUid, Node(test, InProgressTestNodeStateProperty.CachedInstance)));
                                TestNodeStateProperty state;
                                try
                                {
                                    await test.Invoke();
                                    state = PassedTestNodeStateProperty.CachedInstance;
                                }
                                catch (Exception e)
                                {
                                    state = new FailedTestNodeStateProperty(e);
                                }
                                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                                    run.Session.SessionUid, Node(test, state)));
                            }
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
                        new TestFileLocationProperty(
                            test.FilePath,
                            new LinePositionSpan(new LinePosition(test.Line, -1), new LinePosition(test.Line, -1))),
                        new TestMethodIdentifierProperty(
                            typeof(Framework).Assembly.FullName!,
                            test.Namespace,
                            test.ClassName,
                            test.MethodName,
                            0,
                            [],
                            "System.Void")),
                };
        }
    }
}

namespace VerificationGlue.Mocks
{
    public static class Mock
    {
        public static T Of<T>() where T : class => MockFactory<T>.Create?.Invoke()
            ?? throw new InvalidOperationException($"No mock registered for {typeof(T)}.");
    }

    public static class MockFactory<T> where T : class
    {
        public static Func<T>? Create { get; set; }
    }

    public readonly struct CallCount(int count, string calls)
    {
        public int Count { get; } = count;
        public static bool operator ==(CallCount left, int right) => left.Count == right;
        public static bool operator !=(CallCount left, int right) => left.Count != right;
        public override bool Equals(object? obj) => obj is CallCount other && other.Count == Count;
        public override int GetHashCode() => Count;
        public override string ToString() => $"{Count} call(s): {calls}";
    }
}
