using FixtureShop;

BuildWindow.Enter();
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IOrderStore, OrderStore>();
builder.Services.AddScoped<Basket>();
builder.Services.AddControllers();

BuildWindow.Exit();
var app = builder.Build();

app.MapPost("/orders", (CreateOrder request, IClock clock, IOrderStore store, ILoggerFactory logs) =>
{
    var order = store.Add(request.Quantity, clock.Now);
    logs.CreateLogger("FixtureShop.Orders").LogInformation("Saved order {Id} of {Quantity}", order.Id, order.Quantity);
    return order;
});
app.MapGet("/orders", (IOrderStore store) => store.Orders);
app.MapControllers();

app.Run();
app.Logger.LogInformation("The entry point of FixtureShop is returning");
return 0;
