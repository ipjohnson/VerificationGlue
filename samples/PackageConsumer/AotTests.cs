using VerificationGlue;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace PackageConsumer;

public interface IClock
{
    DateTime Now { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AreaAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

// Each test checks a part of the runner that Native AOT could break: the generated registration,
// awaited tests, built-in parameters, attributes created without reflection, mocks and messages.
#region tests-under-native-aot
[Area("orders")]
public partial class AotTests
{
    [VerifyTest]
    public void Runs_a_test()
    {
        var lines = new[] { 3, 4 };
        Verify(lines.Sum() == 7);
    }

    [VerifyTest]
    public async Task Awaits_a_test(CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        Verify(!cancellationToken.IsCancellationRequested);
    }

    [VerifyTest]
    [Area("checkout")]
    public void Creates_attributes(TestContext context) =>
        Verify(string.Join(",", context.Attributes.OfType<AreaAttribute>().Select(area => area.Name)) == "orders,checkout");

    [VerifyTest]
    public void Uses_a_mock()
    {
        var clock = Mock.Of<IClock>();
        clock.Now.Returns(new DateTime(2026, 11, 17));
        Verify(clock.Now.Day == 17);
    }

    [VerifyTest]
    public void Reports_a_message()
    {
        var total = 40;
        var failure = Throws<VerifyException>(() => Verify(total == 30));
        Verify(failure.Message.StartsWith("Verify failed: total == 30\n  expected total to equal 30\n", StringComparison.Ordinal));
    }
}
#endregion

// The project sets VerificationGlueRewrite, so the runner calls each test's __Verified copy, and a
// direct call to a [Verify] method runs its copy. The message lists the values the copy captured.
public partial class AotTier3Tests
{
    [VerifyTest]
    public void Reports_captured_values()
    {
        var failure = Throws<VerifyException>(() => VerifyDiscount(total: 40, code: null));

        Verify(failure.Message.StartsWith(
            """
            Verify failed: total == 30 || code != null && code.Length == 6
              total == 30                      → false
              total                            → 40
              code != null && code.Length == 6 → false
              code != null                     → false
              code                             → null
              code.Length == 6                 → <not evaluated>
              code.Length                      → <not evaluated>
              at AotTests.cs:
            """.ReplaceLineEndings("\n"),
            StringComparison.Ordinal));
    }

    [Verify]
    public static void VerifyDiscount(int total, string? code)
    {
        Verify(total == 30 || code != null && code.Length == 6);
    }
}

public sealed class Stamp(IClock clock)
{
    public IClock Clock { get; } = clock;
}

// Builds a class container whose children return the test's overrides, with no reflection.
public sealed class StampBootstrap : ITestContainerBootstrap, ISupportsChildOverrides
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context) => new(new StampContainer([]));

    private sealed class StampContainer(IReadOnlyList<TestOverride> overrides) : ITestContainer, IServiceProvider
    {
        public IServiceProvider Services => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(Stamp) ? new Stamp((IClock)GetService(typeof(IClock))!) :
            overrides.LastOrDefault(entry => entry.ServiceType == serviceType)?.Instance;

        public bool TryResolve(TestParameter parameter, out object? value)
        {
            value = GetService(parameter.Type);
            return value is not null;
        }

        public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context) => new(new StampContainer(context.Overrides));

        public ValueTask DisposeAsync() => default;
    }
}

[Container<StampBootstrap>(Scope = ContainerScope.Class)]
public partial class AotContainerTests
{
    [VerifyTest]
    public void Passes_the_mock_to_the_service([Mock] IClock clock, Stamp stamp)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));
        Verify(ReferenceEquals(stamp.Clock, clock));
        Verify(stamp.Clock.Now.Day == 17);
    }
}
