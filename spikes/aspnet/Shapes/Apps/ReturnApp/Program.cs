using Shapes;

var builder = WebApplication.CreateSlimBuilder(args);
if (builder.Configuration["Mode"] != "serve")
{
    Console.Error.WriteLine("Set Mode=serve to start the server.");
    return 2;
}

builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();
app.MapGet("/now", (IClock clock) => $"ReturnApp {clock.Now:O}");

await app.RunAsync();
return 0;
