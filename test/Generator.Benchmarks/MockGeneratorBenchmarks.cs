using BenchmarkDotNet.Attributes;
using Generator.Harness;
using Microsoft.CodeAnalysis;

namespace Generator.Benchmarks;

/// <summary>Times the mock generator over <see cref="MockWorkload"/>: a cold run, a run after an unrelated edit, and the compile of its output.</summary>
[MemoryDiagnoser]
public class MockGeneratorBenchmarks
{
    private GeneratorDriverHarness cold = null!;
    private GeneratorDriverHarness warm = null!;
    private int edit;

    [Params(500)]
    public int InterfaceCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        warm = MockWorkload.Create(InterfaceCount, 20);
        warm.Run();
    }

    [IterationSetup(Target = nameof(ColdRun))]
    public void CreateColdHarness() => cold = MockWorkload.Create(InterfaceCount, 20);

    [Benchmark]
    public GeneratorDriverRunResult ColdRun() => cold.Run();

    [Benchmark]
    public GeneratorDriverRunResult RunAfterUnrelatedEdit()
    {
        warm.WithSource("Unrelated.cs", MockWorkload.Unrelated(++edit));
        return warm.Run();
    }

    [Benchmark]
    public void CompileGeneratedCode() => MockWorkload.Compile(warm);
}
