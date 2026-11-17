namespace NSubstituteComparison.Cases;

#pragma warning disable VGM0005 // A case moves a setup into a child task on purpose.

[Area("Parallel use", 6)]
public static class Parallel
{
    [Case("200 parallel setups on one mock")]
    public static async Task Parallel_setups_on_one_mock(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => calc.Add(i, i).Returns(i * 10))));

        observe.Record("calls that return their setup", Enumerable.Range(0, 200).Count(i => calc.Add(i, i) == i * 10));
    }

    [Case("200 parallel setups whose values come from an await")]
    [Difference(
        "VerificationGlue.Mocks binds every setup to its own call, because the last call follows each setup across its await. " +
        "Under NSubstitute every setup resumes on a new thread with no last call, and fails.")]
    public static async Task Parallel_setups_with_an_await(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var failed = 0;

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(async () =>
        {
            try
            {
                calc.Add(i, i).Returns(await AsyncResults.LoadOnAnotherThread(i * 10));
            }
            catch (Exception)
            {
                Interlocked.Increment(ref failed);
            }
        })));

        observe.Record("setups that threw", failed);
        observe.Record("calls that return their setup", Enumerable.Range(0, 200).Count(i => calc.Add(i, i) == i * 10));
    }

    [Case("1,000 parallel calls are all recorded")]
    public static async Task Parallel_calls_are_counted(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();

        await Task.WhenAll(Enumerable.Range(0, 1000).Select(_ => Task.Run(() => calc.Add(1, 2))));

        observe.Try("Received(1000).Add(1, 2)", () => calc.Received(1000).Add(1, 2));
    }

    [Case("A call from another thread while Received waits for its arguments")]
    public static async Task Background_call_during_received(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.Add(1, 2);
        using var start = new SemaphoreSlim(0);
        using var done = new SemaphoreSlim(0);
        var worker = Task.Run(async () =>
        {
            await start.WaitAsync();
            try
            {
                calc.Add(5, 5);
            }
            catch (Exception exception)
            {
                observe.Record("background Add(5, 5)", "throws " + exception.GetType().Name);
            }

            done.Release();
        });

        observe.Try("Received().Add(1, 2)", () => calc.Received().Add(1, ArgumentAfterABackgroundCall(start, done)));
        await worker;

        observe.Try("Received(2).Add(1, 2)", () => calc.Received(2).Add(1, 2));
        observe.Try("Received(1).Add(5, 5)", () => calc.Received(1).Add(5, 5));
    }

    [Case("A setup finished in a child task")]
    [Difference(
        "A child task starts with its parent's last call in VerificationGlue.Mocks, so the setup binds. " +
        "NSubstitute keeps the last call per thread, so the child task has none.")]
    public static async Task Setup_finished_in_a_child_task(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var call = calc.Add(1, 2);

        await observe.TryAsync("call.Returns(3) in a child task", () => Task.Factory.StartNew(() => call.Returns(3), TaskCreationOptions.LongRunning));
        observe.Record("Add(1, 2)", calc.Add(1, 2));
    }

    /// <summary>Lets the worker make its call while the check waits, then gives the checked call its argument.</summary>
    private static int ArgumentAfterABackgroundCall(SemaphoreSlim start, SemaphoreSlim done)
    {
        start.Release();
        done.Wait();
        return 2;
    }
}
