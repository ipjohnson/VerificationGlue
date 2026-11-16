using Demo;
using VerifyGlue;

static void Run(string name, Action a)
{
    try { a(); Console.WriteLine($"[{name}] passed\n"); }
    catch (VerifyException e)
    {
        Console.WriteLine($"[{name}]\n{e.Message}");
        var frame = e.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("Demo."));
        Console.WriteLine($"  first user frame: {frame?.Trim()}\n");
    }
}

var t = new Tests();
// Direct calls. The generator redirects each one to the rewritten clone with an interceptor.
Run("null check", () => t.NullCheck(null));
Run("a == b", () => t.Equal(1, 2));
Run("list.Count > 3", () => t.Count([1, 2]));
Run("&& short circuit", () => t.AndAlso("hello"));
Run("&& both evaluated", () => t.AndAlso("x-much-too-long"));
Run("pattern", () => t.Pattern(new Person("bob", 12)));
Run("pattern null", () => t.Pattern(null));
Run("contains", () => t.Contains(["a", "b"], "c"));
Run("exactly once", () => t.OnceOnly());
Console.WriteLine($"Next() was called {t.Calls} times (expected 3)\n");
Run("await", () => t.Awaited().GetAwaiter().GetResult());
Run("out var scope", () => t.OutVarScope(new() { ["a"] = 1 }));
Run("nested [Verify] helper", () => t.CallsHelper(new Person("al", 3)));
Run("passing", () => t.Equal(2, 2));

// A delegate conversion is not an invocation, so it cannot be intercepted. It runs the original body.
Action<int, int> viaDelegate = t.Equal;
Run("via delegate (original body)", () => viaDelegate(1, 2));

var p = new Plain();
Run("plain null check", () => p.NullCheck(null));
Run("plain a == b", () => p.Equal(1, 2));
Run("plain list.Count > 3", () => p.Count([1]));
