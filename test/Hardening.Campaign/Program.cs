using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Hardening.Campaign;
using Hardening.Mutations;
using Hardening.Reports;

// Usage: Hardening.Campaign [--output <folder>] [--seed <n>] [--config <file>] [--only <run>] ...
// Runs each generator of docs/design/hardening-campaign.json with its case count and the seed, then the
// targeted mutations, and stops each run at its limit. Writes campaign.json, campaign.md and issues.json to
// the output folder. The seed is the UTC date, such as 20261123, unless --seed gives one. Exits with 1 when
// a run failed or did not finish.
var output = "artifacts/campaign";
int? seedArgument = null;
string? configPath = null;
var only = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--output" when i + 1 < args.Length:
            output = args[++i];
            break;
        case "--seed" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed):
            seedArgument = parsed;
            i++;
            break;
        case "--config" when i + 1 < args.Length:
            configPath = args[++i];
            break;
        case "--only" when i + 1 < args.Length:
            only.Add(args[++i]);
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument '{args[i]}'. Usage: [--output <folder>] [--seed <n>] [--config <file>] [--only <run>] ...");
            return 2;
    }
}

var root = RepositoryRoot();
var config = configPath is null ? CampaignConfig.Load(root) : CampaignConfig.Parse(File.ReadAllText(configPath));
var problems = config.Validate();
if (only.FirstOrDefault(name => name != CampaignConfig.MutationsName && config.Generators.All(generator => generator.Name != name)) is { } unknown)
{
    problems = [.. problems, $"{unknown} is not a run of the campaign."];
}

if (problems.Count > 0)
{
    problems.ToList().ForEach(problem => Console.Error.WriteLine($"{configPath ?? CampaignConfig.RelativePath}: {problem}"));
    return 2;
}

var seed = seedArgument ?? CampaignConfig.SeedFor(DateOnly.FromDateTime(DateTime.UtcNow));
output = Path.GetFullPath(output);
Directory.CreateDirectory(output);
var logs = Path.Combine(output, "logs");
var runs = new List<CampaignRun>();
var generators = config.Generators.Where(generator => only.Count == 0 || only.Contains(generator.Name)).ToList();
Console.WriteLine($"Seed {seed}.");

if (generators.Count > 0)
{
    var build = await RunAsync("build", ["build", CampaignConfig.SpecsProject, "-c", "Release", "-nologo"], TimeSpan.FromMinutes(30), new Dictionary<string, string>());
    foreach (var generator in generators)
    {
        CampaignRun run;
        if (build.ExitCode != 0 || build.Stopped)
        {
            run = CampaignRun.NotRun(generator, seed, $"{CampaignConfig.SpecsProject} did not build, and logs/build.log has the output");
        }
        else
        {
            var directory = Path.Combine(output, generator.Name);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            var process = await RunAsync(
                generator.Name,
                ["test", "--project", CampaignConfig.SpecsProject, "-c", "Release", "--no-build", "--vg-filter", "name:" + generator.Test,
                 "--report-trx", "--report-trx-filename", "results.trx", "--results-directory", directory],
                TimeSpan.FromMinutes(generator.LimitMinutes),
                new Dictionary<string, string>
                {
                    ["HARDENING_SEED"] = seed.ToString(CultureInfo.InvariantCulture),
                    ["HARDENING_CASES"] = generator.Cases.ToString(CultureInfo.InvariantCulture),
                    ["HARDENING_OUTPUT"] = directory,
                });
            var trxPath = Path.Combine(directory, "results.trx");
            var seedPath = Path.Combine(directory, RunReportBuilder.SeedsDirectory, generator.Test + ".json");
            run = CampaignRun.ForGenerator(
                generator,
                seed,
                process,
                File.Exists(trxPath) ? TrxFile.Read(trxPath) : null,
                File.Exists(seedPath) ? JsonSerializer.Deserialize<SeedRun>(File.ReadAllText(seedPath), JsonOptions()) : null,
                Directory.Exists(directory)
                    ? [.. Directory.EnumerateFiles(directory, "*.txt").Order(StringComparer.Ordinal).Select(file => Path.GetRelativePath(output, file).Replace('\\', '/'))]
                    : []);
        }

        runs.Add(run);
        Console.WriteLine(Line(run));
    }
}

if (only.Count == 0 || only.Contains(CampaignConfig.MutationsName))
{
    var directory = Path.Combine(output, CampaignConfig.MutationsName);
    var process = await RunAsync(
        CampaignConfig.MutationsName,
        ["run", "--project", "test/Hardening.Mutations", "-c", "Release", "--", "--output", directory],
        TimeSpan.FromMinutes(config.Mutations.LimitMinutes),
        new Dictionary<string, string>());
    var reportPath = Path.Combine(directory, MutationReport.JsonFileName);
    var run = CampaignRun.ForMutations(
        config.Mutations.LimitMinutes,
        process,
        File.Exists(reportPath) ? JsonSerializer.Deserialize<MutationReport>(File.ReadAllText(reportPath), JsonOptions()) : null,
        MutationCatalog.Load(root).Mutations.Count);
    runs.Add(run);
    Console.WriteLine(Line(run));
}

var report = new CampaignReport
{
    Commit = Commit(root),
    Date = DateTimeOffset.UtcNow,
    Seed = seed,
    Location = Location(),
    Environment = $"{RuntimeInformation.OSDescription}, {RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}, {RuntimeInformation.FrameworkDescription}"
        + (Environment.GetEnvironmentVariable("ImageOS") is { Length: > 0 } image ? $", GitHub Actions {image} {Environment.GetEnvironmentVariable("ImageVersion")}".TrimEnd() : ""),
    Runs = runs,
};
File.WriteAllText(Path.Combine(output, CampaignReport.JsonFileName), report.Json());
File.WriteAllText(Path.Combine(output, CampaignReport.MarkdownFileName), report.Markdown(config));
File.WriteAllText(Path.Combine(output, CampaignReport.IssuesFileName), report.IssuesJson(config, file => File.Exists(Path.Combine(output, file)) ? File.ReadAllText(Path.Combine(output, file)) : null));
Console.WriteLine($"Wrote {Path.Combine(output, CampaignReport.MarkdownFileName)}. Coverage is {(report.Complete ? "complete" : "incomplete")}.");
return report.Passed ? 0 : 1;

async Task<ProcessOutcome> RunAsync(string name, string[] arguments, TimeSpan limit, Dictionary<string, string> environment)
{
    var log = Path.Combine(logs, name + ".log");
    Directory.CreateDirectory(logs);
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = root,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    arguments.ToList().ForEach(start.ArgumentList.Add);
    foreach (var variable in new[] { "HARDENING_SEED", "HARDENING_CASES", "HARDENING_CASE", "HARDENING_OUTPUT" })
    {
        start.Environment.Remove(variable);
    }

    foreach (var (key, value) in environment)
    {
        start.Environment[key] = value;
    }

    var text = new StringBuilder();
    using var process = new Process { StartInfo = start };
    process.OutputDataReceived += (_, line) => Append(text, line.Data);
    process.ErrorDataReceived += (_, line) => Append(text, line.Data);
    var clock = Stopwatch.StartNew();
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    using var cancellation = new CancellationTokenSource(limit);
    var stopped = false;
    try
    {
        await process.WaitForExitAsync(cancellation.Token);
    }
    catch (OperationCanceledException)
    {
        stopped = true;
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
    }

    clock.Stop();
    lock (text)
    {
        File.WriteAllText(log, $"dotnet {string.Join(' ', arguments)}\n\n{text}");
    }

    return new ProcessOutcome(stopped ? -1 : process.ExitCode, stopped, clock.Elapsed);
}

static void Append(StringBuilder text, string? line)
{
    if (line is null)
    {
        return;
    }

    lock (text)
    {
        text.AppendLine(line);
    }
}

static string Line(CampaignRun run) =>
    string.Create(CultureInfo.InvariantCulture, $"{run.Name}: {run.Status} in {run.ElapsedSeconds:0.0} s of {run.LimitMinutes} min")
    + (run.Incomplete.Count > 0 ? "; " + string.Join("; ", run.Incomplete) : "")
    + (run.Failures.Count > 0 ? "; " + string.Join("; ", run.Failures.Select(failure => failure.Key)) : "");

static JsonSerializerOptions JsonOptions() => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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

static string Commit(string root)
{
    using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root, RedirectStandardOutput = true })
        ?? throw new InvalidOperationException("Could not start git.");
    var text = process.StandardOutput.ReadToEnd().Trim();
    process.WaitForExit();
    return process.ExitCode == 0 ? text : throw new InvalidOperationException($"git rev-parse HEAD exited with {process.ExitCode}.");
}

static string? Location()
{
    var server = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL");
    var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
    var run = Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
    return server is null || repository is null || run is null ? null : $"{server}/{repository}/actions/runs/{run}";
}
