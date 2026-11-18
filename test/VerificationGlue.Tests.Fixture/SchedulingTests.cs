using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

/// <summary>
/// Logs "start {group} {test}" and "end {group} {test}" around a short wait, so the specs can tell
/// from the log which tests ran at the same time.
/// </summary>
public static class Timed
{
    public static async Task RunAsync(string group, string test)
    {
        Log.Write($"start {group} {test}");
        await Task.Delay(300);
        Log.Write($"end {group} {test}");
    }
}

[ScheduleGroup("database", MaxParallel = 2)]
public partial class DatabaseGroupTests
{
    [VerifyTest]
    public Task Database_first() => Timed.RunAsync("database", nameof(Database_first));

    [VerifyTest]
    public Task Database_second() => Timed.RunAsync("database", nameof(Database_second));

    [VerifyTest]
    public Task Database_third() => Timed.RunAsync("database", nameof(Database_third));

    [VerifyTest]
    public Task Database_fourth() => Timed.RunAsync("database", nameof(Database_fourth));
}

[ScheduleGroup("reports")]
[RunAfter("database")]
public partial class ReportGroupTests
{
    [VerifyTest]
    public Task Report_first() => Timed.RunAsync("reports", nameof(Report_first));

    [VerifyTest]
    public Task Report_second() => Timed.RunAsync("reports", nameof(Report_second));
}

[Resource("payments")]
public partial class RefundTests
{
    [VerifyTest]
    public Task Refunds_through_the_sandbox() => Timed.RunAsync("payments", nameof(Refunds_through_the_sandbox));
}

public partial class ChargeTests
{
    [VerifyTest]
    [Resource("payments")]
    public Task Charges_through_the_sandbox() => Timed.RunAsync("payments", nameof(Charges_through_the_sandbox));
}

/// <summary>
/// Two tests of the default group that each wait for the other to start. They pass only when they run
/// at the same time, which needs more than one processor.
/// </summary>
public partial class PartnerTests
{
    private static readonly TaskCompletionSource s_first = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly TaskCompletionSource s_second = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [VerifyTest]
    public Task Meets_its_partner_first() => MeetAsync(s_first, s_second);

    [VerifyTest]
    public Task Meets_its_partner_second() => MeetAsync(s_second, s_first);

    private static async Task MeetAsync(TaskCompletionSource mine, TaskCompletionSource partner)
    {
        mine.SetResult();
        if (Environment.ProcessorCount == 1)
        {
            return;
        }

        var met = await Task.WhenAny(partner.Task, Task.Delay(TimeSpan.FromSeconds(30))) == partner.Task;
        Verify(met);
    }
}
