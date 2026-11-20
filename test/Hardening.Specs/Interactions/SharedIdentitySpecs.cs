using System.Collections.Concurrent;
using VerificationGlue;
using VerificationGlue.Mocks;
using static VerificationGlue.VerifySyntax;

namespace Hardening.Specs.Interactions;

public interface IClock
{
    DateTime Now { get; }
}

/// <summary>The service a container shares across the tests of its scope.</summary>
public sealed class OrderStore;

/// <summary>The system under test, which the container creates for each request.</summary>
public sealed class Stamp(IClock clock, OrderStore store)
{
    public IClock Clock { get; } = clock;

    public OrderStore Store { get; } = store;
}

/// <summary>
/// Builds a container with one <see cref="OrderStore"/>. A child keeps its parent's store and
/// returns its own test's overrides, so the bootstrap works at test, class and assembly scope.
/// </summary>
public sealed class IdentityBootstrap : ITestContainerBootstrap, ISupportsChildOverrides
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context) =>
        new(new IdentityContainer(new OrderStore(), context.Overrides));

    private sealed class IdentityContainer(OrderStore store, IReadOnlyList<TestOverride> overrides) : ITestContainer, IServiceProvider
    {
        public IServiceProvider Services => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(OrderStore) ? store :
            serviceType == typeof(Stamp) ? new Stamp((IClock)GetService(typeof(IClock))!, store) :
            overrides.LastOrDefault(entry => entry.ServiceType == serviceType && entry.Key is null)?.Instance;

        public bool TryResolve(TestParameter parameter, out object? value)
        {
            value = GetService(parameter.Type);
            return value is not null;
        }

        public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context) => new(new IdentityContainer(store, context.Overrides));

        public ValueTask DisposeAsync() => default;
    }
}

/// <summary>
/// MIX-002 of docs/design/hardening.md, from docs/guide/test-parameters.md and
/// docs/guide/containers-and-scopes.md. Within a test, the constructor's <c>[Mock]</c>, the method's
/// <c>[Mock]</c>, a plain parameter the container resolves and the clock of each container-created
/// <see cref="Stamp"/> are one mock. Each test gets its own mock at every scope. The store is the
/// test's own at test scope and shared by the class or the assembly at those scopes.
/// </summary>
public static partial class SharedIdentity
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, (IClock Clock, OrderStore Store)>> s_seen = new();

    [Verify]
    public static void Check(
        string scope,
        bool sharedStore,
        TestContext context,
        (IClock Clock, Stamp Sut) constructor,
        (IClock Clock, IClock Resolved, Stamp Sut) method)
    {
        var clock = constructor.Clock;
        Verify(ReferenceEquals(method.Clock, clock));
        Verify(ReferenceEquals(method.Resolved, clock));
        Verify(ReferenceEquals(constructor.Sut.Clock, clock));
        Verify(ReferenceEquals(method.Sut.Clock, clock));
        Verify(!ReferenceEquals(constructor.Sut, method.Sut));
        Verify(ReferenceEquals(constructor.Sut.Store, method.Sut.Store));

        var day = new DateTime(2026, 11, 20);
        method.Clock.Now.Returns(day);
        Verify(constructor.Sut.Clock.Now == day);

        var seen = s_seen.GetOrAdd(scope, _ => new());
        seen[context.Uid] = (clock, constructor.Sut.Store);
        var others = seen.Where(entry => entry.Key != context.Uid).Select(entry => entry.Value).ToList();
        Verify(others.All(other => !ReferenceEquals(other.Clock, clock)));
        Verify(sharedStore
            ? others.All(other => ReferenceEquals(other.Store, constructor.Sut.Store))
            : others.All(other => !ReferenceEquals(other.Store, constructor.Sut.Store)));
    }
}

[Trait("Step", "5")]
[Container<IdentityBootstrap>]
public sealed partial class TestScopedIdentitySpecs([Mock] IClock clock, Stamp sut)
{
    [VerifyTest]
    public void The_first_test_shares_one_mock([Mock] IClock sameClock, IClock resolved, Stamp methodSut, TestContext context) =>
        SharedIdentity.Check("test", sharedStore: false, context, (clock, sut), (sameClock, resolved, methodSut));

    [VerifyTest]
    public void The_second_test_shares_another_mock([Mock] IClock sameClock, IClock resolved, Stamp methodSut, TestContext context) =>
        SharedIdentity.Check("test", sharedStore: false, context, (clock, sut), (sameClock, resolved, methodSut));
}

[Trait("Step", "5")]
[Container<IdentityBootstrap>(Scope = ContainerScope.Class)]
public sealed partial class ClassScopedIdentitySpecs([Mock] IClock clock, Stamp sut)
{
    [VerifyTest]
    public void The_first_test_shares_one_mock([Mock] IClock sameClock, IClock resolved, Stamp methodSut, TestContext context) =>
        SharedIdentity.Check("class", sharedStore: true, context, (clock, sut), (sameClock, resolved, methodSut));

    [VerifyTest]
    public void The_second_test_shares_another_mock([Mock] IClock sameClock, IClock resolved, Stamp methodSut, TestContext context) =>
        SharedIdentity.Check("class", sharedStore: true, context, (clock, sut), (sameClock, resolved, methodSut));
}

[Trait("Step", "5")]
[Container<IdentityBootstrap>(Scope = ContainerScope.Assembly)]
public sealed partial class AssemblyScopedIdentitySpecs([Mock] IClock clock, Stamp sut)
{
    [VerifyTest]
    public void The_first_test_shares_one_mock([Mock] IClock sameClock, IClock resolved, Stamp methodSut, TestContext context) =>
        SharedIdentity.Check("assembly", sharedStore: true, context, (clock, sut), (sameClock, resolved, methodSut));

    [VerifyTest]
    public void The_second_test_shares_another_mock([Mock] IClock sameClock, IClock resolved, Stamp methodSut, TestContext context) =>
        SharedIdentity.Check("assembly", sharedStore: true, context, (clock, sut), (sameClock, resolved, methodSut));
}

/// <summary>A second class on the assembly container, which shares its store with <see cref="AssemblyScopedIdentitySpecs"/>.</summary>
[Trait("Step", "5")]
[Container<IdentityBootstrap>(Scope = ContainerScope.Assembly)]
public sealed partial class SecondAssemblyScopedIdentitySpecs([Mock] IClock clock, Stamp sut)
{
    [VerifyTest]
    public void A_test_of_another_class_shares_its_own_mock([Mock] IClock sameClock, IClock resolved, Stamp methodSut, TestContext context) =>
        SharedIdentity.Check("assembly", sharedStore: true, context, (clock, sut), (sameClock, resolved, methodSut));
}
