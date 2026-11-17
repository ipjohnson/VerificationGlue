namespace Billing;

public interface IPriceList
{
    int PriceOf(string sku);

    Task<int> PriceOfAsync(string sku);
}

public interface IReceipts
{
    void Send(string customer, int total);
}

public sealed class Quote(IPriceList prices)
{
    public int Total(params string[] skus) => skus.Sum(prices.PriceOf);
}

public sealed class Invoice(IPriceList prices, IReceipts receipts)
{
    public int Bill(string customer, params string[] skus)
    {
        var total = skus.Sum(prices.PriceOf);
        receipts.Send(customer, total);
        return total;
    }

    public async Task<int> BillAsync(string customer, params string[] skus)
    {
        var total = 0;
        foreach (var sku in skus)
        {
            total += await prices.PriceOfAsync(sku);
        }

        receipts.Send(customer, total);
        return total;
    }
}
