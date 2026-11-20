using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace Hardening.Lifecycle.Specs;

/// <summary>
/// test/Hardening.Lifecycle.Fixture, built once per run in the configuration of these specs, and run
/// as a process with <c>VG_INJECT</c> naming the faults.
/// </summary>
internal static class LifecycleFixture
{
    private static readonly Lazy<string> Built = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    private static string RepositoryRoot { get; } = FindRoot();

    private static string Configuration { get; } =
        typeof(LifecycleFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "VerificationGlue.Configuration").Value!;

    /// <summary>Runs the tests that <paramref name="filter"/> selects, with <c>VG_INJECT</c> set to <paramref name="inject"/>.</summary>
    public static FixtureRun Run(string inject, string filter)
    {
        var output = Built.Value;
        var directory = Directory.CreateTempSubdirectory("vg-lifecycle-");
        try
        {
            var log = Path.Combine(directory.FullName, "fixture.log");
            var executable = Path.Combine(output, OperatingSystem.IsWindows() ? "Hardening.Lifecycle.Fixture.exe" : "Hardening.Lifecycle.Fixture");
            var (exitCode, text) = Process(
                executable,
                output,
                new Dictionary<string, string> { ["VG_FIXTURE_LOG"] = log, ["VG_INJECT"] = inject },
                "--vg-filter", filter, "--report-trx", "--report-trx-filename", "fixture.trx", "--results-directory", directory.FullName);
            var trx = Path.Combine(directory.FullName, "fixture.trx");
            return new FixtureRun(
                exitCode,
                text,
                File.Exists(log) ? File.ReadAllLines(log) : [],
                File.Exists(trx) ? ReadTrx(trx) : new Dictionary<string, NodeResult>());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>The results by node, keyed by the class name, a slash and the test name.</summary>
    private static Dictionary<string, NodeResult> ReadTrx(string path)
    {
        XNamespace trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var document = XDocument.Load(path);
        var classes = document.Descendants(trx + "UnitTest").ToDictionary(
            test => test.Attribute("id")!.Value,
            test => test.Element(trx + "TestMethod")!.Attribute("className")!.Value);

        return document.Descendants(trx + "UnitTestResult").ToDictionary(
            test => classes[test.Attribute("testId")!.Value] + "/" + test.Attribute("testName")!.Value,
            test => new NodeResult(
                test.Attribute("outcome")!.Value,
                test.Descendants(trx + "Message").FirstOrDefault()?.Value ?? "",
                test.Descendants(trx + "StdOut").FirstOrDefault()?.Value ?? ""));
    }

    private static string Build()
    {
        var project = Path.Combine(RepositoryRoot, "test", "Hardening.Lifecycle.Fixture", "Hardening.Lifecycle.Fixture.csproj");
        var (exitCode, output) = Process("dotnet", RepositoryRoot, new Dictionary<string, string>(), "build", project, "-c", Configuration);
        Assert.True(exitCode == 0, "dotnet build of the fixture failed:\n" + output);
        return Path.Combine(RepositoryRoot, "test", "Hardening.Lifecycle.Fixture", "bin", Configuration, "net10.0");
    }

    private static (int ExitCode, string Output) Process(string fileName, string directory, IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // An MTP application started by these specs would otherwise take them for its dotnet test host.
        foreach (var variable in start.Environment.Keys.Where(key => key.StartsWith("TESTINGPLATFORM_", StringComparison.Ordinal)).ToList())
        {
            start.Environment.Remove(variable);
        }

        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        start.Environment["NO_COLOR"] = "1";
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var output = new StringBuilder();
        process.OutputDataReceived += (_, line) => { if (line.Data is not null) { lock (output) { output.AppendLine(line.Data); } } };
        process.ErrorDataReceived += (_, line) => { if (line.Data is not null) { lock (output) { output.AppendLine(line.Data); } } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"{fileName} {string.Join(' ', arguments)} did not finish in 2 minutes. Output so far:\n{output}");
        }

        process.WaitForExit();
        return (process.ExitCode, output.ToString());
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VerificationGlue.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No VerificationGlue.slnx above {AppContext.BaseDirectory}.");
    }
}

/// <summary>One run of the fixture: its exit code, its console output, its log and its TRX results.</summary>
internal sealed record FixtureRun(int ExitCode, string Output, string[] Log, IReadOnlyDictionary<string, NodeResult> Results)
{
    public override string ToString() =>
        $"Exit code {ExitCode}.\n\nLog:\n{string.Join('\n', Log)}\n\nResults:\n" +
        string.Join("\n", Results.Select(result => $"{result.Key}: {result.Value.Outcome} {result.Value.Message}")) +
        $"\n\nOutput:\n{Output}";
}

internal sealed record NodeResult(string Outcome, string Message, string Output);
