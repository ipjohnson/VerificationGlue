using System.Globalization;

namespace Hardening.Specs.Mocks;

/// <summary>A sequence the model or the interpreter cannot run. It is a fault of the harness, not an observation.</summary>
public sealed class HarnessException(string message) : Exception(message);

/// <summary>
/// The parts of a sequence that both the reference model and the interpreter read: argument values,
/// predicates, factory results, exception names and the text of a call. They define the input, not
/// the behavior under test.
/// </summary>
public static class Inputs
{
    public enum Result
    {
        Int,
        String,
        Task,
        ValueTask,
        Bool,
        Void,
        Child,
        Leaf,
    }

    public static Result ResultOf(Member member) => member switch
    {
        Member.Name or Member.EchoString => Result.String,
        Member.Load => Result.Task,
        Member.Peek => Result.ValueTask,
        Member.TryGet => Result.Bool,
        Member.Reset or Member.Swap => Result.Void,
        Member.Child => Result.Child,
        Member.ChildLeaf or Member.LeafNext => Result.Leaf,
        _ => Result.Int,
    };

    /// <summary>The number of parameters, and whether they hold strings.</summary>
    public static (int Count, bool Strings) ParametersOf(Member member) => member switch
    {
        Member.Get2 or Member.TryGet => (2, false),
        Member.Name or Member.EchoString => (1, true),
        Member.Count or Member.ChildLeaf or Member.LeafNext => (0, false),
        _ => (1, false),
    };

    /// <summary>The name a recorded call has, as the generated mock passes it to its adapter.</summary>
    public static string NameOf(Member member) => member switch
    {
        Member.Get2 or Member.Get1 => "Get",
        Member.EchoInt => "Echo<Int32>",
        Member.EchoString => "Echo<String>",
        Member.Index => "get_Item",
        Member.Count => "get_Count",
        Member.Load => "LoadAsync",
        Member.Peek => "PeekAsync",
        Member.ChildValue or Member.LeafValue => "Value",
        Member.ChildLeaf => "get_Leaf",
        Member.LeafNext => "get_Next",
        _ => member.ToString(),
    };

    /// <summary>Members that share a generated member, so a <c>Calls()</c> count lists the calls of both.</summary>
    public static string GroupOf(Member member) => member is Member.EchoInt or Member.EchoString ? "Echo" : member.ToString();

    public static object? Parse(Member member, string? value) =>
        ParametersOf(member).Strings ? value : int.Parse(value!, CultureInfo.InvariantCulture);

    public static object? ParseResult(Member member, string? value) => ResultOf(member) switch
    {
        Result.String => value,
        Result.Bool => bool.Parse(value!),
        _ => int.Parse(value!, CultureInfo.InvariantCulture),
    };

    /// <summary>The argument a statement passes for <paramref name="arg"/>: the value, or the default a matcher call returns.</summary>
    public static object? Passed(Member member, ArgSpec arg) =>
        arg.Kind == ArgKind.Exact ? Parse(member, arg.Value) : ParametersOf(member).Strings ? null : 0;

    public static bool Holds(Pred pred, object? value) => pred switch
    {
        Pred.Even => value is int number && number % 2 == 0,
        Pred.AboveOne => value is int number && number > 1,
        _ => string.IsNullOrEmpty(value as string),
    };

    public static bool Matches(Member member, ArgSpec arg, object? value) => arg.Kind switch
    {
        ArgKind.Any => true,
        ArgKind.Pred => Holds(arg.Pred, value),
        _ => Equals(Parse(member, arg.Value), value),
    };

    /// <summary>
    /// What a factory response computes from the arguments it is given: the sum of the int arguments
    /// plus 100, a string with <c>!</c> added, or for <c>TryGet</c> the out argument set to the key plus
    /// 100 and true.
    /// </summary>
    public static object? Factory(Member member, object?[] arguments)
    {
        switch (ResultOf(member))
        {
            case Result.String:
                return (arguments[0] as string ?? "null") + "!";
            case Result.Bool:
                arguments[1] = (int)arguments[0]! + 100;
                return true;
            default:
                return arguments.OfType<int>().Sum() + 100;
        }
    }

    public static InvalidOperationException Error(int id) => new("r" + id.ToString(CultureInfo.InvariantCulture));

    public static string Show(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        bool flag => flag ? "true" : "false",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };

    /// <summary>The text of an exception: the ID of one a response threw, or its type and message.</summary>
    public static string Show(Exception exception) =>
        exception is InvalidOperationException { Message: ['r', ..] } ? exception.Message : exception.GetType().Name + ": " + exception.Message;

    /// <summary>The text of a recorded call, as <c>CallRecord</c> writes it, with a handler written as its ID.</summary>
    public static string CallText(string name, IEnumerable<object?> arguments, Func<object, string> handler) =>
        name + "(" + string.Join(", ", arguments.Select(argument => argument switch
        {
            null => "null",
            Delegate value => handler(value),
            _ => Convert.ToString(argument, CultureInfo.InvariantCulture),
        })) + ")";

    /// <summary>
    /// The observation of statements in parallel flows. Which call takes which value of a response
    /// sequence depends on the order the calls run, so the callbacks that ran and each part of an
    /// outcome, such as the result and the out value of TryGet, are sorted on their own.
    /// </summary>
    public static string Parallel(IEnumerable<string> outcomes)
    {
        var callbacks = new List<string>();
        var rest = new List<string>();
        foreach (var outcome in outcomes)
        {
            var text = outcome;
            if (outcome.StartsWith('[') && outcome.IndexOf("] ", StringComparison.Ordinal) is var end and > 0)
            {
                callbacks.AddRange(outcome[1..end].Split(' '));
                text = outcome[(end + 2)..];
            }

            rest.AddRange(text.Split(", "));
        }

        callbacks.Sort(StringComparer.Ordinal);
        rest.Sort(StringComparer.Ordinal);
        return "parallel: " + string.Join(" | ", rest) + (callbacks.Count == 0 ? "" : "; callbacks " + string.Join(" ", callbacks));
    }

    /// <summary>
    /// The observation of a failed check: the count, the number of calls the message lists, and how
    /// many of those it marks as matching. When the message leaves calls out, which calls it lists can
    /// depend on the order parallel flows recorded them, so it gives the number left out instead of
    /// the marks.
    /// </summary>
    public static string CheckFailure(int received, int listed, int marks, int more) => more > 0
        ? string.Create(CultureInfo.InvariantCulture, $"check fails: received {received}, lists {listed} and {more} more")
        : string.Create(CultureInfo.InvariantCulture, $"check fails: received {received}, lists {listed}, marks {marks}");

    /// <summary>Sorts the calls inside each range that parallel flows recorded, whose order is not part of the contract.</summary>
    public static IEnumerable<string> Normalized(IReadOnlyList<string> calls, IEnumerable<(int Start, int End)> ranges)
    {
        var result = calls.ToArray();
        foreach (var (start, end) in ranges)
        {
            if (start < end && end <= result.Length)
            {
                Array.Sort(result, start, end - start, StringComparer.Ordinal);
            }
        }

        return result;
    }
}
