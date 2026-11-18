using System.Globalization;
using ShopApi;

#region shop-api-program
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IOrderStore, OrderStore>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapPost("/orders", (CreateOrder request, IClock clock, IOrderStore store, IConfiguration configuration, ILogger<Program> log) =>
{
    if (request.Quantity > int.Parse(configuration["Orders:MaxQuantity"]!, CultureInfo.InvariantCulture))
    {
        return Results.BadRequest();
    }

    var order = store.Add(request.Quantity, clock.Now);
    log.LogInformation("Saved order {Id} of {Quantity}", order.Id, order.Quantity);
    return Results.Ok(order);
});
app.MapGet("/orders", (IOrderStore store) => store.Orders);
app.MapControllers();

app.Run();
#endregion
