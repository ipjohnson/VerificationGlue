namespace Store;

public class Order(int id, int total, IReadOnlyList<int> lines, string shipping)
{
    public int Id { get; } = id;

    public int Total { get; } = total;

    public IReadOnlyList<int> Lines { get; } = lines;

    public string Shipping { get; } = shipping;

    public bool IsPaid { get; init; }

    public string? Coupon { get; init; }

    public double Tax => Total * 0.1;
}

public sealed class PaidOrder(int id, int total, IReadOnlyList<int> lines, string shipping) : Order(id, total, lines, shipping);

public sealed class Cart
{
    public List<int> Items { get; } = [];

    public void Checkout()
    {
        if (Items.Count == 0)
        {
            throw new InvalidOperationException("The cart is empty.");
        }
    }
}

public sealed class Client
{
    public async Task<string> GetAsync(string url)
    {
        await Task.Yield();
        throw new TimeoutException($"{url} did not answer.");
    }
}
