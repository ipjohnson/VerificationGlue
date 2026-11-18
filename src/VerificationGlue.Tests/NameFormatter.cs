using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VerificationGlue.Tests;

/// <summary>
/// Writes the display name of a test. <c>[NameFormatter&lt;T&gt;]</c> names the formatter. The display
/// name is only what a test explorer shows. The test ID does not depend on it.
/// </summary>
public interface ITestNameFormatter
{
    /// <summary>The display name of the test that <paramref name="context"/> describes.</summary>
    /// <param name="context">The test method and its row values.</param>
    static abstract string Format(in TestNameContext context);
}

/// <summary>What a name formatter knows about a test: its method, its parameters and its row values.</summary>
public readonly struct TestNameContext
{
    /// <summary>Creates a context.</summary>
    /// <param name="typeName">The full name of the test class, with <c>+</c> between nested types.</param>
    /// <param name="methodName">The test method's name.</param>
    /// <param name="parameters">The parameters of the test method.</param>
    /// <param name="arguments">The row values, or an empty list when the test has no rows.</param>
    public TestNameContext(string typeName, string methodName, IReadOnlyList<TestParameter> parameters, IReadOnlyList<object?> arguments)
    {
        TypeName = typeName;
        MethodName = methodName;
        Parameters = parameters;
        Arguments = arguments;
    }

    /// <summary>The full name of the test class, with <c>+</c> between nested types.</summary>
    public string TypeName { get; }

    /// <summary>The test method's name.</summary>
    public string MethodName { get; }

    /// <summary>The parameters of the test method.</summary>
    public IReadOnlyList<TestParameter> Parameters { get; }

    /// <summary>
    /// The row values, which go to the first parameters in order. Empty when the test has no rows.
    /// </summary>
    public IReadOnlyList<object?> Arguments { get; }
}

/// <summary>The base of <see cref="NameFormatterAttribute{T}"/>.</summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public abstract class NameFormatterAttribute : Attribute
{
    private protected NameFormatterAttribute()
    {
    }
}

/// <summary>
/// Writes the display names of these tests with <typeparamref name="T"/>. The generator uses the
/// narrowest one: the method's, then the class's, then the assembly's.
/// </summary>
/// <typeparam name="T">The formatter.</typeparam>
public class NameFormatterAttribute<T> : NameFormatterAttribute
    where T : ITestNameFormatter
{
}

/// <summary>
/// The display name when no <c>[NameFormatter&lt;T&gt;]</c> applies: the method name, followed by each row
/// value with its parameter name, such as <c>Totals(qty: 2, price: 10)</c>.
/// </summary>
public sealed class DefaultNameFormatter : ITestNameFormatter
{
    /// <inheritdoc/>
    public static string Format(in TestNameContext context)
    {
        if (context.Arguments.Count == 0)
        {
            return context.MethodName;
        }

        var text = new StringBuilder(context.MethodName).Append('(');
        for (var i = 0; i < context.Arguments.Count; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            if (i < context.Parameters.Count)
            {
                text.Append(context.Parameters[i].Name).Append(": ");
            }

            text.Append(FormatValue(context.Arguments[i]));
        }

        return text.Append(')').ToString();
    }

    /// <summary>
    /// A row value as the default name writes it: strings and characters in quotes, enums with their
    /// type name, numbers in the invariant culture, and arrays in brackets.
    /// </summary>
    /// <param name="value">The value.</param>
    public static string FormatValue(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        char character => "'" + (character is '\'' or '\\' ? "\\" : "") + character + "'",
        bool flag => flag ? "true" : "false",
        Enum member => member.GetType().Name + "." + member,
        Type type => "typeof(" + type.Name + ")",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable items => "[" + string.Join(", ", Items(items)) + "]",
        _ => value.ToString() ?? "",
    };

    private static IEnumerable<string> Items(IEnumerable items)
    {
        foreach (var item in items)
        {
            yield return FormatValue(item);
        }
    }
}
