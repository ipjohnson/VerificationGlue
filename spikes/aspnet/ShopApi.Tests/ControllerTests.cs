using System.Net.Http.Json;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace ShopApi.Tests;

[WebApp<ShopApiEntry>]
public partial class ControllerTests
{
    [VerifyTest]
    public async Task A_controller_receives_the_mock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var now = await client.GetFromJsonAsync<DateTime>("/clock");

        Verify(now == new DateTime(2026, 11, 17));
    }
}
