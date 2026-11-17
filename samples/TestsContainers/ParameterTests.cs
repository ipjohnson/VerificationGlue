using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace TestsContainers;

#region tests-parameter-provider
/// <summary>Supplies the string parameters of the tests, or of the parameter, it is placed on.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter)]
public sealed class SettingAttribute(string value) : Attribute, ITestParameterProvider
{
    public bool TryProvide(TestContext context, TestParameter parameter, out object? provided)
    {
        provided = parameter.Type == typeof(string) ? value : null;
        return provided is not null;
    }
}
#endregion

#region tests-parameter-sources
[Container<ShopBootstrap>]
[Setting("eu")]
public partial class ParameterSourceTests
{
    [VerifyTest]
    public void Takes_a_value_from_each_source(
        string region,
        [Setting("eur")] string currency,
        [Mock] IClock clock,
        OrderService sut,
        TestContext context,
        int quantity = 3)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var order = sut.Create(quantity, price: 4);

        Verify(region == "eu");
        Verify(currency == "eur");
        Verify(order.Created == clock.Now);
        Verify(order.Total == 12);
        Verify(context.Uid.StartsWith("TestsContainers.ParameterSourceTests.", StringComparison.Ordinal));
    }
}
#endregion

#region tests-constructor-parameters
[Container<ShopBootstrap>]
public sealed partial class CheckoutTests([Mock] IClock clock, OrderService sut)
{
    [VerifyTest]
    public void Constructor_and_method_share_one_mock([Mock] IClock sameClock)
    {
        sameClock.Now.Returns(new DateTime(2026, 11, 17));

        Verify(ReferenceEquals(clock, sameClock));
        Verify(sut.Create(quantity: 1, price: 1).Created == new DateTime(2026, 11, 17));
    }
}
#endregion
