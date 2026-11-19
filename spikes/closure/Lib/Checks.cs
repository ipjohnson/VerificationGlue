using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ClosureGlue;

public static class Checks
{
    /// <summary>The fallback when the generator does not intercept the call.</summary>
    public static void Verify(Func<bool> condition, [CallerArgumentExpression(nameof(condition))] string? text = null)
    {
        if (!condition())
        {
            throw new VerifyException("Verify failed: " + text);
        }
    }

    public static void Verify(bool condition, [CallerArgumentExpression(nameof(condition))] string? text = null)
    {
        if (!condition)
        {
            throw new VerifyException("Verify failed: " + text);
        }
    }
}

public sealed class VerifyException(string message) : Exception(message);

/// <summary>
/// Reads captured variables out of a lambda's closure. Roslyn stores a captured local or parameter in a
/// field of the same name on a display class. A display class for an inner scope points at the one for
/// the outer scope through a field named <c>CS$&lt;&gt;8__locals{n}</c>, and a captured <c>this</c> is the
/// field <c>&lt;&gt;4__this</c>. A lambda that captures only <c>this</c> is an instance method of the
/// containing type, so its target is the instance itself.
/// </summary>
public static class Closure
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "A missing field throws, and the caller falls back to the tier 2 message.")]
    public static T Read<T>(object? target, string name)
    {
        for (var scope = target; scope is not null; scope = Outer(scope))
        {
            if (scope.GetType().GetField(name, Fields) is { } field)
            {
                return (T)field.GetValue(scope)!;
            }
        }

        throw new MissingFieldException("no captured variable named " + name);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "A missing field throws, and the caller falls back to the tier 2 message.")]
    public static T This<T>(object? target)
    {
        for (var scope = target; scope is not null; scope = Outer(scope))
        {
            if (scope is T self)
            {
                return self;
            }

            if (scope.GetType().GetField("<>4__this", Fields) is { } field)
            {
                return (T)field.GetValue(scope)!;
            }
        }

        throw new MissingFieldException("no captured this");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "A missing field ends the search.")]
    private static object? Outer(object scope)
    {
        foreach (var field in scope.GetType().GetFields(Fields))
        {
            if (field.Name.StartsWith("CS$<>8__locals", StringComparison.Ordinal))
            {
                return field.GetValue(scope);
            }
        }

        return null;
    }
}

/// <summary>Records the value of each part of a condition as the instrumented copy runs.</summary>
public sealed class Explanation(string[] texts)
{
    private readonly object?[] _values = new object?[texts.Length];
    private readonly bool[] _set = new bool[texts.Length];

    public T R<T>(int slot, T value)
    {
        _values[slot] = value;
        _set[slot] = true;
        return value;
    }

    public string Render()
    {
        var width = texts.Max(text => text.Length);
        var builder = new StringBuilder();
        for (var i = 0; i < texts.Length; i++)
        {
            builder.Append("  ").Append(texts[i].PadRight(width)).Append(" → ")
                .Append(_set[i] ? Format(_values[i]) : "<not evaluated>").Append('\n');
        }

        return builder.ToString();
    }

    internal static string Format(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        bool flag => flag ? "true" : "false",
        _ => value.ToString() ?? "",
    };
}

/// <summary>
/// One captured variable read by one call site. The path to its field is found by reflection on the
/// first read and kept until a closure of another type arrives.
/// </summary>
public sealed class Capture(string name)
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private sealed record Resolved(Type Type, FieldInfo[] Path, FieldInfo Field);

    private Resolved? _resolved;

    public T Read<T>(object? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var resolved = _resolved;
        if (resolved is null || resolved.Type != target.GetType())
        {
            _resolved = resolved = Resolve(target);
        }

        var scope = target;
        foreach (var outer in resolved.Path)
        {
            scope = outer.GetValue(scope)!;
        }

        return (T)resolved.Field.GetValue(scope)!;
    }

    /// <summary>The captured <c>this</c>, which is the target itself when the lambda captures nothing else.</summary>
    public T This<T>(object? target) => target is T self ? self : Read<T>(target);

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "A missing field throws, and the call site falls back to running the delegate.")]
    private Resolved Resolve(object target)
    {
        var path = new List<FieldInfo>();
        for (object? scope = target; scope is not null;)
        {
            var type = scope.GetType();
            if (type.GetField(name, Fields) is { } field)
            {
                return new Resolved(target.GetType(), path.ToArray(), field);
            }

            var outer = type.GetFields(Fields).FirstOrDefault(candidate => candidate.Name.StartsWith("CS$<>8__locals", StringComparison.Ordinal));
            if (outer is null)
            {
                break;
            }

            path.Add(outer);
            scope = outer.GetValue(scope);
        }

        throw new MissingFieldException("no captured variable named " + name);
    }
}

public static class Slot
{
    public static T R<T>(ref T slot, ref bool set, T value)
    {
        slot = value;
        set = true;
        return value;
    }
}

/// <summary>Builds the message of a failed check from the typed slots, so values are boxed only on failure.</summary>
public sealed class FailureMessage(string text, string where)
{
    private readonly List<(string Part, string Value)> _lines = new();

    public FailureMessage Add<T>(string part, bool set, T value)
    {
        _lines.Add((part, set ? Explanation.Format(value) : "<not evaluated>"));
        return this;
    }

    public VerifyException Build()
    {
        var builder = new StringBuilder("Verify failed: ").Append(text).Append('\n');
        var width = _lines.Count == 0 ? 0 : _lines.Max(line => line.Part.Length);
        foreach (var (part, value) in _lines)
        {
            builder.Append("  ").Append(part.PadRight(width)).Append(" → ").Append(value).Append('\n');
        }

        return new VerifyException(builder.Append("  at ").Append(where).ToString());
    }
}
