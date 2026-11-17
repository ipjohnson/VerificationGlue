using Shapes;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();
app.MapGet("/now", (IClock clock) => $"InternalApp {clock.Now:O}");

app.Run();

internal partial class Program;
