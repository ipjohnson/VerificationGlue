using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Generator.Benchmarks;

/// <summary>
/// Times each budgeted scenario and compares its median with the limit in a budget file. Returns 1
/// when a scenario is over its limit or the file and the scenarios do not name the same set.
/// </summary>
public static class Budgets
{
    private const int Runs = 5;

    private const int InterfaceCount = 500;

    private const int MembersPerInterface = 20;

    public static int Check(string path)
    {
        var limits = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path} holds no budgets.");
        var scenarios = Scenarios();

        var unknown = limits.Keys.Except(scenarios.Keys).Concat(scenarios.Keys.Except(limits.Keys)).ToList();
        if (unknown.Count > 0)
        {
            Console.Error.WriteLine($"The budgets in {path} and the scenarios differ: {string.Join(", ", unknown)}.");
            return 1;
        }

        var failed = false;
        Console.WriteLine($"| Scenario | Median of {Runs} runs | Budget |");
        Console.WriteLine("|---|---|---|");
        foreach (var (name, scenario) in scenarios)
        {
            var median = Median(scenario);
            var over = median > limits[name];
            failed |= over;
            Console.WriteLine($"| {name} | {median:F0} ms | {limits[name]:F0} ms{(over ? " OVER" : "")} |");
        }

        return failed ? 1 : 0;
    }

    /// <summary>Each scenario prepares untimed work and returns the work to time.</summary>
    private static Dictionary<string, Func<Action>> Scenarios()
    {
        var edit = 0;
        var warm = MockWorkload.Create(InterfaceCount, MembersPerInterface);
        warm.Run();

        return new()
        {
            ["MockGenerator.ColdRun"] = () =>
            {
                var cold = MockWorkload.Create(InterfaceCount, MembersPerInterface);
                return () => cold.Run();
            },
            ["MockGenerator.RunAfterUnrelatedEdit"] = () =>
            {
                warm.WithSource("Unrelated.cs", MockWorkload.Unrelated(++edit));
                return () => warm.Run();
            },
            ["MockGenerator.CompileGeneratedCode"] = () => () => MockWorkload.Compile(warm),
        };
    }

    private static double Median(Func<Action> scenario)
    {
        scenario()();
        var times = new List<double>();
        for (var i = 0; i < Runs; i++)
        {
            var work = scenario();
            var watch = Stopwatch.StartNew();
            work();
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        return times[Runs / 2];
    }
}
