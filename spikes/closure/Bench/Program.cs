using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bench;

public static class Sample
{
    // Three methods with closures, so the ordinals in the display class names are visible.
    public static Func<bool> First() { var a = 1; return () => a == 1; }
    public static Func<bool> Second() { var b = 2; { var c = 3; return () => b + c == 5; } }
    public static Func<bool> Third(int width) { return () => width == 4; }
}

public static class Program
{
    private const int Calls = 10_000_000;

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Sample))]
    public static void Main()
    {
        NameProbe.Run();
        foreach (var make in new Func<Func<bool>>[] { Sample.First, Sample.Second, () => Sample.Third(4) })
        {
            var target = make().Target!;
            Console.WriteLine(target.GetType().FullName + " fields: " + string.Join(", ", target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(f => f.Name)));
        }

        var closure = Sample.Third(4).Target!;
        var field = closure.GetType().GetField("width", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

        for (var round = 0; round < 3; round++)
        {
            long sum = 0;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < Calls; i++)
            {
                sum += (int)field.GetValue(closure)!;
            }

            var reflection = watch.Elapsed;
            watch.Restart();
            for (var i = 0; i < Calls; i++)
            {
                sum += Width(closure);
            }

            var accessor = watch.Elapsed;
            Console.WriteLine($"cached FieldInfo.GetValue: {reflection.TotalMilliseconds * 1e6 / Calls:F2} ns   UnsafeAccessorType: {accessor.TotalMilliseconds * 1e6 / Calls:F2} ns   ({sum})");
        }

        try
        {
            Console.WriteLine("wrong name: " + WrongName(closure));
        }
        catch (Exception exception)
        {
            Console.WriteLine("wrong name throws " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "width")]
    private static extern ref int Width([UnsafeAccessorType("Bench.Sample+<>c__DisplayClass2_0, Bench")] object closure);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "width")]
    private static extern ref int WrongName([UnsafeAccessorType("Bench.Sample+<>c__DisplayClass9_0, Bench")] object closure);
}

public static class NameProbe
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Tricky))]
    public static void Run()
    {
        var t = new Tricky();
        Show("constructor", Tricky.Last!);
        Show("NoCapture", t.NoCapture());
        Show("OnlyThis", t.OnlyThis());
        var two = t.TwoClosures();
        Show("TwoClosures loop lambda", Tricky.Last!);
        Show("TwoClosures returned", two);
        Show("WithLocalFunction", t.WithLocalFunction());
        _ = t.Getter;
        Show("Getter", Tricky.Last!);
        Show("InSecondPart", t.InSecondPart());
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075")]
    private static void Show(string what, Delegate d)
    {
        var type = d.Target!.GetType();
        Console.WriteLine($"{what,-26} {type.FullName}  [{string.Join(", ", type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(f => f.Name))}]");
    }
}
