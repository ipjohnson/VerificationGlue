using System.Linq;
using BenchmarkDotNet.Running;

namespace Generator.Benchmarks;

public static class Program
{
    /// <summary>
    /// <c>budget &lt;file&gt;</c> checks the budgets in the file. Any other arguments go to BenchmarkDotNet.
    /// </summary>
    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "budget")
        {
            return Budgets.Check(args[1]);
        }

        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        var failed = false;
        foreach (var summary in summaries)
        {
            failed |= summary.HasCriticalValidationErrors || summary.Reports.Length == 0 || summary.Reports.Any(report => !report.Success);
        }

        return failed ? 1 : 0;
    }
}
