using System.Reflection;

namespace VerificationGlue.Specs;

public class PackageVersionSpecs
{
    [VerifyTest]
    public void Runtime_has_the_repository_version()
    {
        Assert.Equal(VersionOf(typeof(PackageVersionSpecs).Assembly), VersionOf(Assembly.Load("VerificationGlue")));
    }

    private static string? VersionOf(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
