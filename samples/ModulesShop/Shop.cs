using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ModulesShop;

#region sut-module
[DependencyModule]
public partial class SutModule;
#endregion

public interface IClock
{
    DateTime Now { get; }
}

[SingletonService]
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.UtcNow;
}

public sealed record Order(int Quantity, int Price, DateTime Created)
{
    public int Total => Quantity * Price;
}

public interface IOrderStore
{
    IReadOnlyList<Order> Orders { get; }

    void Save(Order order);
}

[SingletonService]
public sealed class InMemoryOrderStore : IOrderStore
{
    private readonly List<Order> _orders = [];

    public IReadOnlyList<Order> Orders
    {
        get
        {
            lock (_orders)
            {
                return _orders.ToArray();
            }
        }
    }

    public void Save(Order order)
    {
        lock (_orders)
        {
            _orders.Add(order);
        }
    }
}

[TransientService]
public sealed class OrderService(IClock clock, IOrderStore store)
{
    public Order Create(int quantity, int price)
    {
        var order = new Order(quantity, price, clock.Now);
        store.Save(order);
        return order;
    }
}

public sealed record MailSettings(string Host, int Port, string? Sender);

#region mail-module
[DependencyModule(OnlyRealm = true)]
public partial class MailModule(string host, int port) : IServiceCollectionConfiguration
{
    public string? Sender { get; set; }

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton(new MailSettings(host, port, Sender));

    public override bool Equals(object? obj) =>
        obj is MailModule other && other.Settings == Settings;

    public override int GetHashCode() => Settings.GetHashCode();

    private MailSettings Settings => new(host, port, Sender);
}
#endregion
