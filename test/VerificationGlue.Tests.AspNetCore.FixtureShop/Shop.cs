using Microsoft.AspNetCore.Mvc;

namespace FixtureShop;

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

[ApiController]
[Route("clock")]
public sealed class ClockController(IClock clock) : ControllerBase
{
    [HttpGet]
    public DateTime Get() => clock.Now;
}

/// <summary>A scoped service that logs when it is disposed, and whether the app was stopping then.</summary>
public sealed class Basket(ILogger<Basket> log, IHostApplicationLifetime lifetime) : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
        log.LogInformation(lifetime.ApplicationStopping.IsCancellationRequested
            ? "Basket disposed while the app stops"
            : "Basket disposed while the app runs");
    }
}

/// <summary>
/// Counts the entry points that run between their start and builder.Build(), and keeps the most that
/// ran there at once. The apps of a test process share it.
/// </summary>
public static class BuildWindow
{
    private static int s_current;
    private static int s_most;

    public static int Most => Volatile.Read(ref s_most);

    /// <summary>
    /// Starts a new count of the most. It keeps the current count, because the app of another test can be
    /// between its start and Build() and would take the count below zero when it leaves.
    /// </summary>
    public static void Reset() => Volatile.Write(ref s_most, Volatile.Read(ref s_current));

    public static void Enter()
    {
        var current = Interlocked.Increment(ref s_current);
        int most;
        while (current > (most = Volatile.Read(ref s_most)) && Interlocked.CompareExchange(ref s_most, current, most) != most)
        {
        }
    }

    public static void Exit() => Interlocked.Decrement(ref s_current);
}
