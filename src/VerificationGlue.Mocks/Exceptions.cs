using System;

namespace VerificationGlue.Mocks;

/// <summary>Thrown by a <c>Received</c> or <c>DidNotReceive</c> check that does not hold.</summary>
public sealed class ReceivedCallsException(string message) : Exception(message);

/// <summary>Thrown when the queued <c>Arg</c> matchers cannot be given to the arguments of a mock call.</summary>
public sealed class AmbiguousArgumentsException(string message) : Exception(message);

/// <summary>Thrown when a setup has no mock call to apply to, such as <c>Returns</c> on a value that no mock returned.</summary>
public sealed class MockSetupException(string message) : Exception(message);
