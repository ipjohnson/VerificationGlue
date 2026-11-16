using VerifyGlue;
using static VerifyGlue.VerifySyntax;

Verify(1 == 1);
var ex = Verify.Throws<InvalidOperationException>(() => throw new InvalidOperationException("boom"));
Verify(ex.Message == "boom");
Console.WriteLine("type Verify and method Verify coexist: " + ex.Message);

namespace VerifyGlue
{
    public static class Verify
    {
        public static T Throws<T>(Action a) where T : Exception
        {
            try { a(); } catch (T e) { return e; }
            throw new Exception("did not throw");
        }
    }
    public static class VerifySyntax
    {
        public static void Verify(bool condition) { if (!condition) throw new Exception("failed"); }
    }
}
