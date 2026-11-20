// case: mocks-behaviour-0003
// layer: behaviour
// origin: mocks.md, Surface: Received, Received(count) and DidNotReceive
// expect: pass

namespace VerificationGlue.Mocks.Specs.Behaviour;

public class Verification
{
    [VerifyTest]
    public void Received_passes_for_a_call_that_was_made()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(1, 2);
        calc.Reset();
        calc.Reset();

        calc.Received().Add(1, 2);
        calc.Received(2).Reset();
        calc.Received().Add(Arg.Any<int>(), Arg.Is<int>(b => b > 1));
        calc.DidNotReceive().Divide(Arg.Any<int>(), Arg.Any<int>());
    }

    [VerifyTest]
    public void A_check_is_not_recorded_as_a_call()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Reset();
        calc.Received().Reset();
        calc.Received(1).Reset();

        Assert.Single(((IMock)calc).Adapter.ReceivedCalls());
    }

    [VerifyTest]
    public void Received_fails_with_the_expected_call_and_the_calls_received()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(3, 4);

        var error = Assert.Throws<ReceivedCallsException>(() => calc.Received().Add(1, 2));
        Assert.Equal(
            """
            Expected at least one call to VerificationGlue_Mocks_Specs_Behaviour_ICalculator_Mock.Add(1, 2), but received 0.
            Calls received:
              Add(3, 4)
            """.ReplaceLineEndings("\n"),
            error.Message);
    }

    [VerifyTest]
    public void Received_with_a_count_and_DidNotReceive_fail_on_the_wrong_count()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Reset();

        Assert.StartsWith("Expected exactly 2 calls to", Assert.Throws<ReceivedCallsException>(() => calc.Received(2).Reset()).Message);
        Assert.StartsWith("Expected no calls to", Assert.Throws<ReceivedCallsException>(() => calc.DidNotReceive().Reset()).Message);
    }

    [VerifyTest]
    public void Checks_take_no_value_from_a_response_sequence_and_give_the_same_result_when_repeated()
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2).Returns(10, 20, 30);

        var first = calc.Add(1, 2);
        calc.Received(1).Add(1, 2);
        calc.Received(1).Add(1, 2);
        var counts = new[] { calc.Calls().Add(1, 2) == 1, calc.Calls().Add(1, 2) == 1 };
        var failures = new[]
        {
            Assert.Throws<ReceivedCallsException>(() => calc.DidNotReceive().Add(1, 2)).Message,
            Assert.Throws<ReceivedCallsException>(() => calc.DidNotReceive().Add(1, 2)).Message,
        };
        var second = calc.Add(1, 2);

        Assert.Equal(10, first);
        Assert.Equal([true, true], counts);
        Assert.Equal(failures[0], failures[1]);
        Assert.Equal(20, second);
        calc.Received(2).Add(1, 2);
    }

    [VerifyTest]
    public void Received_checks_property_reads_and_writes()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Mode = Mode.Scientific;
        _ = calc.Label;

        calc.Received().Mode = Mode.Scientific;
        calc.DidNotReceive().Mode = Mode.Basic;
        _ = calc.Received().Label;
    }
}
