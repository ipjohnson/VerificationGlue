// case: mocks-shape-0014
// layer: shape
// origin: sweep, a pointer member needs AllowUnsafeBlocks in the project that holds the mock
// expect: VGM0001
// library: Libraries/Native.cs

namespace Shapes.PointersWithoutUnsafe;

public class MockTargets
{
    public Native.INative? Native;
}
