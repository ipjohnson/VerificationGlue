using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

#region tests-default-group
[assembly: ScheduleGroup("default", MaxParallel = 0)]
#endregion

#region tests-scheduler-attribute
[assembly: Scheduler<TestsScheduling.ShardScheduler>]
#endregion

namespace TestsScheduling;

/// <summary>A database that counts the queries running at once.</summary>
public static class OrderDatabase
{
    private static int s_open;

    public static int Open => Volatile.Read(ref s_open);

    /// <summary>Runs a query and returns how many queries were running with it, itself included.</summary>
    public static async Task<int> QueryAsync()
    {
        var open = Interlocked.Increment(ref s_open);
        try
        {
            await Task.Delay(100);
            return open;
        }
        finally
        {
            Interlocked.Decrement(ref s_open);
        }
    }
}

/// <summary>A payment sandbox that one test at a time may use.</summary>
public static class PaymentsSandbox
{
    private static int s_users;

    /// <summary>Uses the sandbox and returns true when no other test used it at the same time.</summary>
    public static async Task<bool> UseAloneAsync()
    {
        var alone = Interlocked.Increment(ref s_users) == 1;
        try
        {
            await Task.Delay(100);
            return alone;
        }
        finally
        {
            Interlocked.Decrement(ref s_users);
        }
    }
}

#region tests-schedule-group
[ScheduleGroup("database", MaxParallel = 2)]
public partial class OrderRepositoryTests
{
    [VerifyTest]
    public async Task Saves_an_order() => Verify(await OrderDatabase.QueryAsync() <= 2);

    [VerifyTest]
    public async Task Loads_an_order() => Verify(await OrderDatabase.QueryAsync() <= 2);

    [VerifyTest]
    public async Task Deletes_an_order() => Verify(await OrderDatabase.QueryAsync() <= 2);
}
#endregion

#region tests-resource
public partial class PaymentTests
{
    [VerifyTest]
    [Resource("payments-sandbox")]
    public async Task Charges_a_card() => Verify(await PaymentsSandbox.UseAloneAsync());

    [VerifyTest]
    [Resource("payments-sandbox")]
    public async Task Refunds_a_card() => Verify(await PaymentsSandbox.UseAloneAsync());
}
#endregion

#region tests-run-after
[ScheduleGroup("reports")]
[RunAfter("database")]
public partial class DailyReportTests
{
    [VerifyTest]
    public void Starts_after_the_database_tests() => Verify(OrderDatabase.Open == 0);
}
#endregion

#region tests-shard-scheduler
/// <summary>
/// Runs every n-th test when <c>VG_SHARD</c> is set to "index/count", such as "0/2" on one machine and
/// "1/2" on another, and leaves the rest to the default scheduler's rules.
/// </summary>
public sealed class ShardScheduler : ITestScheduler
{
    private readonly DefaultTestScheduler _default = new();

    public ValueTask RunAsync(IReadOnlyList<ScheduledTest> tests, Func<ScheduledTest, ValueTask> run, CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("VG_SHARD")?.Split('/') is not [var index, var count])
        {
            return _default.RunAsync(tests, run, cancellationToken);
        }

        var shard = tests.Where((test, position) => position % int.Parse(count) == int.Parse(index)).ToArray();
        return _default.RunAsync(shard, run, cancellationToken);
    }
}
#endregion
