using System.Globalization;

namespace NSubstituteComparison;

/// <summary>What one run of a case saw, as labelled values. Two runs agree when their entries are equal.</summary>
public sealed class Observations
{
    private readonly List<Observation> _entries = [];

    public IReadOnlyList<Observation> Entries => _entries;

    public void Record(string label, object? value) => _entries.Add(new Observation(label, Format(value)));

    /// <summary>Runs <paramref name="action"/> and records <c>ok</c>, or the type of the exception it throws.</summary>
    public void Try(string label, Action action)
    {
        try
        {
            action();
            _entries.Add(new Observation(label, "ok"));
        }
        catch (Exception exception)
        {
            _entries.Add(new Observation(label, Thrown(exception)));
        }
    }

    /// <summary>
    /// Records the value <paramref name="call"/> returns, or the type of the exception it throws.
    /// It is not an overload of <see cref="Try(string, Action)"/>, because a lambda such as
    /// <c>() => calc.Add(1, 2).Returns(3)</c> would then record NSubstitute's <c>ConfiguredCall</c>.
    /// </summary>
    public void TryValue<T>(string label, Func<T> call)
    {
        try
        {
            Record(label, call());
        }
        catch (Exception exception)
        {
            _entries.Add(new Observation(label, Thrown(exception)));
        }
    }

    /// <summary>Awaits <paramref name="action"/> and records <c>ok</c>, or the type of the exception it throws.</summary>
    public async Task TryAsync(string label, Func<Task> action)
    {
        try
        {
            await action();
            _entries.Add(new Observation(label, "ok"));
        }
        catch (Exception exception)
        {
            _entries.Add(new Observation(label, Thrown(exception)));
        }
    }

    internal void Add(Observation observation) => _entries.Add(observation);

    internal static string Thrown(Exception exception) => "throws " + exception.GetType().Name;

    private static string Format(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        bool flag => flag ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };
}

public sealed record Observation(string Label, string Value);
