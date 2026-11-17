#if SHOW_FAILURE
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Shapes.Tests;

/// <summary>Fails on purpose, to show what a test reports when the app stops before it builds a host. Build with -p:ShowFailure=true.</summary>
[WebApp<ThrowAppEntry>]
public partial class ThrowingAppTests
{
    [VerifyTest]
    public async Task The_app_throws_before_it_builds_a_host(HttpClient client)
    {
        Verify(await client.GetStringAsync("/now") != "");
    }
}

[WebApp<ReturnAppEntry>]
public partial class ReturningAppTests
{
    [VerifyTest]
    public async Task The_app_returns_before_it_builds_a_host(HttpClient client)
    {
        Verify(await client.GetStringAsync("/now") != "");
    }
}
#endif
