// case: mocks-behaviour-0009
// layer: behaviour
// origin: mocks.md, Recursive mocks: unconfigured members return mocks up to the recursion depth
// expect: pass

namespace VerificationGlue.Mocks.Specs.Behaviour;

public class RecursiveMocks
{
    [VerifyTest]
    public void A_member_of_an_unconfigured_member_can_be_set_up()
    {
        var calc = Mock.Of<ICalculator>();

        calc.History.Last.Returns(5);

        Assert.Equal(5, calc.History.Last);
    }

    [VerifyTest]
    public void The_same_arguments_return_the_same_mock()
    {
        var history = Mock.Of<ICalculator>().History;

        Assert.Same(history.Find(1), history.Find(1));
        Assert.NotSame(history.Find(1), history.Find(2));
    }

    [VerifyTest]
    public void A_configured_member_returns_its_value_and_not_a_mock()
    {
        var calc = Mock.Of<ICalculator>();
        var history = Mock.Of<ICalculator>().History;

        calc.History.Returns(history);

        Assert.Same(history, calc.History);
    }

    [VerifyTest]
    public void The_typed_surface_configures_a_recursive_mock()
    {
        var calc = Mock.Of<ICalculator>();

        calc.History.Setup().Find(Arg<int>.Any()).Returns((IEntry)null!);

        Assert.Null(calc.History.Find(3));
    }

    [VerifyTest]
    public void A_member_beyond_the_depth_returns_null()
    {
        var entry = Mock.Of<ICalculator>().History.Find(1);

        Assert.NotNull(entry);
        Assert.Null(entry.Next);
    }

    [VerifyTest]
    public void A_check_with_Received_returns_null_and_creates_no_mock()
    {
        var calc = Mock.Of<ICalculator>();
        var history = calc.History;

        Assert.Null(calc.Received().History);
        Assert.Same(history, calc.History);
    }
}
