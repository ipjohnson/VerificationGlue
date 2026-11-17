// case: mocks-behaviour-0008
// layer: behaviour
// origin: mocks.md, Typed setup surface: Setup(), Calls(), Arg<T> and the call count
// expect: pass

using static VerificationGlue.VerifySyntax;

namespace VerificationGlue.Mocks.Specs.Behaviour;

public partial class TypedSurface
{
    [Fact]
    public void Setup_with_a_value_and_Arg_Any_configures_the_mock()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Setup().Add(1, Arg<int>.Any()).Returns(3);

        Assert.Equal(3, calc.Add(1, 2));
        Assert.Equal(3, calc.Add(1, 0));
        Assert.Equal(0, calc.Add(2, 2));
    }

    [Fact]
    public void Arg_Is_matches_by_predicate_and_by_value()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Setup().Add(Arg<int>.Is(a => a > 10), Arg<int>.Is(5)).Returns(-1);

        Assert.Equal(-1, calc.Add(11, 5));
        Assert.Equal(0, calc.Add(10, 5));
        Assert.Equal(0, calc.Add(11, 6));
    }

    [Fact]
    public void A_default_argument_matches_the_default_value()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Setup().Add(default, 1).Returns(7);

        Assert.Equal(7, calc.Add(0, 1));
        Assert.Equal(0, calc.Add(2, 1));
    }

    [Fact]
    public void Setup_does_not_record_a_call()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Setup().Add(1, 2).Returns(3);
        calc.Setup().Reset().Does(_ => { });

        Assert.Empty(((IMock)calc).Adapter.ReceivedCalls());
    }

    [Fact]
    public void Throws_Does_and_sequences_apply_to_matching_calls()
    {
        var calc = Mock.Of<ICalculator>();
        var resets = 0;
        var seen = new List<int>();

        calc.Setup().Divide(Arg<int>.Any(), 0).Throws(new DivideByZeroException());
        calc.Setup().Reset().Does(_ => resets++);
        calc.Setup().Add(Arg<int>.Any(), Arg<int>.Any()).Does(info => seen.Add(info.ArgAt<int>(0))).Returns(1, 2);

        Assert.Throws<DivideByZeroException>(() => calc.Divide(4, 0));
        calc.Reset();
        calc.Reset();

        Assert.Equal(2, resets);
        Assert.Equal([1, 2, 2], new[] { calc.Add(7, 0), calc.Add(8, 0), calc.Add(9, 0) });
        Assert.Equal([7, 8, 9], seen);
    }

    [Fact]
    public void A_void_member_can_throw()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Setup().Reset().Throws(new InvalidOperationException("reset"));

        Assert.Equal("reset", Assert.Throws<InvalidOperationException>(calc.Reset).Message);
    }

    [Fact]
    public void Properties_indexers_and_out_parameters_can_be_set_up()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Setup().Mode.Returns(Mode.Scientific);
        calc.Setup()[Arg<int>.Is(i => i > 0)].Returns(42);
        calc.Setup().TryParse("7", Arg<int>.Any()).Returns(info =>
        {
            info[1] = 7;
            return true;
        });

        Assert.Equal(Mode.Scientific, calc.Mode);
        Assert.Equal(42, calc[1]);
        Assert.Equal(0, calc[0]);
        Assert.True(calc.TryParse("7", out var value));
        Assert.Equal(7, value);
    }

    [Fact]
    public async Task Async_members_take_the_result_value()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Setup().ComputeAsync(5).Returns(25);
        calc.Setup().NameAsync().Returns("calc");

        Assert.Equal(25, await calc.ComputeAsync(5));
        Assert.Equal("calc", await calc.NameAsync());
    }

    [Fact]
    public void A_generic_method_matches_its_type_argument()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Setup().Echo(Arg<int>.Any()).Returns(9);
        calc.Setup().Echo<string>("a").Returns("b");

        Assert.Equal(9, calc.Echo(1));
        Assert.Equal("b", calc.Echo("a"));
        Assert.Equal("", calc.Echo("c"));
        Assert.True(calc.Calls().Echo(Arg<int>.Any()) == 1);
        Assert.True(calc.Calls().Echo(Arg<string>.Any()) == 2);
    }

    [Fact]
    public void An_interface_typed_parameter_matches_an_exact_value_through_Arg_Is()
    {
        var calc = Mock.Of<ICalculator>();
        var clock = Mock.Of<IClock>();

        calc.Setup().Since(Arg<IClock>.Is(clock)).Returns(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(1), calc.Since(clock));
        Assert.Equal(TimeSpan.Zero, calc.Since(Mock.Of<IClock>()));
    }

    [Fact]
    public void Calls_counts_the_matching_calls()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Add(1, 2);
        calc.Add(1, 3);
        _ = calc.Mode;

        Verify(calc.Calls().Add(1, 2) == 1);
        Verify(calc.Calls().Add(1, Arg<int>.Any()) == 2);
        Verify(calc.Calls().Add(Arg<int>.Is(a => a > 1), Arg<int>.Any()) == 0);
        Verify(calc.Calls().Reset() == 0);
        Verify(calc.Calls().Mode == 1);
        Verify(calc.Calls().Add(1, 2) >= 1 && calc.Calls().Add(1, 2) < 2 && 1 == calc.Calls().Add(1, 2));
    }

    [Fact]
    public void Both_styles_drive_the_same_mock()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Setup().Add(1, 2).Returns(3);
        calc.Divide(Arg.Any<int>(), 0).Returns(-1);

        Assert.Equal(3, calc.Add(1, 2));
        Assert.Equal(-1, calc.Divide(5, 0));
        calc.Received().Add(1, 2);
        Verify(calc.Calls().Divide(5, 0) == 1);
    }

    [Fact]
    public void The_call_count_lists_up_to_10_calls_to_the_member()
    {
        var calc = Mock.Of<ICalculator>();

        Assert.Equal("0 (Add received no calls)", calc.Calls().Add(1, 2).ToString());

        calc.Reset();
        calc.Add(3, 4);
        Assert.Equal("0 (Add received 1 call: Add(3, 4))", calc.Calls().Add(1, 2).ToString());

        for (var i = 0; i < 11; i++)
        {
            calc.Add(i, i);
        }

        Assert.Equal(
            "0 (Add received 12 calls: Add(3, 4), Add(0, 0), Add(1, 1), Add(2, 2), Add(3, 3), Add(4, 4), Add(5, 5), Add(6, 6), Add(7, 7), Add(8, 8), and 2 more)",
            calc.Calls().Add(1, 2).ToString());
    }

    [Fact]
    public void A_failed_Verify_lists_the_calls_the_member_received()
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(3, 4);
        calc.Add(5, 6);

        var error = Assert.Throws<VerifyException>(() => VerifyOneAdd(calc));

        Assert.Equal(
            """
            Verify failed: calc.Calls().Add(1, 2) == 1
              calc.Calls().Add(1, 2) → 0 (Add received 2 calls: Add(3, 4), Add(5, 6))
              calc                   → VerificationGlue.Mocks.Generated.VerificationGlue_Mocks_Specs_Behaviour_ICalculator_Mock
              at Behaviour/TypedSurface.cs:221
            """.ReplaceLineEndings("\n"),
            error.Message);
    }

    [Verify]
    private static void VerifyOneAdd(ICalculator calc)
    {
        Verify(calc.Calls().Add(1, 2) == 1);
    }
}
