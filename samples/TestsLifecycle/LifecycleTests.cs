using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

[assembly: TestsLifecycle.LogScopes]

namespace TestsLifecycle;

public static class Steps
{
    public static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "lifecycle.log");

    public static void Write(string step) => File.AppendAllText(LogPath, step + "\n");
}

public interface IClock
{
    DateTime Now { get; }
}

public sealed class Basket(IClock clock)
{
    public IClock Clock { get; } = clock;
}

#region tests-scope-listener
/// <summary>Writes a step when each assembly, class and test scope is created and before it is disposed.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class LogScopesAttribute : Attribute, ITestScopeListener
{
    public ValueTask OnCreatedAsync(TestScope scope)
    {
        Steps.Write($"{Name(scope)} created");
        return default;
    }

    public ValueTask OnDisposingAsync(TestScope scope)
    {
        Steps.Write($"{Name(scope)} disposing");
        return default;
    }

    private static string Name(TestScope scope) => scope.Kind switch
    {
        TestScopeKind.Assembly => "assembly scope",
        TestScopeKind.Class => "class scope " + scope.Uid.Split('.')[^1],
        _ => "test scope " + scope.Uid[..scope.Uid.IndexOf('(')].Split('.')[^1],
    };
}
#endregion

#region tests-middleware
public sealed class LogMiddleware : ITestMiddleware
{
    public async ValueTask InvokeAsync(TestContext context, Func<ValueTask> next)
    {
        Steps.Write("middleware entered");
        try
        {
            await next();
        }
        finally
        {
            Steps.Write("middleware left");
        }
    }
}
#endregion

public sealed class LoggingBootstrap : ITestContainerBootstrap, ISupportsChildOverrides
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context)
    {
        Steps.Write($"{context.Scope.Kind.ToString().ToLowerInvariant()} container built");
        return new(new LoggingContainer("class container", null));
    }

    private sealed class LoggingContainer(string name, IReadOnlyList<TestOverride>? overrides) : ITestContainer, IServiceProvider
    {
        public IServiceProvider Services => this;

        public object? GetService(Type serviceType) =>
            overrides?.LastOrDefault(entry => entry.ServiceType == serviceType)?.Instance ??
            (serviceType == typeof(Basket) ? new Basket((IClock)GetService(typeof(IClock))!) : null);

        public bool TryResolve(TestParameter parameter, out object? value)
        {
            value = GetService(parameter.Type);
            Steps.Write($"parameter {parameter.Name} resolved from the {name}");
            return value is not null;
        }

        public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context)
        {
            Steps.Write("test container built with overrides for " + string.Join(", ", context.Overrides.Select(entry => entry.ServiceType.Name)));
            return new(new LoggingContainer("test container", context.Overrides));
        }

        public ValueTask DisposeAsync()
        {
            Steps.Write(name + " disposed");
            return default;
        }
    }
}

#region tests-lifecycle-sample
[Container<LoggingBootstrap>(Scope = ContainerScope.Class)]
[Use<LogMiddleware>]
public sealed partial class LifecycleTests : IAsyncDisposable
{
    public LifecycleTests() => Steps.Write("test class created");

    [VerifyTest]
    public void First([Mock] IClock clock, Basket basket)
    {
        Steps.Write("first test runs");
        Check(ReferenceEquals(basket.Clock, clock));
    }

    [VerifyTest]
    public void Second([Mock] IClock clock, Basket basket)
    {
        Steps.Write("second test runs");
        Check(ReferenceEquals(basket.Clock, clock));
    }

    public ValueTask DisposeAsync()
    {
        Steps.Write("test class disposed");
        return default;
    }
}
#endregion
