namespace NSubstituteComparison.Cases;

[Area("Response sequences", 3)]
public static class Sequences
{
    [Case("Returns with several values gives each in turn and then repeats the last")]
    public static void Values_in_turn(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2).Returns(1, 2, 3);

        for (var i = 1; i <= 4; i++)
        {
            observe.Record($"call {i}", calc.Add(1, 2));
        }
    }

    [Case("A sequence is shared by every call it matches")]
    public static void Sequence_shared_by_matching_calls(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(10, 20);

        observe.Record("Add(1, 1)", calc.Add(1, 1));
        observe.Record("Add(2, 2)", calc.Add(2, 2));
        observe.Record("Add(1, 1) again", calc.Add(1, 1));
    }

    [Case("A new setup replaces a sequence part way through")]
    public static void New_setup_replaces_a_sequence(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2).Returns(1, 2);
        observe.Record("call 1", calc.Add(1, 2));

        calc.Add(1, 2).Returns(5);
        observe.Record("call 2", calc.Add(1, 2));
    }

    [Case("A factory reads the arguments")]
    public static void Factory_reads_arguments(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(x => x.ArgAt<int>(0) * 10 + x.ArgAt<int>(1));

        observe.Record("Add(1, 2)", calc.Add(1, 2));
        observe.Record("Add(3, 4)", calc.Add(3, 4));
    }

    [Case("A factory runs on each call")]
    public static void Factory_runs_each_call(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var runs = 0;
        calc.Add(1, 2).Returns(_ => ++runs);

        observe.Record("call 1", calc.Add(1, 2));
        observe.Record("call 2", calc.Add(1, 2));
        observe.Record("runs", runs);
    }

    [Case("A factory that throws")]
    public static void Factory_throws(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2).Returns(_ => throw new InvalidOperationException());

        observe.TryValue("Add(1, 2)", () => calc.Add(1, 2));
        observe.TryValue("Add(2, 2)", () => calc.Add(2, 2));
    }
}
