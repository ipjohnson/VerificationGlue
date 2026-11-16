// What the user writes.
using VerificationGlue;

namespace MyApp.Tests;

public interface IClock { DateTime Now { get; } }
public sealed class FixedClock : IClock, IDisposable
{
    public static int Disposed;
    public DateTime Now => new(2026, 1, 1);
    public void Dispose() => Interlocked.Increment(ref Disposed);
}

// A hand-rolled container. A DependencyModules adapter would build a ServiceProvider here.
public sealed class ClockBootstrap : ITestContainerBootstrap
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context) =>
        ValueTask.FromResult<ITestContainer>(new Container());

    private sealed class Container : ITestContainer, IServiceProvider
    {
        private readonly FixedClock _clock = new();
        public IServiceProvider Services => this;
        public object? GetService(Type serviceType) => serviceType == typeof(IClock) ? _clock : null;
        public bool TryResolve(TestParameter parameter, out object? value) =>
            (value = GetService(parameter.Type)) is not null;
        public ValueTask DisposeAsync() { _clock.Dispose(); return ValueTask.CompletedTask; }
    }
}

// A derived generic attribute, the way an adapter package would ship one.
public sealed class ClockAttribute : ContainerAttribute<ClockBootstrap>;

[Clock]
public class ClockTests
{
    [VerifyTest]
    public void NowIsFixed(IClock clock)
    {
        if (clock.Now.Year != 2026) throw new Exception("wrong year");
    }

    [VerifyTest]
    public async Task Fails(IClock clock)
    {
        await Task.Yield();
        throw new Exception("expected failure");
    }
}
