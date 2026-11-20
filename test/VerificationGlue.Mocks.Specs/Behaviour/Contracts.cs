using VerificationGlue.Mocks;

[assembly: GenerateMock(typeof(VerificationGlue.Mocks.Specs.Behaviour.IClock))]

namespace VerificationGlue.Mocks.Specs.Behaviour;

public enum Mode
{
    Basic,
    Scientific,
}

public interface ICalculator
{
    int Add(int a, int b);

    int Divide(int a, int b);

    bool TryParse(string text, out int value);

    int Scale(ref int value);

    void Reset();

    Task<int> ComputeAsync(int value);

    ValueTask<string> NameAsync();

    Mode Mode { get; set; }

    string? Label { get; set; }

    int this[int index] { get; }

    event EventHandler<int>? Changed;

    T Echo<T>(T value);

    TimeSpan Since(IClock clock);

    IHistory History { get; }
}

/// <summary>Reached only through <see cref="ICalculator.History"/>, so its mock is a recursive one.</summary>
public interface IHistory
{
    int Last { get; }

    IEntry Find(int index);

    IEntry Search(string text);

    IEntry Range(int[] indexes);
}

/// <summary>Two steps from <see cref="ICalculator"/>, at the default depth, so its own members return null.</summary>
public interface IEntry
{
    string Text { get; }

    IEntry Next { get; }
}

/// <summary>Named only in <c>[assembly: GenerateMock]</c>, and created by a generic helper.</summary>
public interface IClock
{
    DateTime Now { get; }
}

/// <summary>Named only in a <c>[Mock]</c> attribute.</summary>
public interface IGreeter
{
    string Greet(string name);
}
