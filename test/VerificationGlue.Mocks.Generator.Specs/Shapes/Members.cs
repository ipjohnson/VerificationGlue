// case: mocks-shape-0010
// layer: shape
// origin: mocks.md supported shapes: async returns, ref returns, events, indexers and default interface members
// expect: pass

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shapes.Members;

public delegate int Counter(ref int count, out string label, in DateTime at);

public interface IAsync
{
    Task Run(CancellationToken token);
    Task<int> Compute(int value);
    ValueTask Flush();
    ValueTask<string?> Read();
    IAsyncEnumerable<int> Stream();
    Task<T> Generic<T>(T value);
}

public interface IRefReturns
{
    ref int Slot(int index);
    ref readonly Guid ReadonlySlot();
    ref T GenericSlot<T>();
    ref int this[int index] { get; }
    ref readonly long Total { get; }
    ref int Write(out int written);
}

public interface IEvents
{
    event EventHandler? Plain;
    event EventHandler<int> Generic;
    event Counter ByRef;
    event Action<string, int, object?>? Many;
    event Func<int, Task<bool>> Returning;
}

public interface IDefaults
{
    int Abstract();
    int Virtual() => 1;
    sealed int Sealed() => 2;
    private int Private() => 3;
    static int Static() => 4;
    static virtual int StaticVirtual() => 5;
    int Property { get => 6; set { } }
    event EventHandler Event { add { } remove { } }
    static int StaticField = 7;
}

public interface IIndexers
{
    string this[int index] { get; set; }
    string this[string key, int version] { get; }
    int this[Guid id] { set; }
}

public class MockTargets
{
    public IAsync? Async;
}
