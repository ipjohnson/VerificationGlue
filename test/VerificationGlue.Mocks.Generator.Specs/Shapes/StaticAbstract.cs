// case: mocks-shape-0012
// layer: shape
// origin: mocks.md, static abstract members are error VGM0001
// expect: VGM0001

namespace Shapes.StaticAbstract;

public interface IFactory<TSelf> where TSelf : IFactory<TSelf>
{
    static abstract TSelf Create();
    int Value { get; }
}

public interface IDerivesFromStatic : System.IParsable<int>
{
}
