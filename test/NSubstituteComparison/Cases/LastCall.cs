namespace NSubstituteComparison.Cases;

#pragma warning disable VGM0002 // The cases misplace Arg matchers on purpose.
#pragma warning disable VGM0005 // The cases call Returns on values that are not mock calls on purpose.

[Area("Last-call hazards", 5)]
public static class LastCall
{
    [Case("Returns with no mock call before it")]
    [Difference(
        "Both libraries reject the setup, with different exception types. " +
        "VerificationGlue.Mocks also reports VGM0005 at build time for a Returns on a value that is not a mock call.")]
    public static void Returns_with_no_mock_call(Observations observe)
    {
        var mock = Mock.Of<ICalculator>();
        observe.Try("5.Returns(6)", () => 5.Returns(6));
        observe.Record("Add(0, 0)", mock.Add(0, 0));
    }

    [Case("Returns on a value that is not a mock call, after a mock call")]
    public static void Returns_on_a_value_after_a_mock_call(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2);
        observe.Try("\"abc\".Length.Returns(3)", () => "abc".Length.Returns(3));

        observe.Record("Add(1, 2)", calc.Add(1, 2));
    }

    [Case("A setup through a local that holds the result of a mock call")]
    public static void Setup_through_a_local(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var call = calc.Add(1, 2);
        call.Returns(3);

        observe.Record("Add(1, 2)", calc.Add(1, 2));
    }

    [Case("A mock call in the argument of Returns")]
    public static void Mock_call_inside_returns(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var other = Mock.Of<ICalculator>();
        other.Add(3, 4).Returns(7);

        observe.Try("calc.Add(1, 2).Returns(other.Add(3, 4))", () => calc.Add(1, 2).Returns(other.Add(3, 4)));
        observe.Record("calc.Add(1, 2)", calc.Add(1, 2));
        observe.Record("other.Add(3, 4)", other.Add(3, 4));
    }

    [Case("An Arg matcher stored in a local")]
    public static void Matcher_in_a_local(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var any = Arg.Any<int>();
        calc.Add(any, 5).Returns(3);

        observe.Record("Add(9, 5)", calc.Add(9, 5));
        observe.Record("Add(9, 6)", calc.Add(9, 6));
    }

    [Case("An abandoned Arg matcher before an unrelated call")]
    [Difference(
        "Both libraries fail the next call and then clear the matcher, with different exception types. " +
        "VerificationGlue.Mocks also reports VGM0002 at build time for a matcher that is not an argument of a mock call.")]
    public static void Abandoned_matcher(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        Arg.Any<string>();

        observe.TryValue("Add(1, 2)", () => calc.Add(1, 2));
        observe.TryValue("Add(1, 2) again", () => calc.Add(1, 2));
    }

    [Case("Received does not count as a call")]
    public static void Received_is_not_a_call(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2);
        calc.Received().Add(1, 2);

        observe.Try("Received(1).Add(1, 2)", () => calc.Received(1).Add(1, 2));
    }

    [Case("Received with the wrong count")]
    public static void Received_with_the_wrong_count(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2);
        calc.Add(1, 2);

        observe.Try("Received(1).Add(1, 2)", () => calc.Received(1).Add(1, 2));
        observe.Try("Received(2).Add(1, 2)", () => calc.Received(2).Add(1, 2));
        observe.Try("Received().Add(1, 2)", () => calc.Received().Add(1, 2));
    }
}
