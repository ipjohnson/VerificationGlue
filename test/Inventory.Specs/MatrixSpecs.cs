namespace Inventory.Specs;

public class MatrixSpecs
{
    public const string ExclusionsHeading = "#### Excluded pairs";

    private static readonly ConfigurationMatrix Small = new()
    {
        Factors =
        [
            new Factor { Name = "os", Values = ["linux", "mac", "windows"] },
            new Factor { Name = "runtime", Values = ["jit", "aot"] },
            new Factor { Name = "host", Values = ["a", "b", "c"] },
        ],
        Exclusions =
        [
            new Exclusion { Pair = ["os=mac", "runtime=aot"], Reason = "r" },
            new Exclusion { Pair = ["runtime=aot", "host=c"], Reason = "r" },
        ],
        Profiles = [new Profile { Name = "p", Project = "test/Inventory.Specs", Values = new Dictionary<string, string> { ["runtime"] = "aot", ["host"] = "b" }, Os = ["linux"] }],
    };

    [VerifyTest]
    public void The_checked_in_matrix_is_valid()
    {
        var matrix = ConfigurationMatrix.Load(RepositoryRoot.Path);

        Assert.Empty(matrix.Validate());
        Assert.All(matrix.Profiles, profile => Assert.True(Directory.Exists(Path.Combine(RepositoryRoot.Path, profile.Project)), $"{profile.Project} does not exist"));
    }

    [VerifyTest]
    public void The_pairwise_selection_of_the_checked_in_matrix_holds_every_valid_pair_and_no_excluded_pair()
    {
        AssertPairwise(ConfigurationMatrix.Load(RepositoryRoot.Path));
    }

    [VerifyTest]
    public void The_excluded_pairs_in_the_design_doc_match_the_matrix()
    {
        var matrix = ConfigurationMatrix.Load(RepositoryRoot.Path);
        var rows = MarkdownTable.Read(File.ReadAllText(Path.Combine(RepositoryRoot.Path, "docs/design/hardening.md")), ExclusionsHeading);

        Assert.NotNull(rows);
        Assert.Equal(
            matrix.Exclusions.Select(exclusion => $"{exclusion.Pair[0]} | {exclusion.Pair[1]} | {exclusion.Reason}"),
            rows.Select(row => string.Join(" | ", row)));
    }

    [VerifyTest]
    public void A_selection_starts_with_the_profiles_and_holds_every_valid_pair()
    {
        var selection = Small.Select();

        Assert.Equal(new Combination(["os=linux", "runtime=aot", "host=b"], "p").Key, selection[0].Key);
        Assert.Equal("p", selection[0].Origin);
        AssertPairwise(Small);
    }

    [VerifyTest]
    public void The_valid_pairs_leave_out_each_excluded_pair()
    {
        var pairs = Small.ValidPairs().Select(pair => pair.ToString()).ToList();

        Assert.Equal(3 * 2 + 3 * 3 + 2 * 3 - 2, pairs.Count);
        Assert.DoesNotContain("os=mac runtime=aot", pairs);
        Assert.DoesNotContain("runtime=aot host=c", pairs);
    }

    [VerifyTest]
    public void An_exclusion_needs_two_known_values_of_two_factors_and_a_reason()
    {
        var matrix = Small with
        {
            Exclusions =
            [
                new Exclusion { Pair = ["os=mac", "os=linux"], Reason = "r" },
                new Exclusion { Pair = ["os=bsd", "runtime=jit"], Reason = "r" },
                new Exclusion { Pair = ["host=a", "runtime=jit"], Reason = " " },
                new Exclusion { Pair = ["host=a"], Reason = "r" },
            ],
        };

        Assert.Equal(
            [
                "the exclusion os=mac os=linux names two values of one factor",
                "the exclusion os=bsd runtime=jit names the unknown value os=bsd",
                "the exclusion host=a runtime=jit has no reason",
                "the exclusion host=a does not name two values",
            ],
            matrix.Validate());
    }

    [VerifyTest]
    public void A_profile_must_give_each_factor_but_os_a_listed_value_and_avoid_the_excluded_pairs()
    {
        var matrix = Small with
        {
            Profiles =
            [
                new Profile { Name = "p", Project = "x", Values = new Dictionary<string, string> { ["runtime"] = "aot", ["host"] = "c" }, Os = ["linux"] },
                new Profile { Name = "q", Project = "x", Values = new Dictionary<string, string> { ["runtime"] = "fast", ["os"] = "linux" }, Os = ["bsd"] },
            ],
        };

        Assert.Equal(
            [
                "the profile p on linux has the excluded pair runtime=aot host=c",
                "the profile q gives runtime=fast, which the matrix does not list",
                "the profile q gives no host",
                "the profile q gives os, which is not a factor it can set",
                "the profile q runs on bsd, which the matrix does not list",
            ],
            matrix.Validate());
    }

    private static void AssertPairwise(ConfigurationMatrix matrix)
    {
        var selection = matrix.Select();

        var missing = matrix.ValidPairs().Where(pair => !selection.Any(combination => combination.Covers(pair))).ToList();
        Assert.True(missing.Count == 0, "Pairs in no combination: " + string.Join(", ", missing));
        var excluded = selection.Where(combination => matrix.Exclusions.Any(exclusion => exclusion.Pair.All(combination.Settings.Contains))).ToList();
        Assert.True(excluded.Count == 0, "Combinations with an excluded pair: " + string.Join(", ", excluded.Select(combination => combination.Key)));
        Assert.Equal(selection.Count, selection.Select(combination => combination.Key).Distinct().Count());
    }
}
