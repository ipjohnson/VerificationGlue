namespace VerificationGlue.Tests.Specs;

/// <summary>
/// Runs samples/TestsLifecycle and compares the steps it logs with
/// Snapshots/LifecycleSampleSpecs.Logs_every_step.verified.txt, which docs/guide/test-lifecycle.md shows.
/// </summary>
public class LifecycleSampleSpecs
{
    [Fact]
    public void Logs_every_step()
    {
        var project = Path.Combine(Fixture.RepositoryRoot, "samples", "TestsLifecycle", "TestsLifecycle.csproj");
        var build = Fixture.Dotnet(Fixture.RepositoryRoot, "build", project, "-c", Fixture.Configuration);
        Assert.True(build.ExitCode == 0, build.Output);

        // dotnet test on the solution runs the sample too, so this run uses a copy with its own lifecycle.log.
        var output = Path.Combine(Fixture.RepositoryRoot, "samples", "TestsLifecycle", "bin", Fixture.Configuration, "net10.0");
        var copy = Directory.CreateTempSubdirectory("vg-lifecycle-");
        try
        {
            Copy(new DirectoryInfo(output), copy);
            var run = Fixture.Dotnet(copy.FullName, Path.Combine(copy.FullName, "TestsLifecycle.dll"));
            Assert.True(run.ExitCode == 0, run.Output);

            var verified = Path.Combine(Fixture.RepositoryRoot, "test", "VerificationGlue.Tests.Specs", "Snapshots", "LifecycleSampleSpecs.Logs_every_step.verified.txt");
            Assert.Equal(File.ReadAllText(verified).Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(copy.FullName, "lifecycle.log")));
        }
        finally
        {
            copy.Delete(recursive: true);
        }
    }

    private static void Copy(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var file in source.GetFiles().Where(file => file.Name != "lifecycle.log"))
        {
            file.CopyTo(Path.Combine(target.FullName, file.Name));
        }

        foreach (var directory in source.GetDirectories())
        {
            Copy(directory, target.CreateSubdirectory(directory.Name));
        }
    }
}
