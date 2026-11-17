namespace Messages;

public sealed record Order(int Id, int Total, string Shipping)
{
    public IReadOnlyList<int> Lines { get; init; } = [10, 20];

    public override string ToString() => $"order {Id}";
}

public sealed class Cart
{
    private readonly List<int> _items = [];

    public int Count => _items.Count;

    public void Add(int item) => _items.Add(item);
}
