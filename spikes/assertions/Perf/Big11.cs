using VerifyGlue;
using static VerifyGlue.VerifySyntax;
namespace Perf;
public partial class Big11 {
    [Verify] public void M0(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 0);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M1(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 1);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M2(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 2);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M3(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 3);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M4(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 4);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M5(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 5);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M6(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 6);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M7(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 0);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M8(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 1);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M9(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 2);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M10(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 3);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M11(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 4);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M12(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 5);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M13(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 6);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M14(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 0);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M15(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 1);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M16(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 2);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M17(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 3);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M18(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 4);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
    [Verify] public void M19(List<int> list, string s, int a, int b)
    {
        Verify(a == b);
        Verify(list.Count > 5);
        Verify(s.StartsWith("x") && s.Length < 10);
    }
}