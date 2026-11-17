using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Shapes.Tests;

// The entry generator fills in each of these from the referenced app assembly.
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

[WebAppEntry("ThrowApp")]
public sealed partial class ThrowAppEntry;

[WebAppEntry("ReturnApp")]
public sealed partial class ReturnAppEntry;

internal static class Day
{
    public static readonly DateTime Mocked = new(2026, 11, 17);
}

[WebApp<VoidAppEntry>]
public partial class VoidAppTests
{
    [VerifyTest]
    public async Task Top_level_void_starts_with_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "VoidApp 2026-11-17T00:00:00.0000000");
    }
}

[WebApp<IntAppEntry>]
public partial class IntAppTests
{
    [VerifyTest]
    public async Task Top_level_int_starts_with_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "IntApp 2026-11-17T00:00:00.0000000");
    }
}

[WebApp<TaskAppEntry>]
public partial class TaskAppTests
{
    [VerifyTest]
    public async Task Top_level_Task_starts_with_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "TaskApp 2026-11-17T00:00:00.0000000");
    }
}

[WebApp<TaskIntAppEntry>]
public partial class TaskIntAppTests
{
    [VerifyTest]
    public async Task Top_level_Task_of_int_starts_with_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "TaskIntApp 2026-11-17T00:00:00.0000000");
    }
}

[WebApp<MainAppEntry>]
public partial class MainAppTests
{
    [VerifyTest]
    public async Task Explicit_Main_starts_with_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "MainApp 2026-11-17T00:00:00.0000000");
    }
}

[WebApp<InternalAppEntry>]
public partial class InternalAppTests
{
    [VerifyTest]
    public async Task Internal_Program_starts_with_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "InternalApp 2026-11-17T00:00:00.0000000");
    }
}

[WebApp<InternalIvtAppEntry>]
public partial class InternalIvtAppTests
{
    [VerifyTest]
    public async Task Internal_Program_with_InternalsVisibleTo_starts_with_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "InternalIvtApp 2026-11-17T00:00:00.0000000");
    }
}

[WebApp<StartupAppEntry>]
public partial class StartupAppTests
{
    [VerifyTest]
    public async Task Startup_class_with_the_generic_host_starts_with_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "StartupApp 2026-11-17T00:00:00.0000000");
    }
}
