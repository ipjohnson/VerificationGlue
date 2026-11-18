using FixtureShop;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.AspNetCore;

namespace SharedMock;

[WebAppEntry("FixtureShop")]
public sealed partial class FixtureShopEntry;

[WebApp<FixtureShopEntry>(Scope = ContainerScope.Class)]
public partial class SharedMockTests
{
    [VerifyTest]
    public void Takes_a_mock_under_a_shared_app(HttpClient client, [Mock] IClock clock)
    {
    }
}
