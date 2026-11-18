namespace Checkout;

public static class Pricing
{
    public static int Total(int quantity, int price) => quantity * price;

    public static int Discounted(int total, string code) => code switch
    {
        "SAVE10" => total * 90 / 100,
        "HALF" => total / 2,
        _ => total,
    };
}

public sealed class Receipts
{
    public async Task<string> PrintAsync(int total, CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        return $"Total: {total}";
    }
}

/// <summary>Stock that every test in the process shares, so two tests that change it must not overlap.</summary>
public static class Stock
{
    private static int s_count;

    public static void Reset(int count) => s_count = count;

    public static async Task<int> TakeAsync(int count)
    {
        var before = s_count;
        await Task.Delay(10);
        s_count = before - count;
        return s_count;
    }
}
