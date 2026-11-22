using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hardening.Reports;

// The completion gate of docs/design/hardening.md. It reads the run reports of one CI run and decides each
// inventory entry. An entry is verified when the reports made at the current commit have at least one entry
// for it, every one of them passed, and they ran each evidence reference on every OS that the reference's
// profile in the matrix lists. A report made at another commit does not count, so a pass in an earlier run
// never verifies an entry.
public sealed class CompletionGate(ContractInventory inventory, ConfigurationMatrix matrix, string commit)
{
    public const string MarkdownFileName = "completion.md";
    public const string JsonFileName = "completion.json";

    public const string Verified = "verified";
    public const string Failed = "failed";
    public const string Unverified = "unverified";

    public IReadOnlyList<string> Problems { get; private set; } = [];

    public IReadOnlyList<string> Rejected { get; private set; } = [];

    public IReadOnlyList<GateSource> Sources { get; private set; } = [];

    public IReadOnlyList<GateEntry> Entries { get; private set; } = [];

    public bool Passed => Problems.Count == 0 && Entries.All(entry => entry.Status == Verified);

    public CompletionGate Decide(IReadOnlyList<(string Source, RunReport Report)> reports)
    {
        var problems = new List<string>();
        var rejected = new List<string>();
        var current = new List<(string Source, RunReport Report)>();
        foreach (var (source, report) in reports)
        {
            problems.AddRange(report.Validate(inventory).Select(problem => $"{source}: {problem}"));
            if (report.Commit != commit)
            {
                rejected.Add($"{source} was made at {report.Commit}, not at the current commit {commit}, so its entries do not count.");
                continue;
            }

            current.Add((source, report));
        }

        if (reports.Count == 0)
        {
            problems.Add("The gate read no run report.");
        }

        var runs = current.SelectMany(report => report.Report.Scenarios.Where(entry => entry.Commit == commit)).ToList();
        Entries = [.. inventory.Scenarios.Select(scenario => Decide(scenario, [.. runs.Where(entry => entry.Id == scenario.Id)]))];
        Sources = [.. current.Select(report => new GateSource(report.Source, report.Report.Commit, report.Report.Complete, report.Report.Scenarios.Count, report.Report.Location))];
        Problems = problems;
        Rejected = rejected;
        return this;
    }

    private GateEntry Decide(Scenario scenario, IReadOnlyList<ScenarioRun> runs)
    {
        var configurations = runs.Select(run => string.Join(" ", run.Configuration)).Distinct().ToList();
        GateEntry Entry(string status, string? detail) => new(scenario.Id, scenario.Support, status, configurations, detail);

        if (scenario.Support is not ("supported" or "unsupported"))
        {
            return Entry(Unverified, $"its support status is {scenario.Support}, so it is neither a supported contract nor a tested boundary");
        }

        if (runs.Count == 0)
        {
            return Entry(Unverified, "no run report of this commit has an entry for it");
        }

        var failed = runs.Where(run => run.Status == ScenarioStatus.Failed).ToList();
        if (failed.Count > 0)
        {
            return Entry(Failed, string.Join("; ", failed.Select(run => $"{string.Join(" ", run.Configuration)}: {run.Detail}")));
        }

        var blocked = runs.Where(run => run.Status != ScenarioStatus.Passed).ToList();
        if (blocked.Count > 0)
        {
            return Entry(Unverified, string.Join("; ", blocked.Select(run => $"{string.Join(" ", run.Configuration)}: {run.Detail}")));
        }

        var missing = new List<string>();
        foreach (var reference in scenario.Evidence)
        {
            if (matrix.Profiles.FirstOrDefault(profile => profile.ProjectOf(reference) is not null) is not { } profile)
            {
                missing.Add($"{reference}, which no profile of the matrix runs");
                continue;
            }

            missing.AddRange(profile.Os
                .Where(os => !runs.Any(run => run.Configuration.SequenceEqual(matrix.Configuration(profile, os)) && run.Tests.Any(test => Names(reference, test.Test))))
                .Select(os => $"{reference} on {os}"));
        }

        return missing.Count > 0
            ? Entry(Unverified, $"no step of this commit ran {string.Join(", ", missing)}")
            : Entry(Verified, null);
    }

    // Whether a test of a run report is the reference or, for a .cs file, one of the tests it declares.
    private static bool Names(string reference, string test) =>
        test == reference || test.StartsWith(reference + "#", StringComparison.Ordinal);

    public string Json() => JsonSerializer.Serialize(new { commit, passed = Passed, problems = Problems, rejected = Rejected, sources = Sources, entries = Entries }, ConfigurationMatrix.Options);

    public string Markdown()
    {
        var text = new StringBuilder("## Completion gate\n\n");
        var verified = Entries.Count(entry => entry.Status == Verified);
        var shortCommit = commit.Length >= 12 ? commit[..12] : commit;
        text.Append(Passed
            ? $"The gate passed. Every one of the inventory's {Entries.Count} entries has passing evidence at commit `{shortCommit}`.\n"
            : $"**The gate failed.** {verified} of the inventory's {Entries.Count} entries have passing evidence at commit `{shortCommit}`.\n");

        foreach (var problem in Problems.Concat(Rejected))
        {
            text.Append("\n- ").Append(problem);
        }

        text.Append("\n\n| Run report | Complete | Entries |\n|---|---|---|\n");
        foreach (var source in Sources)
        {
            text.Append(CultureInfo.InvariantCulture, $"| {source.Name} | {(source.Complete ? "yes" : "no")} | {source.Entries} |\n");
        }

        var open = Entries.Where(entry => entry.Status != Verified).ToList();
        if (open.Count > 0)
        {
            text.Append("\n| Scenario ID | Support | Status | Detail |\n|---|---|---|---|\n");
            foreach (var entry in open)
            {
                text.Append(CultureInfo.InvariantCulture, $"| `{entry.Id}` | {entry.Support} | {entry.Status} | {(entry.Detail ?? "").Replace("|", "\\|", StringComparison.Ordinal)} |\n");
            }
        }

        return text.ToString();
    }
}

public sealed record GateSource(string Name, string Commit, bool Complete, int Entries, string? Location);

public sealed record GateEntry(string Id, string Support, string Status, IReadOnlyList<string> Configurations, string? Detail);
