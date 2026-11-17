// case: mocks-shape-0011
// layer: shape
// origin: mocks.md, ref struct parameters warn with VGM0003 and the member throws
// expect: VGM0003

using System;

namespace Shapes.RefStruct;

public delegate void SpanHandler(ReadOnlySpan<char> text);

public interface ISpans
{
    int Parse(ReadOnlySpan<char> text);
    Span<byte> Buffer(int size);
    bool TryWrite(Span<char> destination, out int written);
    ReadOnlySpan<int> Values { get; }
    int this[Span<int> key] { get; }

    // The event can be added and removed. Raising it throws, because its argument is a ref struct.
    event SpanHandler Text;
}
