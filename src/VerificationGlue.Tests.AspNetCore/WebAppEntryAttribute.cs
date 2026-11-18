using System;

namespace VerificationGlue.Tests.AspNetCore;

/// <summary>
/// Marks a partial class as the entry type of the app assembly <see cref="AssemblyName"/>. A generator
/// makes the class implement <see cref="IWebAppEntry"/>, so <c>[WebApp&lt;TEntry&gt;]</c> can name it.
/// </summary>
/// <param name="assemblyName">The name of the app's assembly, which the test project references.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WebAppEntryAttribute(string assemblyName) : Attribute
{
    /// <summary>The name of the app's assembly.</summary>
    public string AssemblyName { get; } = assemblyName;
}
