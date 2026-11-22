using System.Diagnostics;
using System.Runtime.InteropServices;
using Hardening.Reports;

// Usage: Hardening.Reports --output <folder> --step <name> <profile> <outcome> <folder> [--step ...]
// Writes run-report.json and coverage.md to the output folder. Exits with 1 when the run is incomplete,
// when a step or a scenario failed, or when the report or the matrix has a problem. A step folder of -
// means the step writes no TRX file.
//
// Usage: Hardening.Reports gate --commit <sha> --output <folder> <run-report.json> ...
// Writes completion.md and completion.json. Exits with 1 when an inventory entry has no passing evidence
// in the reports made at the commit.
if (args is ["gate", ..])
{
    return Gate(args[1..]);
}

var output = "artifacts/hardening";
var steps = new List<StepInput>();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--output" when i + 1 < args.Length:
            output = args[++i];
            break;
        case "--step" when i + 4 < args.Length:
            steps.Add(new StepInput(args[i + 1], args[i + 2], args[i + 3], args[i + 4]));
            i += 4;
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument '{args[i]}'. Usage: --output <folder> --step <name> <profile> <outcome> <folder> ...");
            return 2;
    }
}

var root = RepositoryRoot();
var inventory = ContractInventory.Load(root);
var matrix = ConfigurationMatrix.Load(root);
var problems = matrix.Validate().Select(problem => $"{ConfigurationMatrix.RelativePath}: {problem}").ToList();
if (problems.Count > 0)
{
    problems.ForEach(Console.Error.WriteLine);
    return 1;
}

Directory.CreateDirectory(output);
var report = new RunReportBuilder(root, inventory, matrix).Build(steps, Run(root, "git", "rev-parse HEAD"), Environment(root), Os(), Location(), output);
File.WriteAllText(Path.Combine(output, RunReport.FileName), report.Json());
var coverage = new CoverageReport(report, matrix, inventory);
File.WriteAllText(Path.Combine(output, CoverageReport.FileName), coverage.Markdown());

problems.AddRange(report.Validate(inventory).Select(problem => $"{RunReport.FileName}: {problem}"));
problems.ForEach(Console.Error.WriteLine);
foreach (var reason in report.Incomplete)
{
    Console.Error.WriteLine($"Incomplete: {reason}");
}

foreach (var failure in coverage.Failures)
{
    Console.Error.WriteLine($"Failed: {failure.Id}: {failure.Detail}");
}

Console.WriteLine($"Wrote {Path.Combine(output, RunReport.FileName)} with {report.Scenarios.Count} entries, and {Path.Combine(output, CoverageReport.FileName)}. The run is {(report.Complete ? "complete" : "incomplete")}.");
return problems.Count == 0 && coverage.Passed ? 0 : 1;

static int Gate(string[] args)
{
    string? commit = null;
    var output = "artifacts/completion";
    var files = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--commit" when i + 1 < args.Length:
                commit = args[++i];
                break;
            case "--output" when i + 1 < args.Length:
                output = args[++i];
                break;
            case var file when !file.StartsWith("--", StringComparison.Ordinal):
                files.Add(file);
                break;
            default:
                Console.Error.WriteLine($"Unknown or incomplete argument '{args[i]}'. Usage: gate --commit <sha> --output <folder> <run-report.json> ...");
                return 2;
        }
    }

    if (commit is null)
    {
        Console.Error.WriteLine("The gate needs --commit, the commit the run checked out.");
        return 2;
    }

    var root = RepositoryRoot();
    var reports = files.Order(StringComparer.Ordinal).Select(file => (Path.GetRelativePath(root, Path.GetFullPath(file)).Replace('\\', '/'), RunReport.Parse(File.ReadAllText(file)))).ToList();
    var gate = new CompletionGate(ContractInventory.Load(root), ConfigurationMatrix.Load(root), commit).Decide(reports);
    Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output, CompletionGate.MarkdownFileName), gate.Markdown());
    File.WriteAllText(Path.Combine(output, CompletionGate.JsonFileName), gate.Json());
    foreach (var line in gate.Problems.Concat(gate.Rejected).Concat(gate.Entries.Where(entry => entry.Status != CompletionGate.Verified).Select(entry => $"{entry.Id} is {entry.Status}: {entry.Detail}")))
    {
        Console.Error.WriteLine(line);
    }

    Console.WriteLine($"{gate.Entries.Count(entry => entry.Status == CompletionGate.Verified)} of {gate.Entries.Count} inventory entries have passing evidence at {commit} in {reports.Count} run reports. The gate {(gate.Passed ? "passed" : "failed")}.");
    return gate.Passed ? 0 : 1;
}

static string RepositoryRoot()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "VerificationGlue.slnx")))
        {
            return directory.FullName;
        }
    }

    throw new InvalidOperationException($"No VerificationGlue.slnx above {Directory.GetCurrentDirectory()}.");
}

static RunEnvironment Environment(string root) => new()
{
    Os = RuntimeInformation.OSDescription,
    Architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
    Runtime = RuntimeInformation.FrameworkDescription,
    Sdk = Run(root, "dotnet", "--version"),
    Runner = System.Environment.GetEnvironmentVariable("ImageOS") is { Length: > 0 } image
        ? $"GitHub Actions {image} {System.Environment.GetEnvironmentVariable("ImageVersion")}".TrimEnd()
        : null,
};

// The value of the matrix's os factor for this machine, such as linux-x64.
static string Os()
{
    var platform = OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsWindows() ? "windows" : "other";
    return $"{platform}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";
}

static string? Location()
{
    var server = System.Environment.GetEnvironmentVariable("GITHUB_SERVER_URL");
    var repository = System.Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
    var run = System.Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
    var attempt = System.Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT");
    return server is null || repository is null || run is null ? null : $"{server}/{repository}/actions/runs/{run}/attempts/{attempt ?? "1"}";
}

static string Run(string directory, string command, string arguments)
{
    using var process = Process.Start(new ProcessStartInfo(command, arguments)
    {
        WorkingDirectory = directory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    }) ?? throw new InvalidOperationException($"Could not start {command}.");
    var text = process.StandardOutput.ReadToEnd().Trim();
    process.WaitForExit();
    return process.ExitCode == 0 ? text : throw new InvalidOperationException($"{command} {arguments} exited with {process.ExitCode}: {process.StandardError.ReadToEnd()}");
}
