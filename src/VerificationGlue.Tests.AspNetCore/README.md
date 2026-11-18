![VerificationGlue](https://raw.githubusercontent.com/ipjohnson/VerificationGlue/main/assets/logo/lockup.png)

# VerificationGlue.Tests.AspNetCore

Starts an ASP.NET Core app for each `VerificationGlue.Tests` test. The test takes an `HttpClient` as a parameter, and its `[Mock]` parameters are registered in the app.

<!-- snippet: first-app-test -->
<a id='snippet-first-app-test'></a>
```cs
[WebApp<ShopApiEntry>]
public partial class OrderTests
{
    [VerifyTest]
    public async Task Post_stamps_the_order_with_the_clock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2));
        var order = await response.Content.ReadFromJsonAsync<Order>();

        Verify(response.IsSuccessStatusCode);
        Verify(order!.Created == new DateTime(2026, 11, 17));
    }
}
```
<sup><a href='/samples/TestsAspNetCore/OrderTests.cs#L10-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-first-app-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each test gets its own app on TestServer. The app stops when the test ends, and its log is in the test's output.

The test project references this package, `VerificationGlue.Tests` and the app's project.

`ShopApiEntry` is the app's entry type. The test project declares it with the app's assembly name, and the generator in this package makes it an `IWebAppEntry` that calls the app's entry point:

<!-- snippet: name-the-app -->
<a id='snippet-name-the-app'></a>
```cs
using VerificationGlue.Tests.AspNetCore;

namespace TestsAspNetCore;

[WebAppEntry("ShopApi")]
public sealed partial class ShopApiEntry;
```
<sup><a href='/samples/TestsAspNetCore/ShopApiEntry.cs#L1-L8' title='Snippet source file'>snippet source</a> | <a href='#snippet-name-the-app' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Docs

- [Getting started with VerificationGlue.Tests.AspNetCore](../../docs/guide/getting-started-aspnetcore.md)
- [Share an app across a class](../../docs/guide/share-an-app-across-a-class.md)
- [Generated entry types](../../docs/guide/generated/web-app-entry.md)
- [The design of VerificationGlue.Tests.AspNetCore](../../docs/design/aspnet.md)
