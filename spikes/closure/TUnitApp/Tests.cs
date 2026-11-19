using static ClosureGlue.Checks;

namespace Shop.TUnitTests;

public class ComparerTests
{
    private readonly List<string> _expected = ["a", "b", "c"];

    [Test]
    public void CaseCount()
    {
        var cases = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
        Verify(() => cases.Count == 9);
    }

    [Test]
    [Arguments(1)]
    public async Task LinesMatch(int skip)
    {
        await Task.Yield();
        var actual = new List<string> { "a", "x", "c" };
        Verify(() => actual.Skip(skip).First() == _expected[skip]);
    }
}
