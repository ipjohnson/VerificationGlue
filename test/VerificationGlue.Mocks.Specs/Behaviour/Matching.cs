// case: mocks-behaviour-0002
// layer: behaviour
// origin: mocks.md, Matcher positions: NSubstitute binds Arg.Any in Add(0, Arg.Any<int>()) to the first argument
// expect: pass

namespace VerificationGlue.Mocks.Specs.Behaviour;

public class Matching
{
    [Fact]
    public void Any_matches_every_value()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(Arg.Any<int>(), 2).Returns(5);

        Assert.Equal(5, calc.Add(-4, 2));
        Assert.Equal(5, calc.Add(100, 2));
        Assert.Equal(0, calc.Add(100, 3));
    }

    [Fact]
    public void Is_matches_values_for_which_the_predicate_holds()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(Arg.Any<int>(), Arg.Is<int>(x => x > 10)).Returns(-1);

        Assert.Equal(-1, calc.Add(1, 11));
        Assert.Equal(0, calc.Add(1, 10));
    }

    [Fact]
    public void Is_with_a_value_matches_equal_values()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Echo(Arg.Is("a")).Returns("matched");

        Assert.Equal("matched", calc.Echo("a"));
        Assert.Null(calc.Echo("b"));
    }

    [Fact]
    public void A_matcher_binds_to_the_argument_it_is_written_in_when_another_argument_holds_the_default()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(0, Arg.Any<int>()).Returns(9);

        Assert.Equal(9, calc.Add(0, 5));
        Assert.Equal(0, calc.Add(1, 5));
    }

    [Fact]
    public void A_matcher_on_an_indexer_argument_matches()
    {
        var calc = Mock.Of<ICalculator>();

        calc[Arg.Is<int>(i => i < 0)].Returns(-1);

        Assert.Equal(-1, calc[-3]);
        Assert.Equal(0, calc[3]);
    }
}
