using System.Diagnostics;

namespace Hardening.Specs;

/// <summary>
/// The object a generated expression reads, calls and changes. Each member records an event in
/// <see cref="Trace"/>, so a run in plain C# and a run inside <c>Verify</c> or <c>Check</c> can be compared.
/// </summary>
public sealed class Probe
{
    private int _state;

    public List<string> Trace { get; } = [];

    /// <summary>The value of the case method's local <c>n</c> when the case method returned.</summary>
    public int Local { get; set; }

    /// <summary>The number of false results a plain C# baseline counted.</summary>
    public int Failures { get; set; }

    /// <summary>The type and method that called the probe first, which tells the tier that ran.</summary>
    public (string Type, string Method)? Evaluator { get; private set; }

    public string FinalState => $"State={_state} Local={Local}";

    public int State
    {
        get
        {
            Record("get State");
            return _state;
        }
    }

    public bool B(int id, bool value)
    {
        Record($"B{id}");
        return value;
    }

    public int I(int id, int value)
    {
        Record($"I{id}");
        return value;
    }

    public int? N(int id, int? value)
    {
        Record($"N{id}");
        return value;
    }

    public string? S(int id, string? value)
    {
        Record($"S{id}");
        return value;
    }

    public Box O(int id, int value)
    {
        Record($"O{id}");
        return new Box(this, id, value);
    }

    public int Bump(int id)
    {
        Record($"Bump{id}");
        return ++_state;
    }

    public bool Fail(int id)
    {
        Record($"Fail{id}");
        throw new ProbeException(id);
    }

    public bool Try(int id, int value, out int result)
    {
        Record($"Try{id}");
        result = value;
        return value > 0;
    }

    public async Task<int> A(int id, int value)
    {
        Record($"await A{id}");
        await Task.Yield();
        Record($"resume A{id}");
        return value;
    }

    public Task<int> Cancelled(int id)
    {
        Record($"await Cancelled{id}");
        return Task.FromCanceled<int>(new CancellationToken(canceled: true));
    }

    public override string ToString() => "probe";

    internal void Record(string item)
    {
        if (Trace.Count == 0)
        {
            Evaluator = FindEvaluator();
        }

        Trace.Add(item);
    }

    private static (string, string)? FindEvaluator()
    {
        foreach (var frame in new StackTrace(2, false).GetFrames())
        {
            var type = frame.GetMethod()?.DeclaringType;
            if (type is null || IsProbe(type) || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
            {
                continue;
            }

            return (type.FullName ?? type.Name, frame.GetMethod()!.Name);
        }

        return null;
    }

    private static bool IsProbe(Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current == typeof(Probe) || current == typeof(Box))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class Box(Probe probe, int id, int value)
{
    public int V
    {
        get
        {
            probe.Record($"get V{id}");
            return value;
        }
    }

    public override string ToString() => "box";
}

public sealed class ProbeException(int id) : Exception($"Fail{id}");
