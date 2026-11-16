using Generator.Harness.Sample;
using Microsoft.CodeAnalysis;

namespace Generator.Harness.Specs;

public class DescribeGeneratorSpecs
{
    private const string Person = """
        using Generator.Harness.Sample;

        namespace People;

        [Describe]
        public partial class Person
        {
            public string Name { get; set; } = "";
            public int Age { get; set; }

            public int Years() => Age;
        }
        """;

    private const string Other = """
        public class Other
        {
            public int Value { get; set; }
        }
        """;

    private static GeneratorDriverHarness CreateHarness() =>
        new GeneratorDriverHarness(new DescribeGenerator())
            .WithSource("Person.cs", Person)
            .WithSource("Other.cs", Other);

    [Fact]
    public void Generated_source_matches_snapshot_and_compiles()
    {
        var harness = CreateHarness();

        var run = harness.Run();

        GeneratorSnapshot.Match<DescribeGeneratorSpecs>(run);
        Assert.Empty(harness.OutputCompilationDiagnostics());
    }

    [Fact]
    public void Edit_to_an_unrelated_file_reruns_the_transform_and_leaves_the_output_cached()
    {
        var harness = CreateHarness();
        harness.Run();

        harness.WithSource("Other.cs", Other.Replace("Value", "Total", StringComparison.Ordinal));
        harness.Run();

        Assert.Equal([IncrementalStepRunReason.Unchanged], harness.StepReasons(DescribeGenerator.ModelStep));
        Assert.Equal([IncrementalStepRunReason.Cached], harness.StepReasons("SourceOutput"));
    }

    [Fact]
    public void Edit_to_a_method_body_reruns_the_transform_and_leaves_the_output_cached()
    {
        var harness = CreateHarness();
        harness.Run();

        harness.WithSource("Person.cs", Person.Replace("=> Age", "=> Age + 1", StringComparison.Ordinal));
        harness.Run();

        Assert.Equal([IncrementalStepRunReason.Unchanged], harness.StepReasons(DescribeGenerator.ModelStep));
        Assert.Equal([IncrementalStepRunReason.Cached], harness.StepReasons("SourceOutput"));
    }

    [Fact]
    public void New_property_modifies_the_model_and_regenerates_the_output()
    {
        var harness = CreateHarness();
        harness.Run();

        harness.WithSource("Person.cs", Person.Replace("public int Age", "public string Email { get; set; } = \"\";\n    public int Age", StringComparison.Ordinal));
        harness.Run();

        Assert.Equal([IncrementalStepRunReason.Modified], harness.StepReasons(DescribeGenerator.ModelStep));
        Assert.Equal([IncrementalStepRunReason.Modified], harness.StepReasons("SourceOutput"));
        Assert.Contains("Person(Name, Email, Age)", harness.LastRun.GeneratedTrees.Single(tree => tree.FilePath.EndsWith("People.Person.Describe.g.cs", StringComparison.Ordinal)).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Removing_the_attribute_removes_the_model_and_the_output()
    {
        var harness = CreateHarness();
        harness.Run();

        harness.WithSource("Person.cs", Person.Replace("[Describe]", "", StringComparison.Ordinal));
        harness.Run();

        Assert.Equal([IncrementalStepRunReason.Removed], harness.StepReasons(DescribeGenerator.ModelStep));
        Assert.Equal([IncrementalStepRunReason.Removed], harness.StepReasons("SourceOutput"));
        Assert.DoesNotContain(harness.LastRun.GeneratedTrees, tree => tree.FilePath.EndsWith("People.Person.Describe.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_tracking_name_lists_the_tracked_steps()
    {
        var harness = CreateHarness();
        harness.Run();

        var error = Assert.Throws<ArgumentException>(() => harness.StepReasons("Missing"));

        Assert.Contains(DescribeGenerator.ModelStep, error.Message, StringComparison.Ordinal);
    }
}
