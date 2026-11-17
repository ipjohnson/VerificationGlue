using ShopApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IOrderStore, OrderStore>();

var app = builder.Build();

app.MapPost("/orders", (CreateOrder request, IClock clock, IOrderStore store) => store.Add(request.Quantity, clock.Now));
app.MapGet("/orders", (IOrderStore store) => store.Orders);

app.Run();
