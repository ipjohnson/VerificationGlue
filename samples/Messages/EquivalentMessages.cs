using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary>Tier 2: <c>VerifyEquivalent</c> and <c>CheckEquivalent</c>, which compare two values member by member.</summary>
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

    public static void Maps()
    {
        #region message-tier2-equivalent-maps
        var parsed = new Stock(new() { ["gift"] = 3, ["wrap"] = 1 }, ["fragile", "gift"]);
        var expected = new Stock(new() { ["gift"] = 5, ["card"] = 2 }, ["gift", "express"]);
        VerifyEquivalent(parsed, expected);
        #endregion
    }

    public static void Dictionary()
    {
        #region verify-equivalent-dictionary
        var parsed = new Stock(new() { ["gift"] = 3, ["wrap"] = 1 }, ["gift"]);
        var expected = new Stock(new() { ["gift"] = 5, ["card"] = 2 }, ["gift"]);
        VerifyEquivalent(parsed, expected);
        #endregion
    }

    public static void Set()
    {
        #region verify-equivalent-set
        var parsed = new Stock(new() { ["gift"] = 3 }, ["fragile", "gift"]);
        var expected = new Stock(new() { ["gift"] = 3 }, ["gift", "express"]);
        VerifyEquivalent(parsed, expected);
        #endregion
    }

    public static void Options()
    {
        #region verify-equivalent-options
        var parsed = new Invoice(8, [new InvoiceLine("B-2", 3), new InvoiceLine("A-1", 2)], ["gift", "gift"]);
        var expected = new Invoice(7, [new InvoiceLine("A-1", 2), new InvoiceLine("B-2", 3)], ["fragile", "gift"]);
        VerifyEquivalent(parsed, expected, options => options.Unordered(x => x.Lines).Unordered(x => x.Tags).Ignore(x => x.Id));
        #endregion
    }

    public static void CheckInScope()
    {
        #region check-equivalent
        var parsed = new Invoice(7, [new InvoiceLine("A-1", 1)], ["gift"]);
        var expected = new Invoice(8, [new InvoiceLine("A-1", 2)], ["gift"]);

        using (VerifyScope.Begin())
        {
            CheckEquivalent(parsed, expected, options => options.Ignore(x => x.Id));
            CheckEquivalent(parsed.Tags, expected.Tags);
            Check(parsed.Lines.Count == 2);
        }
        #endregion
    }
}

#region verify-equivalent-types
public sealed record InvoiceLine(string Sku, int Quantity);

public sealed record Invoice(int Id, List<InvoiceLine> Lines, string[] Tags);

public sealed record Stock(Dictionary<string, int> Levels, HashSet<string> Tags);
#endregion

public enum ShipmentStatus
{
    Packed,
    Sent,
}

public sealed record Address(string Name, string City);

public sealed record Shipment(int Id, Address To, ShipmentStatus Status, Address? ReturnTo);
