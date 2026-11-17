using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Shapes.Tests;

public partial class ConcurrentStartTests
{
    [VerifyTest]
    public async Task Apps_that_start_at_once_each_get_their_own_host_and_overrides()
    {
        const int count = 16;
        var outputs = Enumerable.Range(0, count).Select(_ => new LineOutput()).ToArray();
        var starts = Enumerable.Range(0, count).Select(i => Start(i, outputs[i])).ToArray();
        var apps = await Task.WhenAll(starts);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var text = await apps[i].Client.GetStringAsync("/now");
                Verify(text == $"{Name(i)} {Day.Mocked.AddDays(i):O}");
            }

            Verify(apps.Select(app => app.Host).Distinct().Count() == count);
            Verify(outputs.All(output => output.Lines.Count(line => line.Contains(" started in ")) == 1));
            Verify(WebApp.SerialBuild || WebApp.MostStartingAtOnce > 1);
            Console.WriteLine($"At most {WebApp.MostStartingAtOnce} of {count} apps were starting at once.");
        }
        finally
        {
            foreach (var app in apps)
            {
                await app.DisposeAsync();
            }
        }
    }

    private static Task<RunningApp> Start(int i, LineOutput output)
    {
        TestOverride[] overrides = [new(typeof(IClock), new FixedClock(Day.Mocked.AddDays(i)), null)];
        return (i % 4) switch
        {
            0 => WebApp.StartAsync<VoidAppEntry>(overrides, output),
            1 => WebApp.StartAsync<TaskIntAppEntry>(overrides, output),
            2 => WebApp.StartAsync<MainAppEntry>(overrides, output),
            _ => WebApp.StartAsync<StartupAppEntry>(overrides, output),
        };
    }

    private static string Name(int i) => (i % 4) switch
    {
        0 => "VoidApp",
        1 => "TaskIntApp",
        2 => "MainApp",
        _ => "StartupApp",
    };

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime Now => now;
    }
}
