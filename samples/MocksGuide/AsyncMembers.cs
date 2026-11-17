using VerificationGlue.Mocks;
using Xunit;
using static VerificationGlue.VerifySyntax;

namespace MocksGuide;

public class AsyncMembers
{
    [Fact]
    public async Task Returns_wraps_the_value_in_a_task()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-async-returns
        calc.ComputeAsync(5).Returns(25);
        calc.NameAsync().Returns("casio");

        Verify(await calc.ComputeAsync(5) == 25);
        Verify(await calc.NameAsync() == "casio");
        #endregion
    }

    [Fact]
    public async Task Typed_setup_wraps_the_value_in_a_task()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-async-typed
        calc.Setup().ComputeAsync(5).Returns(25);

        Verify(await calc.ComputeAsync(5) == 25);
        #endregion
    }

    [Fact]
    public async Task Unconfigured_members_return_completed_tasks()
    {
        #region mocks-async-auto-values
        var calc = Mock.Of<ICalculator>();

        Verify(calc.SaveAsync().IsCompletedSuccessfully);
        Verify(await calc.ComputeAsync(1) == 0);
        Verify(await calc.NameAsync() == "");
        #endregion
    }

    [Fact]
    public async Task Faulted_tasks_and_throws()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-async-faulted
        calc.ComputeAsync(0).Returns(Task.FromException<int>(new TimeoutException()));
        calc.ComputeAsync(-1).Throws(new ArgumentOutOfRangeException());

        await ThrowsAsync<TimeoutException>(() => calc.ComputeAsync(0));
        Throws<ArgumentOutOfRangeException>(() => { _ = calc.ComputeAsync(-1); });
        #endregion
    }

    [Fact]
    public async Task A_setup_can_await_its_value()
    {
        var calc = Mock.Of<ICalculator>();

        #region mocks-async-await-in-setup
        calc.Add(1, 2).Returns(await LoadExpectedSumAsync());

        Verify(calc.Add(1, 2) == 3);
        #endregion
    }

    private static async Task<int> LoadExpectedSumAsync()
    {
        await Task.Yield();
        return 3;
    }
}
