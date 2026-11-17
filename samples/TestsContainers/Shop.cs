namespace TestsContainers;

public interface IClock
{
    DateTime Now { get; }
}

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

public sealed class OrderService(IClock clock, IOrderStore store)
{
    public Order Create(int quantity, int price)
    {
        var order = new Order(quantity, price, clock.Now);
        store.Save(order);
        return order;
    }
}
