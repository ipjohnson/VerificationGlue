namespace Bench;

// Shapes that could shift the ordinals in display class names.
public partial class Tricky
{
    private int _field = 1;
    public int Property => _field;
    public Func<bool> InitializerLambda = () => true;
    public Tricky() { var k = 1; Keep(() => k == 1); }

    public Func<bool> NoCapture() => () => true;
    public Func<bool> OnlyThis() => () => _field == 1;

    public Func<bool> TwoClosures()
    {
        var a = 1;
        Keep(() => a == 1);
        for (var i = 0; i < 1; i++)
        {
            var b = i;
            Keep(() => a + b == 1);
        }

        return () => a == 2;
    }

    public Func<bool> WithLocalFunction()
    {
        var c = 3;
        bool Local() => c == 3;
        return () => Local();
    }

    public int Getter { get { var d = 4; Keep(() => d == 4); return d; } }

    public static void Keep(Func<bool> f) => Last = f;
    public static Func<bool>? Last;
}

public partial class Tricky
{
    public Func<bool> InSecondPart() { var e = 5; return () => e == 5; }
}
