// case: mocks-shape-0004
// layer: shape
// origin: mocks.md, keyword identifiers, init accessors and member hiding with new
// expect: pass

namespace Shapes.Names;

public interface @interface
{
    void @class(int @event, string @operator);
    int @namespace { get; set; }
    event System.Action @delegate;
}

public interface IContextual
{
    void value(int value);
    int field { get; }
    int var(int var, int dynamic, int nameof, int await, int async);
    void Locals(int __args, int __result, int __raise, int __handler, int _adapter);
}

public interface IAccessors
{
    int Init { get; init; }
    string SetOnly { set; }
    int this[string key] { set; }
    int GetOnly { get; }
}

public interface IBase
{
    int Value { get; }
    void Run();
    string Name();
}

public interface IHiding : IBase
{
    new string Value { get; set; }
    new void Run();
    new int Name();
}

public class MockTargets
{
    public @interface? Keywords;
    public IContextual? Contextual;
    public IAccessors? Accessors;
    public IHiding? Hiding;
}
