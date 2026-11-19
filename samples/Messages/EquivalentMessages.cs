using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary>Tier 2: <c>VerifyEquivalent</c>, which compares two values member by member.</summary>
public static class EquivalentMessages
{
    public static void Members()
    {
        #region message-tier2-equivalent
        var parsed = new Shipment(7, new Address("Ada", "London"), ShipmentStatus.Packed, null);
        var expected = new Shipment(7, new Address("Ada", "Paris"), ShipmentStatus.Sent, new Address("Ada", "Paris"));
        VerifyEquivalent(parsed, expected);
        #endregion
    }

    public static void Items()
    {
        #region verify-equivalent-items
        var parsed = new Invoice(7, [new InvoiceLine("A-1", 2), new InvoiceLine("B-2", 1)], ["gift"]);
        var expected = new Invoice(7, [new InvoiceLine("A-1", 2), new InvoiceLine("B-2", 3)], ["gift"]);
        VerifyEquivalent(parsed, expected);
        #endregion
    }

    public static void Counts()
    {
        #region verify-equivalent-counts
        var parsed = new Invoice(7, [new InvoiceLine("A-1", 1)], ["gift", "fragile"]);
        var expected = new Invoice(7, [new InvoiceLine("A-1", 2), new InvoiceLine("B-2", 3)], ["gift"]);
        VerifyEquivalent(parsed, expected);
        #endregion
    }

    public static void Dictionary()
    {
        #region verify-equivalent-dictionary
        var parsed = new Stock(new Dictionary<string, int> { ["gift"] = 3 });
        var expected = new Stock(new Dictionary<string, int> { ["gift"] = 3 });
        VerifyEquivalent(parsed, expected);
        #endregion
    }
}

#region verify-equivalent-types
public sealed record InvoiceLine(string Sku, int Quantity);

public sealed record Invoice(int Id, List<InvoiceLine> Lines, string[] Tags);

public sealed record Stock(Dictionary<string, int> Levels);
#endregion

public enum ShipmentStatus
{
    Packed,
    Sent,
}

public sealed record Address(string Name, string City);

public sealed record Shipment(int Id, Address To, ShipmentStatus Status, Address? ReturnTo);
