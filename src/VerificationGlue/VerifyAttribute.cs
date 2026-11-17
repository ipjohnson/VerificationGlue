using System;
using System.ComponentModel;

namespace VerificationGlue;

/// <summary>
/// Marks a method whose <c>Verify</c> and <c>Check</c> calls report captured values. With tier 3
/// rewriting enabled and the method eligible, the generator writes a copy named
/// <c>{Name}__Verified</c> and sends direct calls of the method to it.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class VerifyAttribute : Attribute
{
}

/// <summary>
/// Marks an attribute whose methods a runner always calls through their <c>__Verified</c> copy, such
/// as <c>[VerifyTest]</c> from <c>VerificationGlue.Tests</c>. A method with such an attribute is a
/// tier 3 candidate like a <c>[Verify]</c> method.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class VerifyRewriteAttribute : Attribute
{
}

/// <summary>
/// Marks a <c>__Verified</c> copy that the generator wrote. A call from another assembly is sent to a
/// copy only when the copy carries this attribute with a contract version the generator knows.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class VerifiedCopyAttribute : Attribute
{
    /// <summary>Creates the marker.</summary>
    /// <param name="contractVersion">The version of the rewrite contract the copy was written for.</param>
    public VerifiedCopyAttribute(int contractVersion)
    {
        ContractVersion = contractVersion;
    }

    /// <summary>The version of the rewrite contract the copy was written for.</summary>
    public int ContractVersion { get; }
}
