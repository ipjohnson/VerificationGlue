// A test with no __Verified copy, so a breakpoint here shows whether the debugger and the run work at all.
using VerificationGlue;

namespace MyApp.Tests;

public class ControlTests
{
    [VerifyTest]
    public void Plain()
    {
        var total = 40;
        total += 2;
        if (total != 42) throw new InvalidOperationException("total");
    }
}
