using Microsoft.Extensions.DependencyInjection;
using TestsDependencyInjection;
using VerificationGlue.Tests;
using VerificationGlue.Tests.DependencyInjection;

#region service-collection-assembly
[assembly: ServiceCollection<ClockServices>]
#endregion

namespace TestsDependencyInjection;

#region service-collection-clock-services
public sealed class ClockServices
{
    public static void Configure(IServiceCollection services) =>
        services.AddSingleton<IClock, SystemClock>();
}
#endregion

#region service-collection-shop-services
public sealed class ShopServices
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<IOrderStore, InMemoryOrderStore>();
        services.AddTransient<OrderService>();
        services.AddScoped<Basket>();
    }
}
#endregion

#region service-collection-payment-services
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
#endregion

public sealed class FixedClockServices
{
    public static void Configure(IServiceCollection services) =>
        services.AddSingleton<IClock, FixedClock>();
}
