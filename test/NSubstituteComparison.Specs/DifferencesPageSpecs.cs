namespace NSubstituteComparison.Specs;

/// <summary>
/// <c>docs/guide/nsubstitute-differences.md</c> is generated from the comparison run. Run with
/// <c>VERIFICATIONGLUE_ACCEPT_DIFFERENCES=1</c> to write a fresh page.
/// </summary>
public class DifferencesPageSpecs
{
    private const string AcceptVariable = "VERIFICATIONGLUE_ACCEPT_DIFFERENCES";

    [Fact]
    public async Task The_page_matches_a_fresh_comparison_run()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, DifferencesPage.PagePath);
        var fresh = DifferencesPage.Render(await Comparison.RunAll(), Comparison.NSubstituteVersion, root);
        var page = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";

        if (fresh == page)
        {
            return;
        }

        if (Environment.GetEnvironmentVariable(AcceptVariable) == "1")
        {
            File.WriteAllText(path, fresh);
            return;
        }

        Assert.Fail($"{DifferencesPage.PagePath} differs from a fresh comparison run. Rerun with {AcceptVariable}=1 to update it.\n\nA fresh run gives:\n{fresh}");
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
