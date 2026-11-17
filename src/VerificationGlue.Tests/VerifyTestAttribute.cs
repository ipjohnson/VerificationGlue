using System;

namespace VerificationGlue.Tests;

/// <summary>
/// Marks a test. The generator registers each public <c>[VerifyTest]</c> method, and the runner
/// calls its <c>{Name}__Verified</c> copy when tier 3 rewriting is enabled and the method is eligible.
/// </summary>
[VerifyRewrite]
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class VerifyTestAttribute : Attribute
{
}
