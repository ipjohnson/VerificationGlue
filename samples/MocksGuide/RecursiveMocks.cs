using VerificationGlue.Mocks;
using Xunit;
using static VerificationGlue.VerifySyntax;

namespace MocksGuide;

public class RecursiveMocks
{
    [Fact]
    public void A_member_of_an_unconfigured_member_can_be_set_up()
    {
        #region mocks-recursive
        var calc = Mock.Of<ICalculator>();

        calc.History.Last.Returns(5);

        Verify(calc.History.Last == 5);
        Verify(ReferenceEquals(calc.History, calc.History));
        #endregion
    }

    [Fact]
    public void Each_set_of_arguments_gets_its_own_mock()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-recursive-arguments
        var first = calc.History.Find(1);

        Verify(ReferenceEquals(first, calc.History.Find(1)));
        Verify(!ReferenceEquals(first, calc.History.Find(2)));
        #endregion
    }

    [Fact]
    public void The_walk_stops_at_the_depth()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-recursive-depth
        var entry = calc.History.Find(1);

        Verify(entry is not null);
        Verify(entry.Next is null);
        #endregion
    }
}
