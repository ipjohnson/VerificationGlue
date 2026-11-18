using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Runs <paramref name="action"/> and returns the exception it throws. Throws a
    /// <see cref="VerifyException"/> when it throws nothing, or throws an exception whose type is
    /// not exactly <typeparamref name="T"/>. A type derived from <typeparamref name="T"/> fails.
    /// </summary>
    /// <typeparam name="T">The exact exception type.</typeparam>
    /// <param name="action">The code that must throw.</param>
    /// <param name="expression">The source text of <paramref name="action"/>. The compiler fills it in.</param>
    public static T ThrowsExactly<T>(
        Action action,
        [CallerArgumentExpression(nameof(action))] string? expression = null)
        where T : Exception =>
        VerifyFailure.ThrowsExactly<T>(action, expression, null, null, 0);

    /// <summary>
    /// Awaits <paramref name="action"/> and returns the exception it throws. Throws a
    /// <see cref="VerifyException"/> when it throws nothing, or throws an exception whose type is
    /// not exactly <typeparamref name="T"/>. A type derived from <typeparamref name="T"/> fails.
    /// </summary>
    /// <typeparam name="T">The exact exception type.</typeparam>
    /// <param name="action">The code that must throw.</param>
    /// <param name="expression">The source text of <paramref name="action"/>. The compiler fills it in.</param>
    public static Task<T> ThrowsExactlyAsync<T>(
        Func<Task> action,
        [CallerArgumentExpression(nameof(action))] string? expression = null)
        where T : Exception =>
        VerifyFailure.ThrowsExactlyAsync<T>(action, expression, null, null, 0);

    /// <summary>
    /// Returns the only item of <paramref name="items"/>. Throws a <see cref="VerifyException"/>
    /// that gives the count and the first items when there are none or more than one.
    /// </summary>
    /// <remarks>It throws inside a <see cref="VerifyScope"/> too, because a failure has no item to return.</remarks>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items, of which there must be one.</param>
    /// <param name="expression">The source text of <paramref name="items"/>. The compiler fills it in.</param>
    public static T Single<T>(
        IEnumerable<T>? items,
        [CallerArgumentExpression(nameof(items))] string? expression = null) =>
        VerifyFailure.Single(items, expression, null, 0);

    /// <summary>
    /// Returns the only item of <paramref name="items"/> that matches <paramref name="predicate"/>.
    /// Throws a <see cref="VerifyException"/> that gives the count of matches and the first of them
    /// when none or more than one match.
    /// </summary>
    /// <remarks>It throws inside a <see cref="VerifyScope"/> too, because a failure has no item to return.</remarks>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items, of which one must match.</param>
    /// <param name="predicate">The test each item is matched with.</param>
    /// <param name="expression">The source text of <paramref name="items"/>. The compiler fills it in.</param>
    /// <param name="predicateExpression">The source text of <paramref name="predicate"/>. The compiler fills it in.</param>
    public static T Single<T>(
        IEnumerable<T>? items,
        Func<T, bool> predicate,
        [CallerArgumentExpression(nameof(items))] string? expression = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null) =>
        VerifyFailure.Single(items, predicate, expression, predicateExpression, null, 0);

    /// <summary>
    /// Returns <paramref name="value"/> as a <typeparamref name="T"/>. Throws a
    /// <see cref="VerifyException"/> that names the actual type when it is null or not a
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>It throws inside a <see cref="VerifyScope"/> too, because a failure has no value to return.</remarks>
    /// <typeparam name="T">The type the value must have, or a base type of it.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="expression">The source text of <paramref name="value"/>. The compiler fills it in.</param>
    public static T Is<T>(
        object? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null) =>
        VerifyFailure.Is<T>(value, expression, null, 0);

    /// <summary>Returns <paramref name="value"/> when it is not null, and throws a <see cref="VerifyException"/> when it is.</summary>
    /// <remarks>It throws inside a <see cref="VerifyScope"/> too, because a failure has no value to return.</remarks>
    /// <typeparam name="T">The reference type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="expression">The source text of <paramref name="value"/>. The compiler fills it in.</param>
    public static T NotNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
        where T : class =>
        VerifyFailure.NotNull(value, expression, null, 0);

    /// <summary>Returns the value of <paramref name="value"/> when it has one, and throws a <see cref="VerifyException"/> when it is null.</summary>
    /// <remarks>It throws inside a <see cref="VerifyScope"/> too, because a failure has no value to return.</remarks>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="expression">The source text of <paramref name="value"/>. The compiler fills it in.</param>
    public static T NotNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
        where T : struct =>
        VerifyFailure.NotNull(value, expression, null, 0);
}
