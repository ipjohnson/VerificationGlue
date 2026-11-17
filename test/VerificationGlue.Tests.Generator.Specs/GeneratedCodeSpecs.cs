using Generator.Harness;
using Microsoft.CodeAnalysis;
using VerificationGlue.Generator;
using VerificationGlue.Mocks.Generator;

namespace VerificationGlue.Tests.Generator.Specs;

/// <summary>
/// Snapshots of the registration for <c>CartTests</c> in <c>samples/TestsGettingStarted</c>, with tier 3
/// rewriting enabled. <c>docs/guide/generated/test-registrations.md</c> shows them.
/// </summary>
public class GeneratedCodeSpecs
{
    private static readonly Lazy<GeneratorDriverRunResult> Result = new(Generate);

    [Fact]
    public void Registration_for_the_getting_started_sample() =>
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Registration_for_the_getting_started_sample), Source("TestsGettingStarted.CartTests.Tests.g.cs"));

    [Fact]
    public void Assembly_attributes_for_the_getting_started_sample() =>
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Assembly_attributes_for_the_getting_started_sample), Source(TestEmitter.AssemblyAttributesHintName));

    private static string Source(string hintName) =>
        Result.Value.Results.Single(result => result.Generator.GetGeneratorType() == typeof(TestGenerator))
            .GeneratedSources.Single(source => source.HintName == hintName).SourceText.ToString();

    private static GeneratorDriverRunResult Generate()
    {
        var path = Path.Combine(GeneratorHarness.RepositoryRoot, "samples", "TestsGettingStarted", "CartTests.cs");
        var input = GeneratorHarness.Compilation([("CartTests.cs", File.ReadAllText(path))]);

        var driver = GeneratorHarness.Driver(rewrite: true, new VerificationGlueGenerator(), new MockGenerator(), new TestGenerator())
            .RunGeneratorsAndUpdateCompilation(input, out var output, out _, TestContext.Current.CancellationToken);

        var errors = output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        return driver.GetRunResult();
    }
}
