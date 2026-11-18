using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

[assembly: Fixture.Tagged("assembly")]

namespace Fixture;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class TaggedAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ThrowingAttribute : Attribute
{
    public ThrowingAttribute() => throw new InvalidOperationException("The attribute constructor failed.");
}

public interface IClock
{
    DateTime Now { get; }
}

[Tagged("class")]
public partial class PassingTests
{
    [VerifyTest]
    public void Passes() => Verify(Total(2) == 3);

    [VerifyTest]
    public async Task Passes_async()
    {
        await Task.Yield();
        Verify(Total(4) == 5);
    }

    [VerifyTest]
    public static void Static_test() => Verify(Total(0) == 1);

    [VerifyTest]
    public void Gets_the_cancellation_token(CancellationToken token) => Verify(!token.IsCancellationRequested);

    [VerifyTest]
    [Tagged("method")]
    public void Sees_its_attributes(TestContext context) =>
        Verify(string.Join(",", context.Attributes.OfType<TaggedAttribute>().Select(tag => tag.Name)) == "assembly,class,method");

    [VerifyTest]
    public void Uses_a_mock()
    {
        var clock = Mock.Of<IClock>();
        clock.Now.Returns(new DateTime(2026, 11, 17));
        Verify(clock.Now.Year == 2026);
    }

    private static int Total(int value) => value + 1;
}

public partial class FailingTests
{
    [VerifyTest]
    public void Fails_in_the_rewritten_copy()
    {
        var quantity = 2;
        Verify(Total(quantity) == 5);
    }

    [VerifyTest]
    public void Throws() => throw new InvalidOperationException("The test threw.");

    [VerifyTest]
    public void Needs_a_service(IClock clock) => Verify(clock is not null);

    [VerifyTest]
    [Throwing]
    public void Has_an_attribute_that_throws(TestContext context) => Verify(context.Attributes.Count > 0);

    private static int Total(int value) => value + 1;
}

public class NotPartialTests
{
    [VerifyTest]
    public void Fails_in_the_original()
    {
        var quantity = 2;
        Verify(Total(quantity) == 5);
    }

    private static int Total(int value) => value + 1;
}

[ScheduleGroup("disposable", MaxParallel = 1)]
public sealed class DisposableTests : IDisposable
{
    private static int s_disposed;

    [VerifyTest]
    public void A_creates_an_instance() => Verify(s_disposed == 0);

    [VerifyTest]
    public void B_sees_the_first_instance_disposed() => Verify(s_disposed == 1);

    public void Dispose() => s_disposed++;
}
