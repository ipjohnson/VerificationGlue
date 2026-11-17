namespace VerificationGlue;

/// <summary>
/// What a <c>Verify</c> call returns, so a reason can follow it:
/// <c>Verify(order.Total == 40).Because("the discount applies");</c>.
/// </summary>
/// <remarks>
/// The reason reaches the message only in a method that tier 3 rewrites. Anywhere else
/// <c>Verify</c> throws before the chained call runs, and analyzer VG0001 reports the call.
/// </remarks>
public readonly struct VerifyResult
{
    /// <summary>Adds a reason to the failure message. Tier 3 moves it into the message.</summary>
    /// <param name="reason">Why the condition must hold.</param>
    public void Because(string reason)
    {
    }

    /// <summary>Adds text to the failure message. Tier 3 moves it into the message.</summary>
    /// <param name="message">The text to add.</param>
    public void WithMessage(string message)
    {
    }
}
