// case: mocks-shape-0013
// layer: shape
// origin: sweep, System.Runtime.InteropServices.Marshalling.IIUnknownStrategy has pointer members
// expect: VGM0003
// options: unsafe

namespace Shapes.Pointers;

public unsafe interface IPointers
{
    void* Allocate(int size);
    int Read(byte* buffer, int length);
    int*[] Table { get; }
    delegate* unmanaged<int, void> Callback { get; }
    int Safe(int value);
}
