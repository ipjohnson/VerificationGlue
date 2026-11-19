using VerificationGlue;
using static VerificationGlue.VerifySyntax;

namespace Messages;

/// <summary>
/// <c>Verify(() =&gt; ...)</c> in an ordinary method. The generated check reads the lambda's captured
/// variables and lists the value of each part of its body. The class needs no <c>[Verify]</c>
/// attribute and no <c>partial</c>.
/// </summary>
public sealed class LambdaMessages
{
    private readonly Order _order = new(7, 30, "standard");

    public static void Values()
    {
        #region message-lambda-values
        var order = new Order(7, 30, "standard");
        Verify(() => order.Shipping.StartsWith("express") && order.Lines.Count < 3);
        #endregion
    }

    public static void Collection()
    {
        #region message-lambda-collection
        var problems = new List<string> { "flowchart/a: box differs", "pie/b: missing label" };
        Verify(() => problems.Count == 0);
        #endregion
    }

    public static void Lazy()
    {
        #region message-lambda-lazy
        var totals = new[] { 30, 45 }.Select(total => total * 2);
        Verify(() => totals.Count() == 0);
        #endregion
    }

    public static void Members() => new LambdaMessages().VerifyTotal(40);

    #region message-lambda-members
    private void VerifyTotal(int expected)
    {
        Verify(() => _order.Total == expected);
    }
    #endregion

    public static void Fallback()
    {
        #region message-lambda-fallback
        var total = 30;
        Verify(() => IsEven(total + 1));

        static bool IsEven(int value) => value % 2 == 0;
        #endregion
    }
}
