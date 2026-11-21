using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Hardening.Reports;

namespace Hardening.Mutations;

// Builds and tests a copy of the repository. The suite is every test project of VerificationGlue.slnx and
// the two hardening projects outside it. Each command's output goes to a log file.
public sealed partial class Suite(string repository, string logs)
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(20);

    private static readonly string[] Builds =
    [
        "VerificationGlue.slnx",
        "test/Hardening.Specs",
        "test/Hardening.Lifecycle.Specs",
        "test/Hardening.Lifecycle.Fixture",
    ];

    private static readonly string[] OutsideTheSolution = ["test/Hardening.Specs", "test/Hardening.Lifecycle.Specs"];

    // Copies the files git tracks or would track, including uncommitted changes, so the run never touches the checkout.
    public static void Copy(string root, string destination)
    {
        var listing = Run(root, "git", ["ls-files", "-z", "--cached", "--others", "--exclude-standard"]);
        foreach (var file in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var source = Path.Combine(root, file);
            if (!File.Exists(source))
            {
                continue;
            }

            var target = Path.Combine(destination, file);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }

    // Null when every build passes, and otherwise the command that failed.
    public async Task<string?> BuildAsync(string label)
    {
        // A compiler server that outlives a build can keep the generator assembly it loaded before the mutation.
        await DotnetAsync(label, "build-server", ["build-server", "shutdown"], BuildTimeout);
        foreach (var target in Builds)
        {
            var (exitCode, timedOut) = await DotnetAsync(label, "build " + target, ["build", target, "-c", "Release", "-nologo"], BuildTimeout);
            if (timedOut || exitCode != 0)
            {
                return $"dotnet build {target} {(timedOut ? "timed out" : $"exited with {exitCode}")}. {Path.Combine(logs, label)} has the output.";
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<RunResult>> TestAsync(string label, string results)
    {
        var nunit = SolutionProjects().Where(UsesNUnit).ToList();
        var filter = Path.Combine(repository, "suite.slnf");
        File.WriteAllText(filter, JsonSerializer.Serialize(new
        {
            solution = new { path = "VerificationGlue.slnx", projects = SolutionProjects().Except(nunit).ToArray() },
        }));

        var runs = new List<RunResult>
        {
            await TestAsync(label, "solution", ["--solution", filter], Path.Combine(results, "solution")),
        };
        foreach (var project in nunit)
        {
            runs.Add(await TestAsync(label, Path.GetDirectoryName(project)!, ["--project", project], trx: null));
        }

        foreach (var project in OutsideTheSolution)
        {
            runs.Add(await TestAsync(label, project, ["--project", project], Path.Combine(results, Path.GetFileName(project))));
        }

        return runs;
    }

    private async Task<RunResult> TestAsync(string label, string name, string[] target, string? trx)
    {
        string[] arguments = ["test", .. target, "-c", "Release", "--no-build"];
        if (trx is not null)
        {
            arguments = [.. arguments, "--report-trx", "--results-directory", trx];
        }

        var (exitCode, timedOut) = await DotnetAsync(label, "test " + name, arguments, TestTimeout);
        var cases = trx is not null && Directory.Exists(trx)
            ? Directory.EnumerateFiles(trx, "*.trx", SearchOption.AllDirectories).SelectMany(file => TrxFile.Read(file).Results).Select(Case).ToList()
            : [];
        var consoleFailures = trx is null && exitCode != 0
            ? FailedLine().Matches(Ansi().Replace(File.ReadAllText(LogPath(label, "test " + name)), "")).Select(match => $"{Path.GetFileName(name)}: {match.Groups["name"].Value}").ToList()
            : [];
        return new RunResult(name, exitCode, timedOut, cases, consoleFailures);
    }

    private static CaseResult Case(TrxResult result) =>
        new(result.Name.StartsWith(result.ClassName + ".", StringComparison.Ordinal) ? result.Name : $"{result.ClassName}.{result.Name}", result.Outcome);

    private IEnumerable<string> SolutionProjects() =>
        XDocument.Load(Path.Combine(repository, "VerificationGlue.slnx")).Descendants("Project").Select(project => project.Attribute("Path")!.Value);

    // NUnit3TestAdapter 5.2.0 does not start with the TRX extension. Directory.Build.targets explains why.
    private bool UsesNUnit(string project) =>
        File.ReadAllText(Path.Combine(repository, project)).Contains("\"NUnit3TestAdapter\"", StringComparison.Ordinal);

    private string LogPath(string label, string step) =>
        Path.Combine(logs, label, string.Concat(step.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '-')) + ".log");

    private async Task<(int ExitCode, bool TimedOut)> DotnetAsync(string label, string step, string[] arguments, TimeSpan timeout)
    {
        var log = LogPath(label, step);
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        arguments.ToList().ForEach(start.ArgumentList.Add);
        start.Environment["VerificationGlueReportTrx"] = "true";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "true";
        start.Environment["DOTNET_NOLOGO"] = "true";

        var output = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, line) => Append(output, line.Data);
        process.ErrorDataReceived += (_, line) => Append(output, line.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cancellation = new CancellationTokenSource(timeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        lock (output)
        {
            File.WriteAllText(log, $"dotnet {string.Join(' ', arguments)}\n\n{output}");
        }

        return (timedOut ? -1 : process.ExitCode, timedOut);
    }

    private static void Append(StringBuilder output, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }

    internal static string Run(string directory, string command, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(command)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        arguments.ToList().ForEach(start.ArgumentList.Add);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {command}.");
        var error = process.StandardError.ReadToEndAsync();
        var text = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? text : throw new InvalidOperationException($"{command} exited with {process.ExitCode}: {error.Result}");
    }

    // A failed test in the console output of Microsoft.Testing.Platform, such as "failed Adds_two (3ms)".
    [GeneratedRegex(@"^\s*failed (?<name>\S.*?) \(\d[^)]*\)\s*$", RegexOptions.Multiline)]
    private static partial Regex FailedLine();

    [GeneratedRegex(@"\x1b\[[0-9;]*[A-Za-z]")]
    private static partial Regex Ansi();
}
