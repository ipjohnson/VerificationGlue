using System.Linq;
using BenchmarkDotNet.Running;

namespace Generator.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        var failed = false;
        foreach (var summary in summaries)
        {
            failed |= summary.HasCriticalValidationErrors || summary.Reports.Length == 0 || summary.Reports.Any(report => !report.Success);
        }

        return failed ? 1 : 0;
    }
}
