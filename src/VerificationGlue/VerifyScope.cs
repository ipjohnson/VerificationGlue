using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VerificationGlue;

/// <summary>
/// Collects the failures of <c>Check</c> calls. Disposing the scope throws one
/// <see cref="VerifyException"/> that lists every failure, or nothing when there is none.
/// </summary>
/// <remarks>
/// The scope flows with the async context, so a <c>Check</c> in a method that the scope's owner
/// calls or awaits reports to it. A scope begun inside another reports its own failures when it
/// ends, and the outer scope becomes current again. <c>VerificationGlue.Tests</c> opens one scope
/// per test. Other hosts open one with <see cref="Begin"/>.
/// </remarks>
public sealed class VerifyScope : IDisposable
{
    private static readonly AsyncLocal<VerifyScope?> CurrentScope = new();

    private static int _watching;

    private readonly VerifyScope? _parent;
    private readonly List<VerifyException> _failures = new();
    private Exception? _thrown;
    private bool _ended;

    private VerifyScope(VerifyScope? parent)
    {
        _parent = parent;
    }

    /// <summary>The innermost open scope of the current async context, or null.</summary>
    public static VerifyScope? Current => CurrentScope.Value;

    /// <summary>The failures recorded so far, in the order the checks failed.</summary>
    public IReadOnlyList<VerifyException> Failures
    {
        get
        {
            lock (_failures)
            {
                return _failures.ToArray();
            }
        }
    }

    /// <summary>Opens a scope and makes it current.</summary>
    /// <returns>The scope. Dispose it to report its failures.</returns>
    public static VerifyScope Begin()
    {
        if (Interlocked.Exchange(ref _watching, 1) == 0)
        {
            AppDomain.CurrentDomain.FirstChanceException += (_, thrown) =>
            {
                if (CurrentScope.Value is { _ended: false } current)
                {
                    Volatile.Write(ref current._thrown, thrown.Exception);
                }
            };
        }

        var scope = new VerifyScope(CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    /// <summary>
    /// Ends the scope. Throws a <see cref="VerifyException"/> that lists every recorded failure when
    /// there is one. When the scope ends because code in it threw, that exception is the
    /// <see cref="Exception.InnerException"/>. A second call does nothing.
    /// </summary>
    public void Dispose()
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        if (CurrentScope.Value == this)
        {
            CurrentScope.Value = _parent;
        }

        var failures = Failures;
        if (failures.Count > 0)
        {
            throw new VerifyException(Compose(failures), Unwinding() ? Volatile.Read(ref _thrown) : null);
        }
    }

    /// <summary>
    /// Whether an exception is passing through the caller, as when <see cref="Dispose"/> runs in the
    /// <c>finally</c> of a <c>using</c> whose body threw. Native AOT cannot tell, so it answers false.
    /// </summary>
    private static bool Unwinding()
    {
        try
        {
            return Marshal.GetExceptionPointers() != IntPtr.Zero;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Records <paramref name="failure"/> in the current scope, or throws it when no scope is open,
    /// so a failure is never lost.
    /// </summary>
    internal static void Report(VerifyException failure)
    {
        var scope = CurrentScope.Value;
        if (scope is null || scope._ended)
        {
            throw failure;
        }

        lock (scope._failures)
        {
            scope._failures.Add(failure);
        }
    }

    private static string Compose(IReadOnlyList<VerifyException> failures)
    {
        var text = new StringBuilder();
        text.Append(failures.Count.ToString(CultureInfo.InvariantCulture))
            .Append(failures.Count == 1 ? " check failed" : " checks failed");
        foreach (var failure in failures)
        {
            text.Append("\n\n").Append(failure.Message);
        }

        return text.ToString();
    }
}
