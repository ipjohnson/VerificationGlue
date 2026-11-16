namespace Contracts;

public interface ICalculator
{
    int Add(int a, int b);

    string Mode { get; set; }

    int this[string key] { get; set; }

    event EventHandler<int>? Changed;

    Task<int> AddAsync(int a, int b);

    ValueTask<string> NameAsync();

    Task SaveAsync();

    void Reset();

    bool TryParse(string text, out int value);

    void Bump(ref int counter);

    T Convert<T>(object value);

    int Twice(int a) => Add(a, a);
}

public interface IRepository<T>
{
    T? Find(int id);

    void Save(T item);
}

public interface ISpanReader
{
    int Read(Span<byte> buffer);

    int Length { get; }
}

public class Clock
{
    public virtual DateTime Now => DateTime.UtcNow;

    public virtual string Format(DateTime value) => value.ToString("O");

    public string NotVirtual() => "real";
}

public record Order(int Id, string Sku);

public class OrderService(ICalculator calculator, IRepository<Order> orders)
{
    public int Total(int id)
    {
        var order = orders.Find(id);
        return order is null ? 0 : calculator.Add(order.Id, 100);
    }
}
