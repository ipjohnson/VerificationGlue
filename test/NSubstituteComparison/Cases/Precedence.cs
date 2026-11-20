namespace NSubstituteComparison.Cases;

[Area("Setup precedence", 2)]
public static class Precedence
{
    [Case("A later setup for the same arguments wins")]
    public static void Later_setup_wins(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2).Returns(3);
        calc.Add(1, 2).Returns(4);

        observe.Record("Add(1, 2)", calc.Add(1, 2));
    }

    [Case("A later Arg.Any setup hides an earlier exact one")]
    public static void Any_after_exact(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2).Returns(3);
        calc.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(9);

        observe.Record("Add(1, 2)", calc.Add(1, 2));
        observe.Record("Add(5, 5)", calc.Add(5, 5));
    }

    [Case("A later exact setup wins over an earlier Arg.Any one")]
    public static void Exact_after_any(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(9);
        calc.Add(1, 2).Returns(3);

        observe.Record("Add(1, 2)", calc.Add(1, 2));
        observe.Record("Add(5, 5)", calc.Add(5, 5));
    }

    [Case("The last of overlapping Arg.Is setups wins for the values it matches")]
    public static void Overlapping_predicates(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(Arg.Is<int>(x => x > 5), 1).Returns(1);
        calc.Add(Arg.Is<int>(x => x > 10), 1).Returns(2);

        observe.Record("Add(7, 1)", calc.Add(7, 1));
        observe.Record("Add(11, 1)", calc.Add(11, 1));
        observe.Record("Add(3, 1)", calc.Add(3, 1));
    }

    [Case("The call inside a setup is not a received call")]
    public static void Setup_call_is_not_received(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2).Returns(3);

        observe.Try("DidNotReceive().Add(1, 2)", () => calc.DidNotReceive().Add(1, 2));
    }

    [Case("A setup call runs the response set up before it")]
    public static void Setup_after_throws(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Divide(1, 0).Throws(new DivideByZeroException());

        observe.Try("Divide(1, 0).Returns(5)", () => calc.Divide(1, 0).Returns(5));
        observe.TryValue("Divide(1, 0)", () => calc.Divide(1, 0));
    }

    [Case("When...Do runs on a setup call and on later calls")]
    public static void When_do_and_returns(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var count = 0;
        calc.When(c => c.Add(1, 2)).Do(_ => count++);
        calc.Add(1, 2).Returns(3);
        observe.Record("count after the setup", count);

        observe.Record("Add(1, 2)", calc.Add(1, 2));
        observe.Record("count after the call", count);
    }

    [Case("When...Do on a void member, then a second callback")]
    public static void Two_callbacks(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var calls = new List<string>();
        calc.When(c => c.Reset()).Do(_ => calls.Add("first"));
        calc.When(c => c.Reset()).Do(_ => calls.Add("second"));

        calc.Reset();
        observe.Record("callbacks", string.Join(", ", calls));
    }

    [Case("A property keeps the value set on it")]
    public static void Property_set_then_get(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Mode = 4;

        observe.Record("Mode", calc.Mode);
    }

    [Case("A set after Returns on a property wins")]
    public static void Property_set_after_returns(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Mode.Returns(7);
        observe.Record("Mode after Returns(7)", calc.Mode);

        calc.Mode = 2;
        observe.Record("Mode after the set", calc.Mode);
    }

    [Case("Returns null on a string member wins over the empty-string auto-value")]
    public static async Task Returns_null_for_a_string(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Describe("x").Returns((string?)null);
        calc.NameAsync().Returns((string)null!);

        observe.Record("Describe(\"x\") is null", calc.Describe("x") is null);
        observe.Record("Describe(\"y\")", calc.Describe("y"));
        observe.Record("NameAsync() result is null", await calc.NameAsync() is null);
    }
}
