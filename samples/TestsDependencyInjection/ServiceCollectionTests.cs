using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.DependencyInjection;
using static VerificationGlue.VerifySyntax;

namespace TestsDependencyInjection;

#region service-collection-mock
[ServiceCollection<ShopServices>]
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

#region service-collection-keyed-mock
[ServiceCollection<PaymentServices>]
public partial class CheckoutTests
{
    [VerifyTest]
    public void The_card_declines([Mock][FromKeyedServices("card")] IPaymentGateway card, CheckoutService sut)
    {
        card.Charge(10).Returns(false);

        Verify(!sut.Pay(10, byInvoice: false));
        Verify(sut.Pay(10, byInvoice: true));
    }
}
#endregion

#region service-collection-add-up
[ServiceCollection<ShopServices>]
public partial class SourceOrderTests
{
    [VerifyTest]
    public void The_assembly_and_the_class_add_up(OrderService sut, IClock clock)
    {
        Verify(clock is SystemClock);
        Verify(sut.Create(quantity: 1, price: 3).Total == 3);
    }

    [VerifyTest]
    [ServiceCollection<FixedClockServices>]
    public void The_method_registers_last(OrderService sut)
    {
        Verify(sut.Create(quantity: 1, price: 3).Created == new DateTime(2030, 1, 1));
    }
}
#endregion

#region service-collection-plain
[ServiceCollection]
public partial class ClockTests
{
    [VerifyTest]
    public void Gets_the_assembly_clock(IClock clock)
    {
        Verify(clock is SystemClock);
    }
}
#endregion

#region service-collection-shared
[ServiceCollection<ShopServices>(Scope = ContainerScope.Class)]
public partial class OrderHistoryTests
{
    private static readonly ConcurrentBag<(Basket Basket, IOrderStore Store)> s_seen = [];

    [VerifyTest]
    public void Saves_an_order(OrderService sut, IOrderStore store, Basket basket)
    {
        s_seen.Add((basket, store));
        var order = sut.Create(quantity: 3, price: 2);

        Verify(store.Orders.Contains(order));
        Verify(s_seen.All(seen => seen.Store == store));
        Verify(s_seen.Count(seen => seen.Basket == basket) == 1);
    }

    [VerifyTest]
    public void Sees_the_same_store(IOrderStore store, Basket basket)
    {
        s_seen.Add((basket, store));

        Verify(s_seen.All(seen => seen.Store == store));
        Verify(s_seen.Count(seen => seen.Basket == basket) == 1);
    }
}
#endregion
