using Apps;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

[WebAppEntry("VoidApp")]
public sealed partial class VoidAppEntry;

[WebAppEntry("IntApp")]
public sealed partial class IntAppEntry;

[WebAppEntry("TaskApp")]
public sealed partial class TaskAppEntry;

[WebAppEntry("TaskIntApp")]
public sealed partial class TaskIntAppEntry;

[WebAppEntry("MainApp")]
public sealed partial class MainAppEntry;

[WebAppEntry("InternalApp")]
public sealed partial class InternalAppEntry;

[WebAppEntry("InternalIvtApp")]
public sealed partial class InternalIvtAppEntry;

[WebAppEntry("StartupApp")]
public sealed partial class StartupAppEntry;

internal static class Day
{
    public static readonly DateTime Mocked = new(2026, 11, 17);

    public static async Task VerifyNowAsync(HttpClient client, IClock clock, string app)
    {
        clock.Now.Returns(Mocked);

        Verify(await client.GetStringAsync("/now") == app + " 2026-11-17T00:00:00.0000000");
    }
}

[WebApp<VoidAppEntry>]
public partial class VoidAppTests
{
    [VerifyTest]
    public Task Top_level_void_starts_with_the_mock(HttpClient client, [Mock] IClock clock) => Day.VerifyNowAsync(client, clock, "VoidApp");
}

[WebApp<IntAppEntry>]
public partial class IntAppTests
{
    [VerifyTest]
    public Task Top_level_int_starts_with_the_mock(HttpClient client, [Mock] IClock clock) => Day.VerifyNowAsync(client, clock, "IntApp");
}

[WebApp<TaskAppEntry>]
public partial class TaskAppTests
{
    [VerifyTest]
    public Task Top_level_Task_starts_with_the_mock(HttpClient client, [Mock] IClock clock) => Day.VerifyNowAsync(client, clock, "TaskApp");
}

[WebApp<TaskIntAppEntry>]
public partial class TaskIntAppTests
{
    [VerifyTest]
    public Task Top_level_Task_of_int_starts_with_the_mock(HttpClient client, [Mock] IClock clock) => Day.VerifyNowAsync(client, clock, "TaskIntApp");
}

[WebApp<MainAppEntry>]
public partial class MainAppTests
{
    [VerifyTest]
    public Task Explicit_Main_in_a_static_class_starts_with_the_mock(HttpClient client, [Mock] IClock clock) => Day.VerifyNowAsync(client, clock, "MainApp");
}

[WebApp<InternalAppEntry>]
public partial class InternalAppTests
{
    [VerifyTest]
    public Task Internal_Program_starts_with_the_mock(HttpClient client, [Mock] IClock clock) => Day.VerifyNowAsync(client, clock, "InternalApp");
}

[WebApp<InternalIvtAppEntry>]
public partial class InternalIvtAppTests
{
    [VerifyTest]
    public Task Internal_Program_with_InternalsVisibleTo_starts_with_the_mock(HttpClient client, [Mock] IClock clock) => Day.VerifyNowAsync(client, clock, "InternalIvtApp");
}

[WebApp<StartupAppEntry>]
public partial class StartupAppTests
{
    [VerifyTest]
    public Task Startup_class_with_the_generic_host_starts_with_the_mock(HttpClient client, [Mock] IClock clock) => Day.VerifyNowAsync(client, clock, "StartupApp");
}
