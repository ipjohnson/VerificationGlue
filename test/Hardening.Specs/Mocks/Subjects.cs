namespace Hardening.Specs.Mocks;

/// <summary>
/// The interface every generated sequence mocks. Its members cover the member factor of
/// docs/design/hardening.md: overloads, a generic method, a property, an indexer, an event,
/// <c>out</c> and <c>ref</c> parameters, <see cref="Task{T}"/> and <see cref="ValueTask{T}"/>,
/// and a recursive member.
/// </summary>
public interface IService
{
    int Get(int a, int b);

    int Get(int a);

    string? Name(string? key);

    T Echo<T>(T value);

    int this[int index] { get; set; }

    int Count { get; set; }

    Task<int> LoadAsync(int a);

    ValueTask<int> PeekAsync(int a);

    void Reset(int a);

    bool TryGet(int key, out int value);

    void Swap(ref int value);

    IChild Child(int key);

    event EventHandler<int>? Changed;
}

/// <summary>Reached only through <see cref="IService.Child"/>, one step from the named type.</summary>
public interface IChild
{
    int Value(int a);

    ILeaf Leaf { get; }
}

/// <summary>Two steps from <see cref="IService"/>, at the default depth, so <see cref="Next"/> returns null.</summary>
public interface ILeaf
{
    int Value(int a);

    ILeaf Next { get; }
}
