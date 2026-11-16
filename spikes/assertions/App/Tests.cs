using VerifyGlue;
using static VerifyGlue.VerifySyntax;

namespace Demo;

public record Person(string Name, int Age);

public partial class Tests
{
    public int Calls;
    int Next() => ++Calls;

    [Verify] public void NullCheck(object? x)
    {
        Verify(x is not null);
    }

    [Verify] public void Equal(int a, int b)
    {
        Verify(a == b);
    }

    [Verify] public void Count(List<int> list)
    {
        Verify(list.Count > 3);
    }

    [Verify] public void AndAlso(string s)
    {
        Verify(s.StartsWith("x") && s.Length < 10);
    }

    [Verify] public void Pattern(Person? x)
    {
        Verify(x is { Name: "bob", Age: > 18 });
    }

    [Verify] public void Contains(List<string> items, string y)
    {
        Verify(items.Contains(y));
    }

    [Verify] public void OnceOnly()
    {
        Verify(Next() + Next() == 10 || Next() == 0);
    }

    [Verify] public async Task Awaited()
    {
        Verify(await Task.FromResult(41) == 42);
    }

    [Verify] public void OutVarScope(Dictionary<string, int> d)
    {
        Verify(d.TryGetValue("a", out var v));
        Verify(
            v ==
            2);
    }

    [Verify] public void CallsHelper(Person p)
    {
        Helper(p);
    }

    [Verify] static void Helper(Person p)
    {
        Verify(p.Age >= 18);
    }
}

// Not partial and not [Verify]: only the per-call-site interceptor applies.
public class Plain
{
    public void NullCheck(object? x) => Verify(x is not null);
    public void Equal(int a, int b) => Verify(a == b);
    public void Count(List<int> list) => Verify(list.Count > 3);
}
