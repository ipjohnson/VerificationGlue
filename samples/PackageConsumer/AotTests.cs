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
