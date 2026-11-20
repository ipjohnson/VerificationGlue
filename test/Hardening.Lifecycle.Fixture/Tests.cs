using VerificationGlue;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

[assembly: Lifecycle.Probe]
[assembly: Use<Lifecycle.AssemblyMiddleware>]
[assembly: Lifecycle.AssemblyProvider]
[assembly: ScheduleGroup("default", MaxParallel = 1)]

namespace Lifecycle;

internal static class Body
{
    public static void Run(TestContext context)
    {
        Log.Write($"body {context.Uid}");
        if (Inject.IsFirst(context.Uid))
        {
            Inject.At("test", "body");
        }
    }
}

/// <summary>A test container for each test, and a test class that the runner creates and disposes.</summary>
[Container<ProbeBootstrap>]
[Use<ClassMiddleware>]
[ClassProvider]
public sealed partial class TestScoped : IDisposable
{
    private readonly string _uid;

    public TestScoped(TestContext context, Service service)
    {
        _uid = context.Uid;
        if (Inject.IsFirst(_uid))
        {
            Inject.At("test", "class-construction");
        }

        Log.Write($"acquire instance {_uid}");
    }

    [VerifyTest]
    [Use<MethodMiddleware>]
    [MethodProvider]
    public void First(Service service, TestContext context) => Body.Run(context);

    [VerifyTest]
    [Use<MethodMiddleware>]
    [MethodProvider]
    public void Second(Service service, TestContext context) => Body.Run(context);

    public void Dispose()
    {
        Log.Write($"release instance {_uid}");
        if (Inject.IsFirst(_uid))
        {
            Inject.At("test", "disposal", "instance");
        }
    }
}

/// <summary>A test class that <see cref="ProbeContainer"/> creates, so the container disposes it and the runner does not.</summary>
[Container<ProbeBootstrap>]
public sealed partial class ContainerOwned : IDisposable
{
    private readonly string _uid;

    public ContainerOwned(string uid)
    {
        _uid = uid;
        if (Inject.IsFirst(_uid))
        {
            Inject.At("test", "class-construction");
        }

        Log.Write($"acquire instance {_uid}");
    }

    [VerifyTest]
    public void First(Service service, TestContext context) => Body.Run(context);

    [VerifyTest]
    public void Second(Service service, TestContext context) => Body.Run(context);

    public void Dispose()
    {
        Log.Write($"release instance {_uid}");
        if (Inject.IsFirst(_uid))
        {
            Inject.At("test", "disposal", "instance");
        }
    }
}

[Container<ProbeBootstrap>(Scope = ContainerScope.Class)]
[Use<ClassMiddleware>]
[ClassProvider]
public sealed partial class ClassScoped
{
    [VerifyTest]
    [Use<MethodMiddleware>]
    [MethodProvider]
    public void First(Service service, TestContext context) => Body.Run(context);

    [VerifyTest]
    [Use<MethodMiddleware>]
    [MethodProvider]
    public void Second(Service service, TestContext context) => Body.Run(context);
}

[Container<ProbeBootstrap>(Scope = ContainerScope.Assembly)]
[Use<ClassMiddleware>]
[ClassProvider]
public sealed partial class AssemblyScoped
{
    [VerifyTest]
    [Use<MethodMiddleware>]
    [MethodProvider]
    public void First(Service service, TestContext context) => Body.Run(context);

    [VerifyTest]
    [Use<MethodMiddleware>]
    [MethodProvider]
    public void Second(Service service, TestContext context) => Body.Run(context);
}

/// <summary>A second class that shares the assembly container.</summary>
[Container<ProbeBootstrap>(Scope = ContainerScope.Assembly)]
public sealed partial class AssemblyScopedOther
{
    [VerifyTest]
    public void Third(Service service, TestContext context) => Body.Run(context);
}

/// <summary>
/// The first test leaves a failed check, a mock call with no Returns, queued matchers and a pending
/// Received on a mock the class container shares, then throws. The second test runs after it.
/// </summary>
[Container<ProbeBootstrap>(Scope = ContainerScope.Class)]
public sealed partial class StaleState
{
    [VerifyTest]
    public void A_leaves_pending_state(ICalculator shared, ITestOutput output)
    {
        output.WriteLine("output of A");
        Check(shared.Add(1, 1) == 5);
        _ = shared.Add(7, 7);
#pragma warning disable VGM0002 // The matchers are left queued on purpose.
        _ = Arg.Any<int>();
        _ = Arg.Any<int>();
#pragma warning restore VGM0002
        shared.Received();
        throw new InvalidOperationException("A left pending state.");
    }

    [VerifyTest]
    public void B_sees_no_pending_state(ICalculator shared, TestContext context)
    {
        Verify(VerifyScope.Current!.Failures.Count == 0);
        Verify(context.Uid.Contains(".B_sees_no_pending_state(", StringComparison.Ordinal));
#pragma warning disable VGM0005 // Returns with no mock call before it in this test must fail.
        Throws<MockSetupException>(() => 0.Returns(1));
#pragma warning restore VGM0005
        shared.Add(2, 3).Returns(9);
        Verify(shared.Add(2, 3) == 9);
        Verify(shared.Add(4, 4) == 0);
    }
}
