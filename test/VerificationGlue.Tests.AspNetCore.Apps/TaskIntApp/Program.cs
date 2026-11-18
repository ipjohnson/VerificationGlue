using Apps;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();
app.MapGet("/now", (IClock clock) => $"TaskIntApp {clock.Now:O}");

await app.RunAsync();
return 0;
