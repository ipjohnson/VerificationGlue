namespace NSubstituteComparison.Cases;

[Area("Argument matching", 1)]
public static class Matching
{
    [Case("Exact arguments match only the same values")]
    public static void Exact_arguments(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2).Returns(3);

        observe.Record("Add(1, 2)", calc.Add(1, 2));
        observe.Record("Add(2, 1)", calc.Add(2, 1));
    }

    [Case("Arg.Any matches every value")]
    public static void Any_for_every_argument(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(7);

        observe.Record("Add(4, 5)", calc.Add(4, 5));
        observe.Record("Add(0, 0)", calc.Add(0, 0));
    }

    [Case("Arg.Is with a predicate")]
    public static void Is_with_a_predicate(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(Arg.Is<int>(x => x > 10), 1).Returns(9);

        observe.Record("Add(11, 1)", calc.Add(11, 1));
        observe.Record("Add(10, 1)", calc.Add(10, 1));
    }

    [Case("Arg.Is with a value next to Arg.Any")]
    public static void Is_with_a_value(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(Arg.Is(3), Arg.Any<int>()).Returns(4);

        observe.Record("Add(3, 8)", calc.Add(3, 8));
        observe.Record("Add(4, 8)", calc.Add(4, 8));
    }

    [Case("A matcher after an argument that holds the default value")]
    [Difference(
        "VerificationGlue.Mocks gives each matcher the argument it is written at, which a generated interceptor reads from the source. " +
        "NSubstitute cannot tell which of the two zero arguments the matcher stands for.")]
    public static void Matcher_after_a_default_argument(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        observe.Try("Add(0, Arg.Any<int>()).Returns(5)", () => calc.Add(0, Arg.Any<int>()).Returns(5));

        observe.Record("Add(0, 9)", calc.Add(0, 9));
        observe.Record("Add(9, 0)", calc.Add(9, 0));
    }

    [Case("A matcher before an argument that holds the default value")]
    [Difference(
        "VerificationGlue.Mocks gives each matcher the argument it is written at. " +
        "NSubstitute rejects the setup, because the 0 in the second argument could also be the value a matcher returned.")]
    public static void Matcher_before_a_default_argument(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        observe.Try("Add(Arg.Any<int>(), 0).Returns(5)", () => calc.Add(Arg.Any<int>(), 0).Returns(5));

        observe.Record("Add(9, 0)", calc.Add(9, 0));
        observe.Record("Add(9, 1)", calc.Add(9, 1));
    }

    [Case("A null argument")]
    public static void Null_argument(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Describe(null).Returns("none");

        observe.Record("Describe(null)", calc.Describe(null));
        observe.Record("Describe(\"a\")", calc.Describe("a"));
    }

    [Case("Arg.Any matches null")]
    public static void Any_matches_null(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Describe(Arg.Any<string?>()).Returns("any");

        observe.Record("Describe(null)", calc.Describe(null));
        observe.Record("Describe(\"a\")", calc.Describe("a"));
    }

    [Case("A params argument matches the same values in a new array")]
    public static void Params_argument(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Sum(1, 2).Returns(3);

        observe.Record("Sum(1, 2)", calc.Sum(1, 2));
        observe.Record("Sum(1, 2, 3)", calc.Sum(1, 2, 3));
    }

    [Case("An array argument that is not params matches the same values in a new array")]
    [Difference(
        "VerificationGlue.Mocks compares every array argument by its elements. " +
        "NSubstitute does that only for a params argument, and compares other arrays by reference.")]
    public static void Array_argument(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Count(new[] { 1, 2 }).Returns(2);

        observe.Record("Count(new[] { 1, 2 })", calc.Count(new[] { 1, 2 }));
    }

    [Case("Received with matchers")]
    public static void Received_with_matchers(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2);

        observe.Try("Received().Add(Arg.Any<int>(), 2)", () => calc.Received().Add(Arg.Any<int>(), 2));
        observe.Try("Received().Add(Arg.Is<int>(x => x > 5), 2)", () => calc.Received().Add(Arg.Is<int>(x => x > 5), 2));
        observe.Try("DidNotReceive().Add(2, Arg.Any<int>())", () => calc.DidNotReceive().Add(2, Arg.Any<int>()));
    }

    [Case("An out argument in a setup")]
    public static void Out_argument(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.TryParse("7", out _).Returns(x =>
        {
            x[1] = 7;
            return true;
        });

        observe.Record("TryParse(\"7\", out value)", calc.TryParse("7", out var value));
        observe.Record("value", value);
        observe.Record("TryParse(\"8\", out other)", calc.TryParse("8", out var other));
        observe.Record("other", other);
    }
}
