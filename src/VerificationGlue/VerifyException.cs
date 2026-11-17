using System;

namespace VerificationGlue;

/// <summary>Thrown when a <c>Verify</c>, <c>Throws</c> or <c>ThrowsAsync</c> call fails.</summary>
public sealed class VerifyException : Exception
{
    /// <summary>Creates the exception with the failure message.</summary>
    /// <param name="message">The failure message.</param>
    public VerifyException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the failure message and the exception that caused it.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    public VerifyException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
