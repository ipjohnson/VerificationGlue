// case: mocks-behaviour-0001
// layer: behaviour
// origin: mocks.md, Surface: Returns, Throws, async results and properties
// expect: pass

namespace VerificationGlue.Mocks.Specs.Behaviour;

public class Setup
{
    [VerifyTest]
    public void Returns_answers_matching_calls_and_leaves_others_unconfigured()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(1, 2).Returns(3);

        Assert.Equal(3, calc.Add(1, 2));
        Assert.Equal(0, calc.Add(2, 1));
    }

    [VerifyTest]
    public void The_setup_call_is_not_a_received_call()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(1, 2).Returns(3);

        Assert.Empty(((IMock)calc).Adapter.ReceivedCalls());
    }

    [VerifyTest]
    public void The_last_setup_for_a_call_wins()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(1, 2).Returns(3);
        calc.Add(1, 2).Returns(4);

        Assert.Equal(4, calc.Add(1, 2));
    }

    [VerifyTest]
    public void Returns_with_several_values_answers_each_in_turn_and_repeats_the_last()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(1, 2).Returns(1, 2, 3);

        Assert.Equal([1, 2, 3, 3], [calc.Add(1, 2), calc.Add(1, 2), calc.Add(1, 2), calc.Add(1, 2)]);
    }

    [VerifyTest]
    public void A_factory_reads_the_arguments_and_sets_an_out_argument()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(call => call.ArgAt<int>(0) * call.ArgAt<int>(1));
        calc.TryParse("7", out Arg.Any<int>()).Returns(call =>
        {
            call[1] = 7;
            return true;
        });

        Assert.Equal(12, calc.Add(3, 4));
        Assert.True(calc.TryParse("7", out var value));
        Assert.Equal(7, value);
    }

    [VerifyTest]
    public void Throws_makes_matching_calls_throw()
    {
        var calc = Mock.Of<ICalculator>();
        var error = new DivideByZeroException();

        calc.Divide(Arg.Any<int>(), 0).Throws(error);

        Assert.Same(error, Assert.Throws<DivideByZeroException>(() => calc.Divide(5, 0)));
        Assert.Equal(0, calc.Divide(5, 1));
    }

    [VerifyTest]
    public async Task Returns_on_a_task_wraps_the_value()
    {
        var calc = Mock.Of<ICalculator>();

        calc.ComputeAsync(5).Returns(25);
        calc.NameAsync().Returns("scientific");

        Assert.Equal(25, await calc.ComputeAsync(5));
        Assert.Equal("scientific", await calc.NameAsync());
        Assert.Equal(0, await calc.ComputeAsync(6));
    }

    [VerifyTest]
    public void Properties_indexers_and_generic_methods_take_setups()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Mode.Returns(Mode.Scientific);
        calc[2].Returns(20);
        calc.Echo("a").Returns("b");

        Assert.Equal(Mode.Scientific, calc.Mode);
        Assert.Equal(20, calc[2]);
        Assert.Equal(0, calc[3]);
        Assert.Equal("b", calc.Echo("a"));
        Assert.Equal(0, calc.Echo(1));
    }

    [VerifyTest]
    public void A_set_property_returns_the_value_it_was_set_to()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Label = "total";

        Assert.Equal("total", calc.Label);
    }

    [VerifyTest]
    public void Two_mocks_of_one_interface_share_nothing()
    {
        var first = Mock.Of<ICalculator>();
        var second = Mock.Of<ICalculator>();

        first.Add(1, 2).Returns(3);
        first.Reset();

        Assert.Equal(0, second.Add(1, 2));
        second.DidNotReceive().Reset();
    }
}
