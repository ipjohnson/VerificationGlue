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
    public void Arguments_that_are_equal_return_the_same_mock_whether_or_not_they_are_the_same_object()
    {
        var history = Mock.Of<ICalculator>().History;
        var text = new string('a', 3);

        Assert.NotSame("aaa", text);
        Assert.Same(history.Search("aaa"), history.Search(text));
        Assert.NotSame(history.Search("aaa"), history.Search("aab"));
        Assert.Same(history.Range([1, 2]), history.Range([1, 2]));
        Assert.NotSame(history.Range([1, 2]), history.Range([2, 1]));
    }

    [VerifyTest]
    public void A_new_setup_on_the_parent_replaces_the_recursive_mock()
    {
        var calc = Mock.Of<ICalculator>();
        var recursive = calc.History;
        recursive.Last.Returns(5);
        var replacement = Mock.Of<ICalculator>().History;
        var typedReplacement = Mock.Of<ICalculator>().History;

        calc.History.Returns(replacement);
        var afterNSubstituteStyle = calc.History;
        calc.Setup().History.Returns(typedReplacement);

        Assert.Same(replacement, afterNSubstituteStyle);
        Assert.Equal(0, afterNSubstituteStyle.Last);
        Assert.Same(typedReplacement, calc.History);
        Assert.Equal(5, recursive.Last);
    }

    // Found by the step 3 sequences of docs/design/hardening.md.
    [VerifyTest]
    public void A_callback_that_writes_an_argument_does_not_change_which_mock_the_arguments_get()
    {
        var history = Mock.Of<ICalculator>().History;
        var entry = history.Find(2);

        history.Setup().Find(2).Does(info => info[0] = 104);

        Assert.Same(entry, history.Find(2));
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
