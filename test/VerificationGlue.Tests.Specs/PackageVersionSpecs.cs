using System.Diagnostics;
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
        var expected = typeof(PackageVersionSpecs).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var actual = FileVersionInfo.GetVersionInfo(Path.Combine(Fixture.OutputDirectory, assemblyName + ".dll")).ProductVersion;

        Assert.Equal(expected, actual);
    }
}
