using VerificationGlue.Mocks;

namespace Hardening.Specs.Mocks;

#pragma warning disable VGM0002 // The cases abandon matchers on purpose.
#pragma warning disable VGM0005 // The cases call Returns with no mock call on purpose.

/// <summary>
/// The misuse set of docs/design/hardening.md, step 3: deliberate misuse that the generated
/// sequences never make. Each case states the failure it expects and what the next operation in the
/// same flow does.
/// </summary>
[Trait("Step", "3")]
public class MockMisuseSpecs
{
    [VerifyTest]
    public void Returns_with_no_mock_call_before_it_fails_and_the_next_setup_works()
    {
        var service = Mock.Of<IService>();

        var error = Assert.Throws<MockSetupException>(() => 5.Returns(6));
        service.Get(1, 2).Returns(3);

        Assert.Contains("no mock call came before it", error.Message);
        Assert.Equal(3, service.Get(1, 2));
        Assert.Equal(["Get(1, 2)"], Calls(service));
    }

    [VerifyTest]
    public void A_second_Returns_for_one_call_fails_and_the_first_stays()
    {
        var service = Mock.Of<IService>();

        service.Get(1, 2).Returns(3);
        Assert.Throws<MockSetupException>(() => 0.Returns(4));

        Assert.Equal(3, service.Get(1, 2));
    }

    [VerifyTest]
    public void A_matcher_left_in_the_flow_that_fits_no_argument_fails_the_next_call_and_is_cleared()
    {
        var service = Mock.Of<IService>();

        Arg.Any<string>();
        var error = Assert.Throws<AmbiguousArgumentsException>(() => service.Get(1, 2));

        Assert.Contains("Arg.Any<String>()", error.Message);
        Assert.Empty(Calls(service));
        Assert.Equal(0, service.Get(1, 2));
        Assert.Equal(["Get(1, 2)"], Calls(service));
    }

    [VerifyTest]
    public void A_matcher_left_in_the_flow_that_fits_joins_the_next_call_at_the_first_default_argument()
    {
        var service = Mock.Of<IService>();

        Arg.Any<int>();
        service.Get(0, 5).Returns(7);

        Assert.Equal(7, service.Get(9, 5));
        Assert.Equal(0, service.Get(9, 6));
    }

    [VerifyTest]
    public void A_pending_check_with_no_call_checks_the_next_call_on_that_mock_and_not_on_another()
    {
        var service = Mock.Of<IService>();
        var other = Mock.Of<IService>();

        service.Received();
        other.Get(1, 2);
        Assert.Throws<ReceivedCallsException>(() => service.Get(1, 2));

        Assert.Equal(["Get(1, 2)"], Calls(other));
        Assert.Empty(Calls(service));
        service.Get(1, 2);
        Assert.Equal(["Get(1, 2)"], Calls(service));
    }

    [VerifyTest]
    public async Task A_pending_check_left_in_a_child_task_does_not_reach_the_parent()
    {
        var service = Mock.Of<IService>();

        await Task.Run(() => service.Received());
        var answer = service.Get(1, 2);

        Assert.Equal(0, answer);
        Assert.Equal(["Get(1, 2)"], Calls(service));
    }

    [VerifyTest]
    public void Received_with_a_negative_count_fails_and_leaves_no_pending_check()
    {
        var service = Mock.Of<IService>();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Received(-1));
        service.Get(1, 2);

        Assert.Equal(["Get(1, 2)"], Calls(service));
    }

    [VerifyTest]
    public void When_with_no_mock_call_fails_and_the_next_call_is_recorded()
    {
        var service = Mock.Of<IService>();

        var error = Assert.Throws<MockSetupException>(() => service.When(_ => { }));
        service.Get(1, 2).Returns(3);

        Assert.Contains("did not call a member", error.Message);
        Assert.Equal(3, service.Get(1, 2));
        Assert.Equal(["Get(1, 2)"], Calls(service));
    }

    // The setup call runs the earlier setup, as in NSubstitute (MOCK-010). It throws before Returns
    // runs, so its call stays recorded and stays the last call of the flow.
    [VerifyTest]
    public void A_setup_whose_call_throws_adds_no_setup_and_its_call_stays_recorded()
    {
        var service = Mock.Of<IService>();
        service.Get(Arg.Any<int>(), Arg.Any<int>()).Throws(new InvalidOperationException("earlier"));

        Assert.Throws<InvalidOperationException>(() => service.Get(1, 2).Returns(5));

        Assert.Equal(["Get(1, 2)"], Calls(service));
        0.Returns(9);
        Assert.Equal(9, service.Get(1, 2));
    }

    // Raise.Event stores its arguments in the flow until an event's add accessor takes them.
    [VerifyTest]
    public void Raise_Event_that_is_not_added_to_an_event_raises_the_next_event_added_to_instead()
    {
        var service = Mock.Of<IService>();
        var seen = new List<int>();
        EventHandler<int> first = (_, value) => seen.Add(value);
        EventHandler<int> second = (_, value) => seen.Add(-value);
        service.Changed += first;

        Raise.Event<EventHandler<int>>(service, 5);
        service.Changed += second;
        service.Changed += Raise.Event<EventHandler<int>>(service, 6);

        Assert.Equal([5, 6], seen);
        Assert.Equal(["add_Changed(System.EventHandler`1[System.Int32])"], Calls(service));
    }

    private static List<string> Calls(IService service) => [.. MockAdapter.Of(service).ReceivedCalls().Select(call => call.ToString())];
}
