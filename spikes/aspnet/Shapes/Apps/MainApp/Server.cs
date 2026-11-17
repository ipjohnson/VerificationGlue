using Shapes;

namespace MainApp;

// A static class cannot be the type of an UnsafeAccessor parameter, so the entry type must name it with UnsafeAccessorType.
public static class Server
{
    private static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.Services.AddSingleton<IClock, SystemClock>();

        var app = builder.Build();
        app.MapGet("/now", (IClock clock) => $"MainApp {clock.Now:O}");

        await app.RunAsync();
        return 0;
    }
}
