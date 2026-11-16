using System.Reflection;

namespace VerificationGlue.Shared;

internal static class GeneratorVersion
{
    /// <summary>
    /// The package version of the generator assembly that compiles this file.
    /// </summary>
    public static string Current { get; } =
        Normalize(typeof(GeneratorVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// Drops build metadata after '+', which is not part of a package version.
    /// </summary>
    public static string Normalize(string? informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion))
        {
            return "";
        }

        int plus = informationalVersion!.IndexOf('+');
        return plus < 0 ? informationalVersion : informationalVersion.Substring(0, plus);
    }
}
