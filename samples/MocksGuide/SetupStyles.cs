using VerificationGlue.Mocks;
using Xunit;
using static VerificationGlue.VerifySyntax;

namespace MocksGuide;

public class SetupStyles
{
    [Fact]
    public void NSubstitute_style()
    {
        #region mocks-style-nsubstitute
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, Arg.Any<int>()).Returns(3);
        calc.Divide(Arg.Any<int>(), 0).Throws(new DivideByZeroException());
        calc.Mode.Returns(Mode.Scientific);

        var sum = calc.Add(1, 2);

        calc.Received(1).Add(1, 2);
        calc.DidNotReceive().Reset();
        #endregion
        Verify(sum == 3);
        Verify(calc.Mode == Mode.Scientific);
        Throws<DivideByZeroException>(() => calc.Divide(4, 0));
    }

    [Fact]
    public void Typed_style()
    {
        #region mocks-style-typed
        var calc = Mock.Of<ICalculator>();
        calc.Setup().Add(1, Arg<int>.Any()).Returns(3);
        calc.Setup().Divide(Arg<int>.Any(), 0).Throws(new DivideByZeroException());
        calc.Setup().Mode.Returns(Mode.Scientific);

        var sum = calc.Add(1, 2);

        Verify(calc.Calls().Add(1, 2) == 1);
        Verify(calc.Calls().Reset() == 0);
        #endregion
        Verify(sum == 3);
        Verify(calc.Mode == Mode.Scientific);
        Throws<DivideByZeroException>(() => calc.Divide(4, 0));
    }

    [Fact]
    public void Both_styles_on_one_mock()
    {
        #region mocks-style-mixed
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2).Returns(3);
        calc.Setup().Add(5, 5).Returns(10);

        calc.Add(1, 2);
        calc.Add(5, 5);

        calc.Received().Add(5, 5);
        Verify(calc.Calls().Add(Arg<int>.Any(), Arg<int>.Any()) == 2);
        #endregion
    }
}
