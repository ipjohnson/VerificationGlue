namespace VerificationGlue.Mocks;

/// <summary>
/// Implemented by every generated mock. Gives setup and verification the mock's adapter.
/// </summary>
public interface IMock
{
    /// <summary>The adapter that records this mock's calls.</summary>
    MockAdapter Adapter { get; }
}
