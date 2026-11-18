using Apps;

namespace MainApp;

public static class Server
{
    private static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<IClock, SystemClock>();

        var app = builder.Build();
        app.MapGet("/now", (IClock clock) => $"MainApp {clock.Now:O}");

        await app.RunAsync();
        return 0;
    }
}
