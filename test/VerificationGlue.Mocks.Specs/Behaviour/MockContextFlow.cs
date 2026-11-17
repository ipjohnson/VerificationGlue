// case: mocks-behaviour-0006
// layer: behaviour
// origin: mocks.md, The adapter and the last call: the thread-static context of the spike bound 199 of 200 Returns(await ...) setups wrongly
// expect: pass

namespace VerificationGlue.Mocks.Specs.Behaviour;

public class MockContextFlow
{
    [Fact]
    public async Task Returns_with_an_await_binds_to_its_own_call_in_200_parallel_setups()
    {
        var calc = Mock.Of<ICalculator>();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(async () => calc.Add(i, i).Returns(await LoadAsync(i)))));

        Assert.Empty(((IMock)calc).Adapter.ReceivedCalls());
        Assert.All(Enumerable.Range(0, 200), i => Assert.Equal(i * 10, calc.Add(i, i)));
    }

    [Fact]
    public async Task A_pending_check_and_a_matcher_flow_across_an_await_in_the_arguments()
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(7, 2);

        calc.Received().Add(Arg.Any<int>(), await Task.Run(() => 2));

        Assert.Single(((IMock)calc).Adapter.ReceivedCalls());
    }

    [Fact]
    public async Task A_background_call_during_Received_is_recorded_and_not_checked()
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2);
        using var start = new SemaphoreSlim(0);
        using var done = new SemaphoreSlim(0);
        var worker = Task.Run(async () =>
        {
            await start.WaitAsync(TestContext.Current.CancellationToken);
            calc.Add(5, 5);
            done.Release();
        }, TestContext.Current.CancellationToken);

        calc.Received().Add(1, CallInTheBackground(start, done));
        await worker;

        Assert.Equal(["Add(1, 2)", "Add(5, 5)"], ((IMock)calc).Adapter.ReceivedCalls().Select(call => call.ToString()));
    }

#pragma warning disable VGM0005 // The receiver is a local on purpose: the case moves the setup into a child task.
    [Fact]
    public async Task A_child_task_starts_with_its_parents_last_call_and_its_own_calls_stay_in_the_child()
    {
        var calc = Mock.Of<ICalculator>();
        var call = calc.Add(1, 2);

        await Task.Run(() => call.Returns(3), TestContext.Current.CancellationToken);
        Assert.Equal(3, calc.Add(1, 2));

        var again = calc.Add(1, 2);
        await Task.Run(() => calc.Add(9, 9));
        again.Returns(4);

        Assert.Equal(4, calc.Add(1, 2));
        Assert.Equal(0, calc.Add(9, 9));
    }

    [Fact]
    public void Returns_with_no_mock_call_before_it_fails()
    {
        var error = Assert.Throws<MockSetupException>(() => 5.Returns(6));

        Assert.Contains("no mock call came before it", error.Message);
    }
#pragma warning restore VGM0005

#pragma warning disable VGM0002 // The cases abandon a matcher on purpose.
    [Fact]
    public async Task A_matcher_abandoned_in_a_child_task_does_not_reach_the_parent()
    {
        var calc = Mock.Of<ICalculator>();

        await Task.Run(() => Arg.Any<int>());
        calc.Add(0, 5).Returns(3);

        Assert.Equal(3, calc.Add(0, 5));
        Assert.Equal(0, calc.Add(7, 5));
    }

    [Fact]
    public void A_matcher_abandoned_in_the_same_flow_fails_the_next_call_it_cannot_fit()
    {
        var calc = Mock.Of<ICalculator>();

        Arg.Any<string>();
        var error = Assert.Throws<AmbiguousArgumentsException>(() => calc.Add(1, 2));

        Assert.Contains("Arg.Any<String>()", error.Message);
        Assert.Equal(0, calc.Add(1, 2));
    }
#pragma warning restore VGM0002

    private static async Task<int> LoadAsync(int value)
    {
        await Task.Yield();
        return value * 10;
    }

    /// <summary>Lets the worker make its call while the check is pending, then gives the checked call its argument.</summary>
    private static int CallInTheBackground(SemaphoreSlim start, SemaphoreSlim done)
    {
        start.Release();
        done.Wait();
        return 2;
    }
}
