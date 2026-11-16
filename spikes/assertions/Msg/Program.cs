using System.Runtime.CompilerServices;
using System.Text;
using static V;

var evaluations = 0;
string Expensive() { evaluations++; return "expensive"; }

Verify(1 == 1, $"not shown {Expensive()}");
Verify2(1 == 1).WithMessage($"not shown {Expensive()}");
Console.WriteLine($"holes evaluated on passing checks: {evaluations}");

try { Verify(1 == 2, $"two-arg form: {Expensive()}"); } catch (Exception e) { Console.WriteLine(e.Message); }
try { Verify2(1 == 2).WithMessage($"fluent form: {Expensive()}"); } catch (Exception e) { Console.WriteLine(e.Message); }
Console.WriteLine($"holes evaluated after two failures: {evaluations}");

// Soft checks collected by a scope.
try
{
    using (var scope = new Scope())
    {
        scope.Check(1 == 2);
        scope.Check("a" == "b");
    }
}
catch (Exception e) { Console.WriteLine(e.Message); }

static class V
{
    public static void Verify(bool condition, [InterpolatedStringHandlerArgument(nameof(condition))] ref Message message,
        [CallerArgumentExpression(nameof(condition))] string? expr = null)
    {
        if (!condition) throw new Exception($"Verify failed: {expr}\n  message: {message.ToString()}");
    }

    // Stand-in for what a chain-aware interceptor would return: a failed result that has not thrown yet.
    public static Result Verify2(bool condition, [CallerArgumentExpression(nameof(condition))] string? expr = null) => new(condition, expr!);
}

readonly struct Result(bool passed, string expr)
{
    public bool Passed => passed;
    public void WithMessage([InterpolatedStringHandlerArgument("")] ref ResultMessage message)
    {
        if (!passed) throw new Exception($"Verify failed: {expr}\n  message: {message.ToString()}");
    }
}

[InterpolatedStringHandler]
ref struct Message
{
    StringBuilder? _sb;
    public Message(int literalLength, int formattedCount, bool condition, out bool shouldAppend)
    { shouldAppend = !condition; if (shouldAppend) _sb = new StringBuilder(literalLength); }
    public void AppendLiteral(string s) => _sb!.Append(s);
    public void AppendFormatted<T>(T value) => _sb!.Append(value);
    public override string ToString() => _sb?.ToString() ?? "";
}

[InterpolatedStringHandler]
ref struct ResultMessage
{
    StringBuilder? _sb;
    public ResultMessage(int literalLength, int formattedCount, Result result, out bool shouldAppend)
    { shouldAppend = !result.Passed; if (shouldAppend) _sb = new StringBuilder(literalLength); }
    public void AppendLiteral(string s) => _sb!.Append(s);
    public void AppendFormatted<T>(T value) => _sb!.Append(value);
    public override string ToString() => _sb?.ToString() ?? "";
}

sealed class Scope : IDisposable
{
    readonly List<string> _failures = new();
    public void Check(bool condition, [CallerArgumentExpression(nameof(condition))] string? expr = null, [CallerLineNumber] int line = 0)
    { if (!condition) _failures.Add($"{expr} (line {line})"); }
    public void Dispose()
    { if (_failures.Count > 0) throw new Exception($"{_failures.Count} checks failed:\n  " + string.Join("\n  ", _failures)); }
}
