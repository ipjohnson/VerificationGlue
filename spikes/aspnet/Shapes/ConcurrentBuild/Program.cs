// Builds hosts on several threads at once in a fresh process, with no test bootstrap. Under Native
// AOT this separates a fault of the framework from a fault of the bootstrap's host capture.
// Usage: ConcurrentBuild <threads> [slim|startup|mixed]
var threads = args.Length > 0 ? int.Parse(args[0]) : 16;
var mode = args.Length > 1 ? args[1] : "slim";
using var barrier = new Barrier(threads);
var failures = new List<string>();
Exception? first = null;
var workers = Enumerable.Range(0, threads).Select(i => new Thread(() =>
{
    barrier.SignalAndWait();
    try
    {
        IHost host = mode == "startup" || (mode == "mixed" && i % 2 == 1)
            ? Host.CreateDefaultBuilder(["--hostBuilder:reloadConfigOnChange=false"]).ConfigureWebHostDefaults(web => web.UseStartup<Startup>()).Build()
            : BuildSlim();
        host.Dispose();
    }
    catch (Exception failure)
    {
        lock (failures)
        {
            first ??= failure;
            failures.Add(failure.GetBaseException().GetType().Name + ": " + failure.GetBaseException().Message);
        }
    }
})).ToList();
workers.ForEach(worker => worker.Start());
workers.ForEach(worker => worker.Join());
Console.WriteLine($"{threads} threads, {failures.Count} failed");
foreach (var failure in failures.Distinct())
{
    Console.WriteLine("  " + failure);
}

if (first is not null)
{
    Console.WriteLine(first);
}

return failures.Count == 0 ? 0 : 1;

static IHost BuildSlim()
{
    var builder = WebApplication.CreateSlimBuilder(["--hostBuilder:reloadConfigOnChange=false"]);
    builder.Services.AddSingleton(TimeProvider.System);
    var app = builder.Build();
    app.MapGet("/now", (TimeProvider time) => time.GetUtcNow().ToString("O"));
    return app;
}

public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services) => services.AddRouting();

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapGet("/now", () => "now"));
    }
}
