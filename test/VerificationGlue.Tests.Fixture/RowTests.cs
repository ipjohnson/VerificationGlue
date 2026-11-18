using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

[Container<FixtureBootstrap>]
public partial class RowTests
{
    [VerifyTest]
    [Row(2, 10)]
    [Row(3, 5)]
    public void Totals(int qty, int price, Database database)
    {
        Log.Write($"Totals({qty}, {price}) used database {database.Id}");
        Verify(qty * price > 0);
    }
}

public sealed class ShoutingNameFormatter : ITestNameFormatter
{
    public static string Format(in TestNameContext context) =>
        context.MethodName.ToUpperInvariant() + " " + string.Join(" ", context.Arguments);
}

[NameFormatter<ShoutingNameFormatter>]
public partial class FormattedRowTests
{
    [VerifyTest]
    [Row("gift")]
    public void Wraps(string label) => Verify(label == "gift");
}
