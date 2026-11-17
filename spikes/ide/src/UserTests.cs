// What the user writes. The line numbers are referenced by Generated/UserTests.Verified.g.cs and docs/research/ide-report.md.
using System.Globalization;
using System.Text;
using VerificationGlue;
using VerificationGlue.Mocks;
using static VerificationGlue.VerifySyntax;
using Money = System.Decimal;

namespace MyApp.Tests;

public interface ICalc
{
    int Add(int a, int b);
}

public partial class OrderTests
{
    [VerifyTest]
    public void HelperCall()
    {
        var total = 40;
        CheckTotal(total);
        total += 2;
        Verify(total == 42);
    }

    [Verify]
    void CheckTotal(int total)
    {
        var doubled = total * 2;
        Verify(doubled == 80);
    }

    [VerifyTest]
    public void FileUsings()
    {
        var sb = new StringBuilder();
        Money price = 12.5m;
        sb.Append(price.ToString(CultureInfo.InvariantCulture));
        var text = sb.ToString();
        Verify(text == "12.5");
    }

    [VerifyTest]
    public void MockCalls()
    {
        var calc = Mock.Of<ICalc>();
        var sum = calc.Add(1, 2);
        Verify(calc.Calls().Add(1, 2) == 1);
        Verify(sum == 0);
    }
}
