using System.Reflection;
using VerificationGlue.Shared;

namespace Shared.Generator.Specs;

public class GeneratorVersionSpecs
{
    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-preview.4+abc123", "1.2.3-preview.4")]
    [InlineData(null, "")]
    public void Normalize_drops_build_metadata(string? informational, string expected)
    {
        Assert.Equal(expected, GeneratorVersion.Normalize(informational));
    }

    [Fact]
    public void Current_is_the_version_of_the_compiling_assembly()
    {
        string? expected = typeof(GeneratorVersionSpecs).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.False(string.IsNullOrEmpty(GeneratorVersion.Current));
        Assert.Equal(expected, GeneratorVersion.Current);
    }
}
