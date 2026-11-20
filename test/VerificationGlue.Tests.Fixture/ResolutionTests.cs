using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

[Container<FixtureBootstrap>]
[LogScopes]
public partial class ThrowingSourceTests
{
    [VerifyTest]
    public void Provider_throws_while_resolving(Database database, [ThrowingProvider] string name) =>
        Log.Write("Provider_throws_while_resolving ran");

    [VerifyTest]
    public void Container_throws_while_resolving(Database database, BrokenService service) =>
        Log.Write("Container_throws_while_resolving ran");
}

[Container<FixtureBootstrap>]
[ThrowingProvider]
public sealed partial class ThrowingConstructorSourceTests(Database database, string name) : IDisposable
{
    [VerifyTest]
    public void Class_provider_throws_for_a_constructor_parameter() =>
        Log.Write($"Class_provider_throws_for_a_constructor_parameter ran with {database.Id} and {name}");

    public void Dispose() => Log.Write("ThrowingConstructorSourceTests disposed");
}

/// <summary>The runner calls the accessible constructor with the most parameters, not the private one.</summary>
[Container<FixtureBootstrap>]
public sealed partial class ConstructorChoiceTests
{
    private readonly string _chosen;

    public ConstructorChoiceTests() => _chosen = "none";

    internal ConstructorChoiceTests(OrderService service) => _chosen = "one";

    public ConstructorChoiceTests(OrderService service, Database database) => _chosen = $"two: {service.Clock is SystemClock}, {database.Id > 0}";

    private ConstructorChoiceTests(OrderService service, Database database, IClock clock) => _chosen = "three";

    [VerifyTest]
    public void Uses_the_accessible_constructor_with_the_most_parameters() => Verify(_chosen == "two: True, True");
}

public partial class CancellationTests
{
    /// <summary>When <c>VG_FIXTURE_HANG</c> is set, waits on its token until the run is cancelled.</summary>
    [VerifyTest]
    public async Task Waits_for_the_run_to_be_cancelled(CancellationToken token, TestContext context)
    {
        Verify(token == context.CancellationToken);
        if (Environment.GetEnvironmentVariable("VG_FIXTURE_HANG") is null)
        {
            return;
        }

        Log.Write("the test waits on its token");
        try
        {
            await Task.Delay(Timeout.Infinite, token);
        }
        finally
        {
            Log.Write($"the test's token is cancelled: {token.IsCancellationRequested}");
        }
    }
}

[Container<FixtureBootstrap>]
[OwnedClock]
public partial class OverrideOwnershipTests
{
    [VerifyTest]
    public void Override_is_left_to_its_owner(OrderService service) => Verify(service.Clock is OwnedClock);
}
