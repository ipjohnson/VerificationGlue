# Give tests a service collection

At the end of this page you can give tests a Microsoft.Extensions.DependencyInjection container with `[ServiceCollection<T>]` and `[ServiceCollection]`, replace a service with a `[Mock]` parameter, and share one container across a class.

## Reference the package

The test project references `VerificationGlue.Tests` and `VerificationGlue.Tests.DependencyInjection`. Both have the same version. The package brings Microsoft.Extensions.DependencyInjection and a generator.

## Name the services with `[ServiceCollection<T>]`

`T` is a class with a public static `Configure` method that registers services:

<!-- snippet: service-collection-shop-services -->
<a id='snippet-service-collection-shop-services'></a>
```cs
public sealed class ShopServices
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<IOrderStore, InMemoryOrderStore>();
        services.AddTransient<OrderService>();
        services.AddScoped<Basket>();
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/Services.cs#L20-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-shop-services' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Put `[ServiceCollection<ShopServices>]` on a test class. Each test then gets a container with those services, and its parameters come from it. A `[Mock]` parameter replaces the registration of its type, so `OrderService` receives the mock:

<!-- snippet: service-collection-mock -->
<a id='snippet-service-collection-mock'></a>
```cs
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
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L10-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-mock' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The generator writes the call to `Configure`, so the container calls it with no reflection. A `T` with no such method fails the build with [VGD0001](../diagnostics/VGD0001.md). `[ServiceCollection<string>]` is one example.

`Configure` can take a second parameter of `TestContainerContext`. It holds the scope, the attributes of the scope and the test's output:

<!-- snippet: service-collection-payment-services -->
<a id='snippet-service-collection-payment-services'></a>
```cs
public sealed class PaymentServices
{
    public static void Configure(IServiceCollection services, TestContainerContext context)
    {
        context.Output.WriteLine($"Payment services for {context.Uid}");
        services.AddKeyedSingleton<IPaymentGateway, AcceptAll>("card");
        services.AddKeyedSingleton<IPaymentGateway, AcceptAll>("invoice");
        services.AddTransient<CheckoutService>();
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/Services.cs#L32-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-payment-services' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Replace a keyed service

A `[Mock]` parameter with `[FromKeyedServices(key)]` replaces the registration under that key. The other keys keep their registrations:

<!-- snippet: service-collection-keyed-mock -->
<a id='snippet-service-collection-keyed-mock'></a>
```cs
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
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L27-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-keyed-mock' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Sources add up

`[ServiceCollection<T>]` goes on the assembly, a class or a method. The container of a test calls the `Configure` method of each one that applies, from the assembly to the class to the method. A source on the class does not replace the one on the assembly. A registration from the method comes last, so it wins when it registers the same service.

This assembly registers the clock for every test:

<!-- snippet: service-collection-assembly -->
<a id='snippet-service-collection-assembly'></a>
```cs
[assembly: ServiceCollection<ClockServices>]
```
<sup><a href='/samples/TestsDependencyInjection/Services.cs#L6-L8' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-assembly' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: service-collection-clock-services -->
<a id='snippet-service-collection-clock-services'></a>
```cs
public sealed class ClockServices
{
    public static void Configure(IServiceCollection services) =>
        services.AddSingleton<IClock, SystemClock>();
}
```
<sup><a href='/samples/TestsDependencyInjection/Services.cs#L12-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-clock-services' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The class adds the shop's services, and one method adds a fixed clock:

<!-- snippet: service-collection-add-up -->
<a id='snippet-service-collection-add-up'></a>
```cs
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
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L42-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-add-up' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The test's `[Mock]` parameters are registered after every source.

## Use `[ServiceCollection]` without a type

`[ServiceCollection]` gives the tests a container from the sources of the wider targets, and adds no source of its own. Here the class gets the clock from the assembly:

<!-- snippet: service-collection-plain -->
<a id='snippet-service-collection-plain'></a>
```cs
[ServiceCollection]
public partial class ClockTests
{
    [VerifyTest]
    public void Gets_the_assembly_clock(IClock clock)
    {
        Verify(clock is SystemClock);
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L62-L72' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-plain' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Share a container across a class or the assembly

`Scope = ContainerScope.Class` builds one container for the tests of the class, from the attributes of the class and the assembly. `Scope = ContainerScope.Assembly` builds one from the attributes of the assembly. Each test gets its own service scope, so a scoped service such as `Basket` is new in each test, and a singleton such as the store is shared:

<!-- snippet: service-collection-shared -->
<a id='snippet-service-collection-shared'></a>
```cs
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
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L74-L100' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-shared' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A shared container does not read the attributes of a narrower target. `[ServiceCollection<T>(Scope = ContainerScope.Class)]` on a method, or `Scope = ContainerScope.Assembly` on a class or a method, fails the build with [VGD0002](../diagnostics/VGD0002.md).

A test of a shared container cannot take a `[Mock]` parameter. The service scope adds no registrations, so the services would not receive the mock. The build fails with [VGT0008](../diagnostics/VGT0008.md). Use the default scope for tests with mocks.

## Next

- [Containers and scopes](containers-and-scopes.md)
- [Parameters and where they come from](test-parameters.md)
