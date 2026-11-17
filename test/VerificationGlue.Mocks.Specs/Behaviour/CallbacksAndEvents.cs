// case: mocks-behaviour-0004
// layer: behaviour
// origin: mocks.md, Surface: When/Do and Raise.Event
// expect: pass

namespace VerificationGlue.Mocks.Specs.Behaviour;

public class CallbacksAndEvents
{
    [Fact]
    public void When_Do_runs_the_callback_on_each_matching_call()
    {
        var calc = Mock.Of<ICalculator>();
        var resets = 0;
        var added = new List<int>();

        calc.When(c => c.Reset()).Do(_ => resets++);
        calc.When(c => c.Add(Arg.Any<int>(), 1)).Do(call => added.Add(call.ArgAt<int>(0)));

        calc.Reset();
        calc.Reset();
        calc.Add(4, 1);
        calc.Add(5, 2);

        Assert.Equal(2, resets);
        Assert.Equal([4], added);
    }

    [Fact]
    public void When_does_not_record_the_selecting_call()
    {
        var calc = Mock.Of<ICalculator>();

        calc.When(c => c.Reset()).Do(_ => { });

        calc.DidNotReceive().Reset();
    }

    [Fact]
    public void When_Throw_makes_a_void_member_throw()
    {
        var calc = Mock.Of<ICalculator>();

        calc.When(c => c.Reset()).Throw(new InvalidOperationException("busy"));

        Assert.Equal("busy", Assert.Throws<InvalidOperationException>(calc.Reset).Message);
    }

    [Fact]
    public void Raise_Event_invokes_the_handlers_with_the_arguments()
    {
        var calc = Mock.Of<ICalculator>();
        var seen = new List<(object? Sender, int Value)>();
        calc.Changed += (sender, value) => seen.Add((sender, value));

        calc.Changed += Raise.Event<EventHandler<int>>(calc, 5);

        Assert.Equal([((object?)calc, 5)], seen);
    }

    [Fact]
    public void Raise_Event_with_no_handler_adds_no_handler_and_records_no_call()
    {
        var calc = Mock.Of<ICalculator>();

        calc.Changed += Raise.Event<EventHandler<int>>(calc, 5);

        Assert.Empty(((IMock)calc).Adapter.ReceivedCalls());
    }
}
