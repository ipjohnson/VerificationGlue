using VerificationGlue;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

[Container<FixtureBootstrap>]
public partial class TestContainerTests
{
    [VerifyTest]
    public void Mock_reaches_the_test_scoped_service([Mock] IClock clock, OrderService service, Database database)
    {
        Log.Write($"Mock_reaches_the_test_scoped_service used database {database.Id}");
        clock.Now.Returns(new DateTime(2026, 11, 17));
        Verify(ReferenceEquals(service.Clock, clock));
        Verify(service.Clock.Now.Year == 2026);
    }

    [VerifyTest]
    public void Service_without_a_mock_gets_the_registered_clock(OrderService service, Database database)
    {
        Log.Write($"Service_without_a_mock_gets_the_registered_clock used database {database.Id}");
        Verify(service.Clock is SystemClock);
    }
}

[Container<FixtureBootstrap>(Scope = ContainerScope.Class)]
[LogScopes]
public partial class ClassContainerTests
{
    [VerifyTest]
    public void Mock_reaches_the_class_scoped_service([Mock] IClock clock, OrderService service, Database database)
    {
        Log.Write($"Mock_reaches_the_class_scoped_service used database {database.Id}");
        Verify(ReferenceEquals(service.Clock, clock));
    }

    [VerifyTest]
    public void Next_test_shares_the_class_database(OrderService service, Database database)
    {
        Log.Write($"Next_test_shares_the_class_database used database {database.Id}");
        Verify(service.Clock is SystemClock);
    }
}

[Container<FixtureBootstrap>(Scope = ContainerScope.Assembly)]
public partial class AssemblyContainerTests
{
    [VerifyTest]
    public void Mock_reaches_the_assembly_scoped_service([Mock] IClock clock, OrderService service, Database database)
    {
        Log.Write($"Mock_reaches_the_assembly_scoped_service used database {database.Id}");
        Verify(ReferenceEquals(service.Clock, clock));
    }
}

[Container<FixtureBootstrap>(Scope = ContainerScope.Assembly)]
public partial class OtherAssemblyContainerTests
{
    [VerifyTest]
    public void Other_class_shares_the_assembly_database(OrderService service, Database database)
    {
        Log.Write($"Other_class_shares_the_assembly_database used database {database.Id}");
        Verify(service.Clock is SystemClock);
    }
}

[Container<FixtureBootstrap>]
[FixedClock]
public partial class ListenerOverrideTests
{
    [VerifyTest]
    public void Listener_override_reaches_the_service(OrderService service) => Verify(service.Clock is FixedClock);
}

[Container<PlainBootstrap>(Scope = ContainerScope.Class)]
[FixedClock]
public partial class SharedContainerWithoutChildOverridesTests
{
    [VerifyTest]
    public void Fails_before_the_test_runs(OrderService service) => Log.Write("Fails_before_the_test_runs ran");
}

[Provide("class")]
public partial class ParameterTests
{
    [VerifyTest]
    [Provide("method")]
    public void Narrowest_provider_wins(string fromMethod, [Provide("parameter")] string fromParameter, int count = 7) =>
        Verify(fromMethod == "method" && fromParameter == "parameter" && count == 7);

    [VerifyTest]
    public void Class_provider_supplies_the_parameter(string fromClass) => Verify(fromClass == "class");
}

/// <summary>The runner creates this class, with constructor arguments from the container and a mock.</summary>
[Container<FixtureBootstrap>]
public sealed partial class RunnerOwnedTests([Mock] IClock clock, OrderService service) : IDisposable
{
    [VerifyTest]
    public void Constructor_gets_the_mock_and_the_service([Mock] IClock sameClock) =>
        Verify(ReferenceEquals(service.Clock, clock) && ReferenceEquals(sameClock, clock));

    public void Dispose() => Log.Write("runner-owned instance disposed");
}

/// <summary>The container creates this class, so the container disposes it and the runner does not.</summary>
[Container<FixtureBootstrap>]
public sealed partial class ContainerOwnedTests(Database database) : IDisposable
{
    [VerifyTest]
    public void Is_created_by_the_container() => Verify(database.Id > 0);

    public void Dispose() => Log.Write("container-owned instance disposed");
}

[Container<FixtureBootstrap>]
[Use<LogMiddleware>]
public partial class OutputTests
{
    [VerifyTest]
    public void Writes_its_output_in_order(ITestOutput output) => output.WriteLine("the test ran");
}

public partial class CheckTests
{
    [VerifyTest]
    public void Reports_every_failed_check()
    {
        var total = 2;
        Check(total == 3);
        Check(total > 5);
    }

    [VerifyTest]
    public void Starts_with_no_failed_checks() => Verify(VerifyScope.Current!.Failures.Count == 0);
}

[Container<ThrowingDisposeBootstrap>]
[Use<ThrowOnLeaveMiddleware>]
public sealed partial class MultipleFailuresTests : IDisposable
{
    [VerifyTest]
    public void Keeps_every_failure()
    {
        var total = 2;
        Check(total == 3);
        throw new InvalidOperationException("The test threw after a failed check.");
    }

    public void Dispose() => throw new InvalidOperationException("The test instance failed to dispose.");
}

[ThrowingClass]
public partial class BrokenClassTests
{
    [VerifyTest]
    public void Broken_class_first()
    {
    }

    [VerifyTest]
    public void Broken_class_second()
    {
    }
}

[Container<HangingChildBootstrap>(Scope = ContainerScope.Class)]
[LogScopes]
public partial class CancelledSetupTests
{
    [VerifyTest]
    public void Is_cancelled_while_its_container_is_created() => Log.Write("Is_cancelled_while_its_container_is_created ran");
}

[Container<FixtureBootstrap>]
public partial class RetryTests
{
    private static int s_runs;

    /// <summary>When <c>VG_FIXTURE_RETRY</c> is set, fails on the first attempt and passes on the next.</summary>
    [VerifyTest]
    public void Passes_on_the_second_attempt(Database database)
    {
        s_runs++;
        var attempt = Log.Lines.Count(line => line.StartsWith("retry attempt", StringComparison.Ordinal)) + 1;
        Log.Write($"retry attempt {attempt}: runs in this process {s_runs}, database {database.Id}");
        Verify(s_runs == 1);
        Verify(Environment.GetEnvironmentVariable("VG_FIXTURE_RETRY") is null || attempt > 1);
    }
}
