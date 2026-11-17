# VerificationGlue.Tests.AspNetCore

Starts an ASP.NET Core app for each `VerificationGlue.Tests` test. The test takes an `HttpClient` as a parameter, and its `[Mock]` parameters are registered in the app.

```csharp
[WebApp<ShopApiEntry>]
public partial class OrderTests
{
    [VerifyTest]
    public async Task Post_stamps_the_order_with_the_clock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var order = await (await client.PostAsJsonAsync("/orders", new CreateOrder(2, 10))).Content.ReadFromJsonAsync<Order>();

        Verify(order!.Created == new DateTime(2026, 11, 17));
    }
}
```

Each test gets its own app on TestServer. The app stops when the test ends, and its log is in the test's output.

The test project references this package, `VerificationGlue.Tests` and the app's project.

`ShopApiEntry` is the app's entry type, an `IWebAppEntry`. A partial class marked `[WebAppEntry("ShopApi")]` will be made the entry type by a generator, which is not built yet. Until then, this package is a preview of the runtime.

- [The design of VerificationGlue.Tests.AspNetCore](../../docs/design/aspnet.md)
