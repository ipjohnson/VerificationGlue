using System.Reflection;

namespace VerificationGlue.Tests.Specs;

public class PackageVersionSpecs
{
    [Theory]
    [InlineData("VerificationGlue")]
    [InlineData("VerificationGlue.Mocks")]
    [InlineData("VerificationGlue.Tests")]
    public void Runtimes_share_one_version(string assemblyName)
    {
        Assert.Equal(VersionOf(typeof(PackageVersionSpecs).Assembly), VersionOf(Assembly.Load(assemblyName)));
    }

    private static string? VersionOf(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
