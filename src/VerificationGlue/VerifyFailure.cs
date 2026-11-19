using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VerificationGlue;

/// <summary>
/// Builds failure messages. <see cref="VerifySyntax"/> and the generated interceptors call it.
/// </summary>
/// <remarks>
/// A message has a first line with the kind of call and its source text. The lines after it are
/// indented: the message for the shape of the call, details from the run, the user's message, and
/// the file and line. A line that is not known is left out.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class VerifyFailure
{
    private const int MaxListedItems = 10;

    /// <summary>The failure of a <c>Verify</c> call whose message is known at compile time.</summary>
    /// <param name="expression">The source text of the condition.</param>
    /// <param name="message">The message for the shape of the condition, or null.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static VerifyException Static(string? expression, string? message, string? file, int line) =>
        new(Compose("Verify", expression, message, null, null, file, line));

    /// <summary>The failure of a <c>Verify</c> call with an interpolated message.</summary>
    /// <param name="expression">The source text of the condition.</param>
    /// <param name="message">The message for the shape of the condition, or null.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    /// <param name="userMessage">The interpolated message from the call.</param>
    public static VerifyException Static(string? expression, string? message, string? file, int line, ref VerifyMessageHandler userMessage) =>
        new(Compose("Verify", expression, message, null, userMessage.ToStringAndClear(), file, line));

    /// <summary>
    /// Records the failure of a <c>Check</c> call whose message is known at compile time in the
    /// current <see cref="VerifyScope"/>, or throws it when no scope is open.
    /// </summary>
    /// <param name="expression">The source text of the condition.</param>
    /// <param name="message">The message for the shape of the condition, or null.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static void CheckFailed(string? expression, string? message, string? file, int line) =>
        VerifyScope.Report(new VerifyException(Compose("Check", expression, message, null, null, file, line)));

    /// <summary>
    /// A tier 2 <c>Verify</c> inside a rewritten copy, where no interceptor can reach the call, such
    /// as in a lambda.
    /// </summary>
    /// <param name="condition">The condition that must hold.</param>
    /// <param name="expression">The source text of the condition.</param>
    /// <param name="message">The message for the shape of the condition, or null.</param>
    /// <param name="file">The file of the call.</param>
    /// <param name="line">The line of the call.</param>
    /// <returns>A value whose <see cref="VerifyResult.Because"/> does nothing here.</returns>
    public static VerifyResult Verify([DoesNotReturnIf(false)] bool condition, string? expression, string? message, string? file, int line)
    {
        if (!condition)
        {
            throw Static(expression, message, file, line);
        }

        return default;
    }

    /// <summary>A tier 2 <c>Verify</c> with an interpolated message inside a rewritten copy.</summary>
    /// <param name="condition">The condition that must hold.</param>
    /// <param name="userMessage">The interpolated message from the call. Its holes run only on failure.</param>
    /// <param name="expression">The source text of the condition.</param>
    /// <param name="message">The message for the shape of the condition, or null.</param>
    /// <param name="file">The file of the call.</param>
    /// <param name="line">The line of the call.</param>
    /// <returns>A value whose <see cref="VerifyResult.Because"/> does nothing here.</returns>
    public static VerifyResult Verify(
        [DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref VerifyMessageHandler userMessage,
        string? expression,
        string? message,
        string? file,
        int line)
    {
        if (!condition)
        {
            throw Static(expression, message, file, line, ref userMessage);
        }

        return default;
    }

    /// <summary>A tier 2 <c>Check</c> inside a rewritten copy, where no interceptor can reach the call.</summary>
    /// <param name="condition">The condition that should hold.</param>
    /// <param name="expression">The source text of the condition.</param>
    /// <param name="message">The message for the shape of the condition, or null.</param>
    /// <param name="file">The file of the call.</param>
    /// <param name="line">The line of the call.</param>
    public static void Check(bool condition, string? expression, string? message, string? file, int line)
    {
        if (!condition)
        {
            CheckFailed(expression, message, file, line);
        }
    }

    /// <summary>
    /// A tier 2 <c>Verify</c> of a lambda, for a call whose closure the generated code cannot read.
    /// The lambda runs once.
    /// </summary>
    /// <param name="condition">The lambda that returns the condition.</param>
    /// <param name="expression">The source text of the lambda's body.</param>
    /// <param name="message">The message for the shape of the body, or null.</param>
    /// <param name="file">The file of the call.</param>
    /// <param name="line">The line of the call.</param>
    public static void Verify(Func<bool> condition, string? expression, string? message, string? file, int line)
    {
        if (!condition())
        {
            throw Static(expression, message, file, line);
        }
    }

    /// <summary>
    /// A tier 2 <c>Check</c> of a lambda, for a call whose closure the generated code cannot read.
    /// The lambda runs once.
    /// </summary>
    /// <param name="condition">The lambda that returns the condition.</param>
    /// <param name="expression">The source text of the lambda's body.</param>
    /// <param name="message">The message for the shape of the body, or null.</param>
    /// <param name="file">The file of the call.</param>
    /// <param name="line">The line of the call.</param>
    public static void Check(Func<bool> condition, string? expression, string? message, string? file, int line)
    {
        if (!condition())
        {
            CheckFailed(expression, message, file, line);
        }
    }

    /// <summary>Starts the tier 3 failure of a <c>Verify</c> call. Only the failure path calls it.</summary>
    /// <param name="expression">The source text of the condition.</param>
    /// <param name="file">The file of the call.</param>
    /// <param name="line">The line of the call.</param>
    /// <returns>A builder that takes the captured values.</returns>
    public static VerifyFailureBuilder Start(string expression, string file, int line) => new("Verify", expression, file, line);

    /// <summary>Starts the tier 3 failure of a <c>Check</c> call. Only the failure path calls it.</summary>
    /// <param name="expression">The source text of the condition.</param>
    /// <param name="file">The file of the call.</param>
    /// <param name="line">The line of the call.</param>
    /// <returns>A builder that takes the captured values.</returns>
    public static VerifyFailureBuilder StartCheck(string expression, string file, int line) => new("Check", expression, file, line);

    /// <summary>Runs <paramref name="action"/> and returns the <typeparamref name="T"/> it throws.</summary>
    /// <typeparam name="T">The exception type, or a base type of it.</typeparam>
    /// <param name="action">The code that must throw.</param>
    /// <param name="expression">The source text of the action.</param>
    /// <param name="message">The message for the shape of the call, or null.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static T Throws<T>(Action action, string? expression, string? message, string? file, int line)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T expected)
        {
            return expected;
        }
        catch (Exception other)
        {
            throw WrongException<T>("Throws", other, expression, message, file, line);
        }

        throw NoException<T>("Throws", expression, message, file, line);
    }

    /// <summary>Awaits <paramref name="action"/> and returns the <typeparamref name="T"/> it throws.</summary>
    /// <typeparam name="T">The exception type, or a base type of it.</typeparam>
    /// <param name="action">The code that must throw.</param>
    /// <param name="expression">The source text of the action.</param>
    /// <param name="message">The message for the shape of the call, or null.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static async Task<T> ThrowsAsync<T>(Func<Task> action, string? expression, string? message, string? file, int line)
        where T : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (T expected)
        {
            return expected;
        }
        catch (Exception other)
        {
            throw WrongException<T>("ThrowsAsync", other, expression, message, file, line);
        }

        throw NoException<T>("ThrowsAsync", expression, message, file, line);
    }

    /// <summary>Runs <paramref name="action"/> and returns the exception it throws when its type is exactly <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The exact exception type.</typeparam>
    /// <param name="action">The code that must throw.</param>
    /// <param name="expression">The source text of the action.</param>
    /// <param name="message">The message for the shape of the call, or null.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static T ThrowsExactly<T>(Action action, string? expression, string? message, string? file, int line)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (Exception thrown)
        {
            if (thrown.GetType() == typeof(T))
            {
                return (T)thrown;
            }

            throw NotExactly<T>("ThrowsExactly", thrown, expression, message, file, line);
        }

        throw NoException<T>("ThrowsExactly", expression, message, file, line);
    }

    /// <summary>Awaits <paramref name="action"/> and returns the exception it throws when its type is exactly <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The exact exception type.</typeparam>
    /// <param name="action">The code that must throw.</param>
    /// <param name="expression">The source text of the action.</param>
    /// <param name="message">The message for the shape of the call, or null.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static async Task<T> ThrowsExactlyAsync<T>(Func<Task> action, string? expression, string? message, string? file, int line)
        where T : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception thrown)
        {
            if (thrown.GetType() == typeof(T))
            {
                return (T)thrown;
            }

            throw NotExactly<T>("ThrowsExactlyAsync", thrown, expression, message, file, line);
        }

        throw NoException<T>("ThrowsExactlyAsync", expression, message, file, line);
    }

    /// <summary>Returns the only item of <paramref name="items"/>.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items, of which there must be one.</param>
    /// <param name="expression">The source text of the items.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static T Single<T>(IEnumerable<T>? items, string? expression, string? file, int line)
    {
        if (items is null)
        {
            throw new VerifyException(Compose("Single", expression, "expected one item, but the items were null", null, null, file, line));
        }

        var found = new List<T>();
        var count = 0;
        foreach (var item in items)
        {
            if (count++ < MaxListedItems)
            {
                found.Add(item);
            }
        }

        if (count == 1)
        {
            return found[0];
        }

        throw new VerifyException(Compose("Single", expression, "expected one item, but found " + Found(found, count), null, null, file, line));
    }

    /// <summary>Returns the only item of <paramref name="items"/> that matches <paramref name="predicate"/>.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items, of which one must match.</param>
    /// <param name="predicate">The test each item is matched with.</param>
    /// <param name="expression">The source text of the items.</param>
    /// <param name="predicateExpression">The source text of the predicate.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static T Single<T>(IEnumerable<T>? items, Func<T, bool> predicate, string? expression, string? predicateExpression, string? file, int line)
    {
        var text = predicateExpression is null ? expression : expression + ", " + predicateExpression;
        if (items is null)
        {
            throw new VerifyException(Compose("Single", text, "expected one match, but the items were null", null, null, file, line));
        }

        var found = new List<T>();
        var count = 0;
        foreach (var item in items)
        {
            if (predicate(item) && count++ < MaxListedItems)
            {
                found.Add(item);
            }
        }

        if (count == 1)
        {
            return found[0];
        }

        throw new VerifyException(Compose("Single", text, "expected one match, but found " + Found(found, count), null, null, file, line));
    }

    /// <summary>Returns <paramref name="value"/> as a <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type the value must have, or a base type of it.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="expression">The source text of the value.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static T Is<T>(object? value, string? expression, string? file, int line)
    {
        if (value is T typed)
        {
            return typed;
        }

        var actual = value is null ? "null" : TypeName(value.GetType());
        throw new VerifyException(Compose("Is", expression, $"expected {TypeName(typeof(T))}, but it was {actual}", null, null, file, line));
    }

    /// <summary>Returns <paramref name="value"/> when it is not null.</summary>
    /// <typeparam name="T">The reference type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="expression">The source text of the value.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static T NotNull<T>([NotNull] T? value, string? expression, string? file, int line)
        where T : class =>
        value ?? throw new VerifyException(Compose("NotNull", expression, null, null, null, file, line));

    /// <summary>Returns the value of <paramref name="value"/> when it has one.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="expression">The source text of the value.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static T NotNull<T>([NotNull] T? value, string? expression, string? file, int line)
        where T : struct =>
        value ?? throw new VerifyException(Compose("NotNull", expression, null, null, null, file, line));

    /// <summary>Throws when a generated <c>VerifyEquivalent</c> comparer found any difference.</summary>
    /// <param name="differences">The differences the comparer found.</param>
    /// <param name="actualExpression">The source text of the actual value.</param>
    /// <param name="expectedExpression">The source text of the expected value.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static void Equivalent(EquivalenceDifferences differences, string? actualExpression, string? expectedExpression, string? file, int line)
    {
        if (differences.Count > 0)
        {
            throw new VerifyException(Compose("VerifyEquivalent", NotEquivalent(actualExpression, expectedExpression), differences.Describe(), null, null, file, line));
        }
    }

    /// <summary>
    /// A <c>VerifyEquivalent</c> call whose type the generated code cannot name, such as a private
    /// nested type. The two values compare with <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actualExpression">The source text of the actual value.</param>
    /// <param name="expectedExpression">The source text of the expected value.</param>
    /// <param name="file">The file of the call, or null.</param>
    /// <param name="line">The line of the call. Ignored when <paramref name="file"/> is null.</param>
    public static void EquivalentByEquals<T>(T actual, T expected, string? actualExpression, string? expectedExpression, string? file, int line)
    {
        var differences = new EquivalenceDifferences();
        differences.Equal("", "", actual, expected);
        Equivalent(differences, actualExpression, expectedExpression, file, line);
    }

    /// <summary>The <c>VerifyEquivalent</c> of a project without the generator, which compares with <see cref="EqualityComparer{T}.Default"/>.</summary>
    internal static void EquivalentWithoutGenerator<T>(T actual, T expected, string? actualExpression, string? expectedExpression)
    {
        var differences = new EquivalenceDifferences();
        differences.Equal("", "", actual, expected);
        if (differences.Count > 0)
        {
            throw new VerifyException(Compose(
                "VerifyEquivalent",
                NotEquivalent(actualExpression, expectedExpression),
                differences.Describe(),
                "compared with Equals, because the VerificationGlue generator did not run",
                null,
                null,
                0));
        }
    }

    private static string NotEquivalent(string? actualExpression, string? expectedExpression) =>
        (actualExpression ?? "actual") + " is not equivalent to " + (expectedExpression ?? "expected");

    private static string Found<T>(List<T> listed, int count)
    {
        var text = new StringBuilder(count.ToString(CultureInfo.InvariantCulture));
        if (count == 0)
        {
            return text.ToString();
        }

        text.Append(": [");
        for (var index = 0; index < listed.Count; index++)
        {
            text.Append(index == 0 ? "" : ", ").Append(VerifyFailureBuilder.Format(listed[index]));
        }

        return text.Append(count > listed.Count ? ", …]" : "]").ToString();
    }

    private static string TypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var tick = name.IndexOf('`');
        var arguments = Array.ConvertAll(type.GetGenericArguments(), TypeName);
        return (tick < 0 ? name : name.Substring(0, tick)) + "<" + string.Join(", ", arguments) + ">";
    }

    private static VerifyException NotExactly<T>(string kind, Exception thrown, string? expression, string? message, string? file, int line) =>
        thrown is T
            ? new(Compose(kind, expression, $"expected exactly {typeof(T).Name}, but {thrown.GetType().Name} was thrown", null, null, file, line), thrown)
            : WrongException<T>(kind, thrown, expression, message, file, line);

    private static VerifyException NoException<T>(string kind, string? expression, string? message, string? file, int line) =>
        new(Compose(kind, expression, message ?? Expected<T>(), "no exception was thrown", null, file, line));

    private static VerifyException WrongException<T>(string kind, Exception thrown, string? expression, string? message, string? file, int line) =>
        new(Compose(kind, expression, message ?? Expected<T>(), $"it threw {thrown.GetType().FullName}: {thrown.Message}", null, file, line), thrown);

    private static string Expected<T>() => "expected " + typeof(T).Name;

    /// <summary>The body of a parameterless lambda's source text, such as <c>x == 1</c> for <c>() =&gt; x == 1</c>.</summary>
    internal static string? LambdaBody(string? expression)
    {
        if (expression is null)
        {
            return null;
        }

        var text = expression.TrimStart();
        if (text.StartsWith("static ", StringComparison.Ordinal))
        {
            text = text.Substring("static ".Length).TrimStart();
        }

        if (!text.StartsWith("()", StringComparison.Ordinal))
        {
            return expression;
        }

        text = text.Substring(2).TrimStart();
        return text.StartsWith("=>", StringComparison.Ordinal) ? text.Substring(2).Trim() : expression;
    }

    internal static string Compose(string kind, string? expression, string? message, string? detail, string? userMessage, string? file, int line)
    {
        var text = new StringBuilder(kind).Append(" failed: ").Append(expression);
        AppendLine(text, message);
        AppendLine(text, detail);
        AppendLine(text, userMessage);
        if (file is not null)
        {
            AppendLine(text, $"at {file}:{line}");
        }

        return text.ToString();
    }

    private static void AppendLine(StringBuilder text, string? line)
    {
        if (!string.IsNullOrEmpty(line))
        {
            text.Append("\n  ").Append(line);
        }
    }
}
