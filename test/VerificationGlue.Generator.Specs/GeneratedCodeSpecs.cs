using Generator.Harness;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Snapshots of generated code for real sample files. <c>docs/guide/generated/interceptors.md</c>
/// shows these snapshots.
/// </summary>
public class GeneratedCodeSpecs
{
    [Fact]
    public void Interceptors_for_the_xunit_sample()
    {
        var samples = Path.Combine(RepositoryRoot(), "samples");
        var compilation = GeneratorHarness.Compilation(
            (GeneratorHarness.ProjectDirectory + "OrderTests.cs", File.ReadAllText(Path.Combine(samples, "VerifyInXunit", "OrderTests.cs"))),
            ("/repo/Shop/Shop.cs", File.ReadAllText(Path.Combine(samples, "Shop", "Shop.cs"))),
            ("/repo/Project/ImplicitUsings.cs", "global using System; global using System.Threading.Tasks;"));

        var result = GeneratorHarness.Generate(compilation, new VerificationGlueGenerator(), out _);

        var generated = Assert.Single(result.GeneratedTrees);
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Interceptors_for_the_xunit_sample), generated.GetText(TestContext.Current.CancellationToken).ToString());
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VerificationGlue.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No VerificationGlue.slnx above {AppContext.BaseDirectory}.");
    }
}
