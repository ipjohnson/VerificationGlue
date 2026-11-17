using ShopApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IOrderStore, InMemoryOrderStore>();
builder.Services.AddTransient<OrderService>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapPost("/orders", (CreateOrder request, OrderService orders, ILogger<OrderService> log) =>
{
    var order = orders.Create(request.Quantity, request.Price);
    log.LogInformation("Created an order of {Quantity} at {Price}", order.Quantity, order.Price);
    return order;
});
app.MapGet("/orders", (IOrderStore store) => store.Orders);

app.MapControllers();

app.Run();
