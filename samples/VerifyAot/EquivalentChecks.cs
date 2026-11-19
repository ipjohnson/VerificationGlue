using VerificationGlue;
using static VerificationGlue.VerifySyntax;

// The same calls as CheckInScope in samples/Messages/EquivalentMessages.cs, so the message matches docs/guide/messages.md.
public static class EquivalentChecks
{
    public static VerifyException CheckInScope() => Throws<VerifyException>(() =>
    {
        var parsed = new Invoice(7, [new InvoiceLine("A-1", 1)], ["gift"]);
        var expected = new Invoice(8, [new InvoiceLine("A-1", 2)], ["gift"]);

        using (VerifyScope.Begin())
        {
            CheckEquivalent(parsed, expected, options => options.Ignore(x => x.Id));
            CheckEquivalent(parsed.Tags, expected.Tags);
            Check(parsed.Lines.Count == 2);
        }
    });
}
