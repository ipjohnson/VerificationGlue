using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace VerificationGlue;

/// <summary>
/// The assertions. Import them with <c>using static VerificationGlue.VerifySyntax;</c>.
/// </summary>
/// <remarks>
/// Each method here gives a message from the source text of its argument. With the generator
/// running, an interceptor replaces each call and adds the file, the line and a message for the
/// shape of the condition. In a method that tier 3 rewrites, the message also lists the values of
/// the condition's parts.
/// </remarks>
public static class VerifySyntax
{
    /// <summary>Throws a <see cref="VerifyException"/> when <paramref name="condition"/> is false.</summary>
    /// <param name="condition">The condition that must hold.</param>
    /// <param name="expression">The source text of <paramref name="condition"/>. The compiler fills it in.</param>
    /// <returns>A value whose <see cref="VerifyResult.Because"/> adds a reason in tier 3.</returns>
    public static VerifyResult Verify(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
        {
            throw VerifyFailure.Static(expression, null, null, 0);
        }

        return default;
    }

    /// <summary>
    /// Throws a <see cref="VerifyException"/> that includes <paramref name="message"/> when
    /// <paramref name="condition"/> is false. The holes of the message run only on failure.
    /// </summary>
    /// <param name="condition">The condition that must hold.</param>
    /// <param name="message">An interpolated string added to the failure message.</param>
    /// <param name="expression">The source text of <paramref name="condition"/>. The compiler fills it in.</param>
    /// <returns>A value whose <see cref="VerifyResult.Because"/> adds a reason in tier 3.</returns>
    public static VerifyResult Verify(
        [DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref VerifyMessageHandler message,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
        {
            throw VerifyFailure.Static(expression, null, null, 0, ref message);
        }

        return default;
    }

    /// <summary>
    /// Records a failure in the current <see cref="VerifyScope"/> when <paramref name="condition"/>
    /// is false, and returns so the caller continues. With no open scope it throws a
    /// <see cref="VerifyException"/> like <c>Verify</c>.
    /// </summary>
    /// <param name="condition">The condition that should hold.</param>
    /// <param name="expression">The source text of <paramref name="condition"/>. The compiler fills it in.</param>
    public static void Check(
        bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
        {
            VerifyFailure.CheckFailed(expression, null, null, 0);
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> and returns the exception it throws. Throws a
    /// <see cref="VerifyException"/> when it throws nothing, or throws an exception that is not a
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The exception type, or a base type of it.</typeparam>
    /// <param name="action">The code that must throw.</param>
    /// <param name="expression">The source text of <paramref name="action"/>. The compiler fills it in.</param>
    public static T Throws<T>(
        Action action,
        [CallerArgumentExpression(nameof(action))] string? expression = null)
        where T : Exception =>
        VerifyFailure.Throws<T>(action, expression, null, null, 0);

    /// <summary>
    /// Awaits <paramref name="action"/> and returns the exception it throws. Throws a
    /// <see cref="VerifyException"/> when it throws nothing, or throws an exception that is not a
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The exception type, or a base type of it.</typeparam>
    /// <param name="action">The code that must throw.</param>
    /// <param name="expression">The source text of <paramref name="action"/>. The compiler fills it in.</param>
    public static Task<T> ThrowsAsync<T>(
        Func<Task> action,
        [CallerArgumentExpression(nameof(action))] string? expression = null)
        where T : Exception =>
        VerifyFailure.ThrowsAsync<T>(action, expression, null, null, 0);
}
