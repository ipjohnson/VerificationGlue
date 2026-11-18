using Apps;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();
app.MapGet("/now", (IClock clock) => $"VoidApp {clock.Now:O}");

app.Run();
