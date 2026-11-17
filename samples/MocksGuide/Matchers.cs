using VerificationGlue.Mocks;
using Xunit;
using static VerificationGlue.VerifySyntax;

namespace MocksGuide;

public class Matchers
{
    [Fact]
    public void Any_and_Is()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-matchers-any-is
        calc.Add(Arg.Any<int>(), Arg.Is<int>(x => x > 10)).Returns(-1);
        calc.Add(Arg.Is(7), 7).Returns(49);

        Verify(calc.Add(3, 11) == -1);
        Verify(calc.Add(3, 10) == 0);
        Verify(calc.Add(7, 7) == 49);
        #endregion
    }

    [Fact]
    public void A_matcher_belongs_to_the_argument_it_is_written_at()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-matchers-position
        calc.Add(0, Arg.Any<int>()).Returns(5);

        Verify(calc.Add(0, 9) == 5);
        Verify(calc.Add(9, 0) == 0);
        #endregion
    }

    [Fact]
    public void Typed_matchers()
    {
        var calc = Mock.Of<ICalculator>();
        var clock = Fakes.Fresh<IClock>();

        #region mocks-matchers-typed
        calc.Setup().Add(Arg<int>.Any(), Arg<int>.Is(x => x > 10)).Returns(-1);
        calc.Setup().Since(Arg<IClock>.Is(clock)).Returns(TimeSpan.FromMinutes(5));

        Verify(calc.Add(3, 11) == -1);
        Verify(calc.Since(clock) == TimeSpan.FromMinutes(5));
        #endregion
    }

    [Fact]
    public void Out_arguments()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-matchers-out
        calc.TryParse("7", out Arg.Any<int>()).Returns(call =>
        {
            call[1] = 7;
            return true;
        });

        var parsed = calc.TryParse("7", out var value);

        Verify(parsed);
        Verify(value == 7);
        #endregion
    }

    [Fact]
    public void A_response_can_read_the_arguments()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-matchers-call-info
        calc.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(call => call.ArgAt<int>(0) * call.ArgAt<int>(1));

        Verify(calc.Add(3, 4) == 12);
        #endregion
    }

    [Fact]
    public void Matchers_in_checks()
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2);

        #region mocks-matchers-received
        calc.Received().Add(Arg.Is<int>(x => x > 0), 2);
        Verify(calc.Calls().Add(Arg<int>.Is(x => x > 0), 2) == 1);
        #endregion
    }
}
