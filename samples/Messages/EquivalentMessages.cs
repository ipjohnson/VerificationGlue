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
}

public enum ShipmentStatus
{
    Packed,
    Sent,
}

public sealed record Address(string Name, string City);

public sealed record Shipment(int Id, Address To, ShipmentStatus Status, Address? ReturnTo);
