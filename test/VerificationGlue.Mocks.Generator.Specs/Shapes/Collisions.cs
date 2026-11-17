// case: mocks-shape-0001
// layer: shape
// origin: mocks.md, explicit interface collisions and diamond inheritance
// expect: pass

namespace Shapes.Collisions;

public interface IReader
{
    string Read();
    int Count { get; }
    event System.EventHandler Changed;
}

public interface IWriter
{
    void Read(string text);
    long Count { get; set; }
    event System.EventHandler<int> Changed;
}

public interface IReadWriter : IReader, IWriter
{
    new object Read();
}

public interface IRoot
{
    void Run();
    int Value { get; }
}

public interface ILeft : IRoot
{
    void IRoot.Run() { }
}

public interface IRight : IRoot
{
    void IRoot.Run() { }
}

public interface IDiamond : ILeft, IRight
{
    new int Value { get; }
}

public interface IDictionaryLike<TKey, TValue> : System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<TKey, TValue>>, System.Collections.IEnumerable
{
}

public class MockTargets
{
    public IReadWriter? ReadWriter;
    public IDiamond? Diamond;
    public IDictionaryLike<string, int>? DictionaryLike;
}
