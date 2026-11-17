using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Shapes.Tests;

public partial class EarlyExitTests
{
    [VerifyTest]
    public async Task A_throw_before_the_host_reaches_the_bootstrap()
    {
        var error = await ThrowsAsync<InvalidOperationException>(() => WebApp.StartAsync<ThrowAppEntry>([], new LineOutput()));

        Verify(error.Message == "The connection string 'Shop' is missing.");
    }

    [VerifyTest]
    public async Task A_return_before_the_host_names_the_exit_code()
    {
        var error = await ThrowsAsync<InvalidOperationException>(() => WebApp.StartAsync<ReturnAppEntry>([], new LineOutput()));

        Verify(error.Message == "The entry point of ReturnApp returned 2 before it built a host.");
    }
}

internal sealed class LineOutput : ITestOutput
{
    public List<string> Lines { get; } = [];

    public void WriteLine(string message)
    {
        lock (Lines)
        {
            Lines.Add(message);
        }
    }
}
