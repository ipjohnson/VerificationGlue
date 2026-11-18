using Apps;

// This app does not pass args to its builder.
var builder = WebApplication.CreateBuilder();
builder.Services.AddSingleton<IClock, SystemClock>();
var beforeBuild = builder.Configuration["Fixture:Setting"] ?? "none";

var app = builder.Build();
app.MapGet("/setting", (IConfiguration configuration) => $"before Build: {beforeBuild}, after Build: {configuration["Fixture:Setting"] ?? "none"}");
app.MapGet("/environment", (IHostEnvironment environment) => environment.EnvironmentName);

app.Run();
