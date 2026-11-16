using VerifyGlue;
using static VerifyGlue.VerifySyntax;
public partial class T
{
    [Verify] public void M(int a)
    {
        int unused = 1;
        Verify(a == 1);
        string s = 5;
    }
}
class P { static void Main() { } }
