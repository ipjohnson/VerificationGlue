namespace ShopApi;

public interface IClock
{
    DateTime Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.UtcNow;
}

public sealed record CreateOrder(int Quantity);

public sealed record Order(int Id, int Quantity, DateTime Created);

public interface IOrderStore
{
    IReadOnlyList<Order> Orders { get; }

    Order Add(int quantity, DateTime created);
}

public sealed class OrderStore : IOrderStore
{
    private readonly List<Order> _orders = [];

    public IReadOnlyList<Order> Orders
    {
        get
        {
            lock (_orders)
            {
                return [.. _orders];
            }
        }
    }

    public Order Add(int quantity, DateTime created)
    {
        lock (_orders)
        {
            var order = new Order(_orders.Count + 1, quantity, created);
            _orders.Add(order);
            return order;
        }
    }
}
