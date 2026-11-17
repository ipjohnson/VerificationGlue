#if SHOW_FAILURE
using System.Net.Http.Json;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace ShopApi.Tests;

/// <summary>Fails on purpose, to show the app's log in the test's output. Build with -p:ShowFailure=true.</summary>
[WebApp<ShopApiEntry>]
public partial class FailingTests
{
    [VerifyTest]
    public async Task A_failing_test_shows_the_app_log(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(3, 7));

        Verify(response.StatusCode == System.Net.HttpStatusCode.Created);
    }
}
#endif
