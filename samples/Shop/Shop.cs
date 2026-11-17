namespace Shop;

public sealed record Order(int Id, int Total);

public sealed class Cart
{
    public void Checkout() => throw new InvalidOperationException("The cart is empty.");
}

public sealed class Client
{
    public async Task<string> GetAsync(string url)
    {
        await Task.Yield();
        throw new TimeoutException($"{url} did not answer.");
    }
}
