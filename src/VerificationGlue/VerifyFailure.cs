using System;
using System.ComponentModel;
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

    private static VerifyException NoException<T>(string kind, string? expression, string? message, string? file, int line) =>
        new(Compose(kind, expression, message ?? Expected<T>(), "no exception was thrown", null, file, line));

    private static VerifyException WrongException<T>(string kind, Exception thrown, string? expression, string? message, string? file, int line) =>
        new(Compose(kind, expression, message ?? Expected<T>(), $"it threw {thrown.GetType().FullName}: {thrown.Message}", null, file, line), thrown);

    private static string Expected<T>() => "expected " + typeof(T).Name;

    private static string Compose(string kind, string? expression, string? message, string? detail, string? userMessage, string? file, int line)
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
