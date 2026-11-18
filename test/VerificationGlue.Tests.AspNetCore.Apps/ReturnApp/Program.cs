using Apps;

var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration["Mode"] != "serve")
{
    Console.Error.WriteLine("Set Mode=serve to start the server.");
    return 2;
}

builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();
app.MapGet("/now", (IClock clock) => $"ReturnApp {clock.Now:O}");

app.Run();
return 0;
