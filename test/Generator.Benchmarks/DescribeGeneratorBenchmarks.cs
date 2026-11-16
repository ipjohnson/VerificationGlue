using System.Linq;
using BenchmarkDotNet.Attributes;
using Generator.Harness;
using Generator.Harness.Sample;
using Microsoft.CodeAnalysis;

namespace Generator.Benchmarks;

/// <summary>
/// Times the sample generator over a project of marked classes: a cold run with a new driver, and a
/// warm run after an edit to an unrelated file.
/// </summary>
[MemoryDiagnoser]
public class DescribeGeneratorBenchmarks
{
    private GeneratorDriverHarness cold = null!;
    private GeneratorDriverHarness warm = null!;
    private int edit;

    [Params(100, 1_000)]
    public int ClassCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        warm = CreateHarness();
        warm.Run();
    }

    [IterationSetup(Target = nameof(ColdRun))]
    public void CreateColdHarness() => cold = CreateHarness();

    [Benchmark]
    public GeneratorDriverRunResult ColdRun() => cold.Run();

    [Benchmark]
    public GeneratorDriverRunResult RunAfterUnrelatedEdit()
    {
        warm.WithSource("Unrelated.cs", $"public class Unrelated {{ public int Value => {edit++}; }}");
        return warm.Run();
    }

    private GeneratorDriverHarness CreateHarness()
    {
        var harness = new GeneratorDriverHarness(new DescribeGenerator());
        foreach (var index in Enumerable.Range(0, ClassCount))
        {
            harness.WithSource(
                $"Model{index}.cs",
                $$"""
                namespace Models;

                [Generator.Harness.Sample.Describe]
                public partial class Model{{index}}
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }
                """);
        }

        return harness.WithSource("Unrelated.cs", "public class Unrelated { }");
    }
}
