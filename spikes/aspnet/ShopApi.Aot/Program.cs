using System.Text.Json.Serialization;
using ShopApi;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ShopJson.Default));
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IOrderStore, InMemoryOrderStore>();
builder.Services.AddTransient<OrderService>();

var app = builder.Build();

app.MapPost("/orders", (CreateOrder request, OrderService orders, ILogger<OrderService> log) =>
{
    var order = orders.Create(request.Quantity, request.Price);
    log.LogInformation("Created an order of {Quantity} at {Price}", order.Quantity, order.Price);
    return order;
});
app.MapGet("/orders", (IOrderStore store) => store.Orders);

app.Run();

[JsonSourceGenerationOptions(System.Text.Json.JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(CreateOrder))]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(Order[]))]
[JsonSerializable(typeof(IReadOnlyList<Order>))]
public partial class ShopJson : JsonSerializerContext;
