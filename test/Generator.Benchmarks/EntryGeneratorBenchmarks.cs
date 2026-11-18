using BenchmarkDotNet.Attributes;
using Generator.Harness;
using Microsoft.CodeAnalysis;

namespace Generator.Benchmarks;

/// <summary>
/// Times the entry generator over <see cref="EntryWorkload"/>: a cold run, a run after an edit to the
/// test project, and a run after an edit to the app, which hands the generator a new reference.
/// </summary>
[MemoryDiagnoser]
public class EntryGeneratorBenchmarks
{
    private readonly byte[][] appVersions = new byte[2][];
    private GeneratorDriverHarness cold = null!;
    private GeneratorDriverHarness warm = null!;
    private int edit;
    private int appEdit;

    [Params(200)]
    public int TestClassCount { get; set; }

    [Params(500)]
    public int AppTypeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        appVersions[0] = EntryWorkload.App(AppTypeCount, 0);
        appVersions[1] = EntryWorkload.App(AppTypeCount, 1);
        warm = EntryWorkload.Create(TestClassCount, MetadataReference.CreateFromImage(appVersions[0]));
        warm.Run();
        EntryWorkload.CheckGenerated(warm);
    }

    [IterationSetup(Target = nameof(ColdRun))]
    public void CreateColdHarness() => cold = EntryWorkload.Create(TestClassCount, MetadataReference.CreateFromImage(appVersions[0]));

    [Benchmark]
    public GeneratorDriverRunResult ColdRun() => cold.Run();

    [Benchmark]
    public GeneratorDriverRunResult RunAfterTestProjectEdit()
    {
        warm.WithSource("Unrelated.cs", EntryWorkload.Unrelated(++edit));
        return warm.Run();
    }

    /// <summary>Each run gets a new reference object, as the IDE gives one for each build of the app.</summary>
    [Benchmark]
    public GeneratorDriverRunResult RunAfterAppEdit()
    {
        warm.WithReference(EntryWorkload.AppName, MetadataReference.CreateFromImage(appVersions[++appEdit % 2]));
        return warm.Run();
    }
}
