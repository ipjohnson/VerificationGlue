using Shapes;

var builder = WebApplication.CreateSlimBuilder(args);
var connection = builder.Configuration.GetConnectionString("Shop")
    ?? throw new InvalidOperationException("The connection string 'Shop' is missing.");
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();
app.MapGet("/now", (IClock clock) => $"ThrowApp {connection} {clock.Now:O}");

app.Run();
