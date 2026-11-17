using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace TestsContainers;

#region tests-container-per-test
[Container<ShopBootstrap>]
public partial class OrderServiceTests
{
    [VerifyTest]
    public void Create_stamps_the_order_with_the_clock([Mock] IClock clock, OrderService sut, IOrderStore store)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var order = sut.Create(quantity: 2, price: 10);

        Verify(order.Created == new DateTime(2026, 11, 17));
        Verify(store.Orders.Count == 1);
    }
}
#endregion

#region tests-container-per-class
[Container<SharedStoreBootstrap>(Scope = ContainerScope.Class)]
public partial class OrderHistoryTests
{
    [VerifyTest]
    public void Each_test_gets_its_own_mock([Mock] IClock clock, OrderService sut, IOrderStore store)
    {
        clock.Now.Returns(new DateTime(2026, 1, 1));

        var order = sut.Create(quantity: 1, price: 5);

        Verify(order.Created.Year == 2026);
        Verify(store.Orders.Contains(order));
    }

    [VerifyTest]
    public void The_store_is_shared_by_the_class(IOrderStore store, OrderService sut)
    {
        var order = sut.Create(quantity: 3, price: 2);

        Verify(store.Orders.Contains(order));
    }
}
#endregion

#region tests-container-per-assembly
[Container<SharedStoreBootstrap>(Scope = ContainerScope.Assembly)]
public partial class ReportTests
{
    [VerifyTest]
    public void Sees_orders_from_the_assembly_store([Mock] IClock clock, OrderService sut, IOrderStore store)
    {
        sut.Create(quantity: 4, price: 1);

        Verify(store.Orders.Any(order => order.Total == 4));
    }
}
#endregion
