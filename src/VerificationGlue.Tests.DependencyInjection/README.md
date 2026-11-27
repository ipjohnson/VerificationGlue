![VerificationGlue](https://raw.githubusercontent.com/ipjohnson/VerificationGlue/main/assets/logo/lockup.png)

# VerificationGlue.Tests.DependencyInjection

Gives `VerificationGlue.Tests` tests a Microsoft.Extensions.DependencyInjection container. The test's parameters come from the container, and its `[Mock]` parameters replace the matching registrations.

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

`ShopServices` registers the services in a static `Configure` method:

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

The test project references this package and `VerificationGlue.Tests`. The generator in this package writes the call to `Configure`, so the container calls it with no reflection.

## Docs

- [Give tests a service collection](../../docs/guide/service-collection.md)
- [Containers and scopes](../../docs/guide/containers-and-scopes.md)
- [VGD0001](../../docs/diagnostics/VGD0001.md) and [VGD0002](../../docs/diagnostics/VGD0002.md)
