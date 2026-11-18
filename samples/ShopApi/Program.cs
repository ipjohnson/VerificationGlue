using ShopApi;

#region shop-api-program
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IOrderStore, OrderStore>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapPost("/orders", (CreateOrder request, IClock clock, IOrderStore store, ILogger<Program> log) =>
{
    var order = store.Add(request.Quantity, clock.Now);
    log.LogInformation("Saved order {Id} of {Quantity}", order.Id, order.Quantity);
    return order;
});
app.MapGet("/orders", (IOrderStore store) => store.Orders);
app.MapControllers();

app.Run();
#endregion
