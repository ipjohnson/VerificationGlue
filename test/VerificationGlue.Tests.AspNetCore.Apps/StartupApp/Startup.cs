using Apps;

namespace StartupApp;

public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddRouting();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapGet("/now", (IClock clock) => $"StartupApp {clock.Now:O}"));
    }
}
