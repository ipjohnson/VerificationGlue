using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using VerificationGlue.Generator;
using VerificationGlue.Mocks.Generator;

namespace VerificationGlue.Tests.Generator.Specs;

/// <summary>
/// Snapshots of the registrations for <c>CartTests</c> in <c>samples/TestsGettingStarted</c> and
/// <c>CheckoutTests</c> in <c>samples/TestsContainers</c>, with tier 3 rewriting enabled.
/// <c>docs/guide/generated/test-registrations.md</c> shows them.
/// </summary>
public class GeneratedCodeSpecs
{
    private static readonly Lazy<GeneratorDriverRunResult> GettingStarted = new(() => Generate("TestsGettingStarted", "CartTests.cs"));

    private static readonly Lazy<GeneratorDriverRunResult> Containers = new(() => Generate("TestsContainers", "Shop.cs", "Bootstraps.cs", "ContainerTests.cs", "ParameterTests.cs"));

    [VerifyTest]
    public void Registration_for_the_getting_started_sample() =>
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Registration_for_the_getting_started_sample), Source(GettingStarted, "TestsGettingStarted.CartTests.Tests.g.cs"));

    [VerifyTest]
    public void Assembly_attributes_for_the_getting_started_sample() =>
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Assembly_attributes_for_the_getting_started_sample), Source(GettingStarted, TestEmitter.AssemblyAttributesHintName));

    [VerifyTest]
    public void Registration_with_mocks_for_the_containers_sample() =>
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Registration_with_mocks_for_the_containers_sample), Source(Containers, "TestsContainers.CheckoutTests.Tests.g.cs"));

    private static string Source(Lazy<GeneratorDriverRunResult> result, string hintName) =>
        result.Value.Results.Single(result => result.Generator.GetGeneratorType() == typeof(TestGenerator))
            .GeneratedSources.Single(source => source.HintName == hintName).SourceText.ToString();

    private static GeneratorDriverRunResult Generate(string sample, params string[] files)
    {
        var input = GeneratorHarness.Compilation(
            files.Select(file => (file, File.ReadAllText(Path.Combine(GeneratorHarness.RepositoryRoot, "samples", sample, file)))),
            MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location));

        // Several tests share the result, so one test's token does not cancel it.
        var driver = GeneratorHarness.Driver(rewrite: true, new VerificationGlueGenerator(), new MockGenerator(), new TestGenerator())
            .RunGeneratorsAndUpdateCompilation(input, out var output, out _, CancellationToken.None);

        var errors = output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        return driver.GetRunResult();
    }
}
