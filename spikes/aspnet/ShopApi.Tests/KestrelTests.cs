using System.Net.Http.Json;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace ShopApi.Tests;

[WebApp<ShopApiEntry>(Kestrel = true)]
public partial class KestrelTests
{
    [VerifyTest]
    public async Task Listens_on_a_real_port(HttpClient client, [Mock] IClock clock, ITestOutput output)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));
        output.WriteLine($"Base address {client.BaseAddress}");

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2, 10));
        var order = await response.Content.ReadFromJsonAsync<Order>();

        Verify(client.BaseAddress!.Port > 1024);
        Verify(order!.Created == new DateTime(2026, 11, 17));
    }
}
