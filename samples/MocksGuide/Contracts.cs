namespace MocksGuide;

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

    void Reset();

    Mode Mode { get; set; }

    TimeSpan Since(IClock clock);

    Task<int> ComputeAsync(int value);

    ValueTask<string> NameAsync();

    Task SaveAsync();

    IHistory History { get; }
}

public interface IHistory
{
    int Last { get; }

    IEntry Find(int index);
}

public interface IEntry
{
    string Text { get; }

    IEntry Next { get; }
}

public interface IClock
{
    DateTime Now { get; }
}
