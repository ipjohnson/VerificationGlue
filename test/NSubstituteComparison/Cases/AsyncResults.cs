using System.Runtime.CompilerServices;

namespace NSubstituteComparison.Cases;

[Area("Async results", 4)]
public static class AsyncResults
{
    [Case("An unconfigured Task<int> is complete with 0")]
    public static async Task Unconfigured_task_of_int(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var task = calc.ComputeAsync(1);

        observe.Record("IsCompletedSuccessfully", task.IsCompletedSuccessfully);
        observe.Record("result", await task);
    }

    [Case("An unconfigured Task<string> is complete with an empty string")]
    public static async Task Unconfigured_task_of_string(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        var task = calc.NameAsync();

        observe.Record("IsCompletedSuccessfully", task.IsCompletedSuccessfully);
        observe.Record("result", await task);
    }

    [Case("An unconfigured Task and ValueTask<int> are complete")]
    public static async Task Unconfigured_task_and_value_task(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();

        observe.Record("SaveAsync().IsCompletedSuccessfully", calc.SaveAsync().IsCompletedSuccessfully);
        var count = calc.CountAsync();
        observe.Record("CountAsync().IsCompletedSuccessfully", count.IsCompletedSuccessfully);
        observe.Record("CountAsync() result", await count);
    }

    [Case("Returns with a value on a Task<int> member")]
    public static async Task Returns_a_value_for_a_task(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.ComputeAsync(5).Returns(25);

        observe.Record("ComputeAsync(5)", await calc.ComputeAsync(5));
        observe.Record("ComputeAsync(6)", await calc.ComputeAsync(6));
    }

    [Case("Returns with a task on a Task<int> member")]
    public static async Task Returns_a_task(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.ComputeAsync(5).Returns(Task.FromResult(25));

        observe.Record("ComputeAsync(5)", await calc.ComputeAsync(5));
    }

    [Case("Returns with a faulted task")]
    public static async Task Returns_a_faulted_task(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.ComputeAsync(0).Returns(Task.FromException<int>(new InvalidOperationException()));

        var task = calc.ComputeAsync(0);
        observe.Record("IsFaulted", task.IsFaulted);
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            observe.Record("await", exception.GetType().Name);
        }
    }

    [Case("Throws on a Task<int> member throws when the member is called")]
    public static void Throws_on_an_async_member(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();
        calc.ComputeAsync(0).Throws(new InvalidOperationException());

        observe.Try("ComputeAsync(0)", () => calc.ComputeAsync(0));
    }

    [Case("A setup whose value comes from an await")]
    [Difference(
        "VerificationGlue.Mocks keeps the last call in an AsyncLocal, so it follows the setup across the await. " +
        "NSubstitute keeps it per thread, so a setup whose await resumes on another thread loses its call.")]
    public static async Task Returns_with_an_await(Observations observe)
    {
        var calc = Mock.Of<ICalculator>();

        await observe.TryAsync("Add(1, 2).Returns(await LoadOnAnotherThread(10))", async () => calc.Add(1, 2).Returns(await LoadOnAnotherThread(10)));

        observe.Record("Add(1, 2)", calc.Add(1, 2));
    }

    /// <summary>Gives <paramref name="value"/> after the await, which always resumes on a new thread.</summary>
    internal static NewThread LoadOnAnotherThread(int value) => new(value);

    internal readonly struct NewThread(int value) : INotifyCompletion
    {
        public bool IsCompleted => false;

        public NewThread GetAwaiter() => this;

        public int GetResult() => value;

        public void OnCompleted(Action continuation) => new Thread(() => continuation()).Start();
    }
}
