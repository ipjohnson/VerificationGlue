using Shapes;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();
app.MapGet("/now", (IClock clock) => $"VoidApp {clock.Now:O}");

app.Run();
