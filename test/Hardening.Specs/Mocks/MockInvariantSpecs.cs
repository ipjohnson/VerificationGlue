namespace Hardening.Specs.Mocks;

/// <summary>
/// The mock invariants of docs/design/hardening.md, step 3, each checked on the generated sequences
/// of a fixed seed. <see cref="Invariants"/> has the check of each.
/// </summary>
[Trait("Step", "3")]
public class MockInvariantSpecs
{
    public const int DefaultCases = 400;

    public const string SpecName = "Each_mock_invariant_holds_on_a_fixed_seed_run";

    [VerifyTest]
    [Row("Setup and verification do not add received calls")]
    [Row("Verification does not consume response sequences")]
    [Row("Separate instances do not share configuration or recorded calls")]
    [Row("Reconfiguration follows the declared precedence rules")]
    [Row("Recursive mock identity and depth follow their contracts")]
    [Row("Matchers and pending setup state follow the documented async context rules")]
    [Row("Exceptions and abandoned setup state do not silently corrupt subsequent operations")]
    public async Task Each_mock_invariant_holds_on_a_fixed_seed_run(string invariant)
    {
        var settings = CampaignSettings.FromEnvironment(DefaultCases, SpecName);
        settings.RecordSeed();
        var check = Invariants.All.Single(entry => entry.Name == invariant).Check;
        var failures = new List<string>();
        var comparisons = 0;

        foreach (var index in settings.Indices)
        {
            var sequence = SequenceGenerator.Generate(settings.Seed, index, settings.Cases);
            var result = await check(sequence, new Random(unchecked(settings.Seed + index)));
            comparisons += result.Comparisons;
            if (result.Failure is { } failure)
            {
                failures.Add($"{sequence.Name} of seed {settings.Seed}: {failure}\n{sequence}\n  replay: {settings.ReplayCommand(index)}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} sequences break \"{invariant}\".\n\n" + string.Join("\n\n", failures.Take(3)));
        Assert.True(settings.Case is not null || comparisons > settings.Cases / 4, $"The check made only {comparisons} comparisons in {settings.Cases} sequences.");
    }

    [VerifyTest]
    public void The_list_of_invariants_matches_the_design()
    {
        var design = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "design", "hardening.md"));
        var section = design[design.IndexOf("The required invariants include:", StringComparison.Ordinal)..];
        var listed = section.Split('\n').Skip(2).TakeWhile(line => line.StartsWith("- ", StringComparison.Ordinal)).Select(line => line[2..].TrimEnd('.')).ToList();

        Assert.Equal(listed, Invariants.All.Select(entry => entry.Name));
    }

    private static string RepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "VerificationGlue.slnx")))
        {
            directory = Path.GetDirectoryName(directory)!;
        }

        return directory;
    }
}
