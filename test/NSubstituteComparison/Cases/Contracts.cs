namespace NSubstituteComparison.Cases;

public interface ICalculator
{
    int Add(int a, int b);

    int Divide(int a, int b);

    string? Describe(string? text);

    int Sum(params int[] values);

    int Count(int[] values);

    bool TryParse(string text, out int value);

    void Reset();

    int Mode { get; set; }

    Task<int> ComputeAsync(int value);

    Task<string> NameAsync();

    ValueTask<int> CountAsync();

    Task SaveAsync();
}
