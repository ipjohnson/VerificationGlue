// case: mocks-shape-0015
// layer: shape
// origin: mocks.md, sealed types, structs and classes before class mocks are error VGM0001
// expect: VGM0001

namespace Shapes.NotAnInterface;

public struct Point
{
}

public sealed class Sealed
{
}

public class Open
{
    public virtual int Value() => 0;
}

public class Outer
{
    private interface IPrivate
    {
        void Run();
    }

    internal static System.Type Private => typeof(IPrivate);
}

public class MockTargets
{
    public Point Point;
    public Sealed? Sealed;
    public Open? Open;
}
