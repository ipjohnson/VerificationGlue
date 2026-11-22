using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hardening.Campaign;

// An issue the triage job opens for the owner, or comments on when an open issue has the same title.
public sealed record CampaignIssue(string Title, string Body);

// campaign.json, campaign.md and issues.json: the result of each run, and one issue for each failure and
// each incomplete run.
public sealed record CampaignReport
{
    public const string JsonFileName = "campaign.json";

    public const string MarkdownFileName = "campaign.md";

    public const string IssuesFileName = "issues.json";

    public const string TitlePrefix = "Hardening campaign: ";

    public const string ArtifactName = "hardening-campaign";

    // GitHub refuses an issue body over 65,536 characters.
    private const int MaxBody = 60_000;

    private const int MaxDetail = 20_000;

    private const int MaxFile = 10_000;

    public required string Commit { get; init; }

    public required DateTimeOffset Date { get; init; }

    public required int Seed { get; init; }

    public string? Location { get; init; }

    public required string Environment { get; init; }

    public required IReadOnlyList<CampaignRun> Runs { get; init; }

    public bool Complete => Runs.All(run => run.Incomplete.Count == 0);

    public bool Passed => Runs.All(run => run.Status == RunStatus.Passed);

    public string Json() => JsonSerializer.Serialize(this, CampaignConfig.Options) + "\n";

    public string Markdown(CampaignConfig config)
    {
        var text = new StringBuilder()
            .AppendLine("## Hardening campaign")
            .AppendLine()
            .AppendLine($"Commit `{Commit}`, seed {Seed}.")
            .AppendLine();
        var incomplete = Runs.Where(run => run.Incomplete.Count > 0).ToList();
        if (incomplete.Count > 0)
        {
            text.AppendLine("Coverage is incomplete:").AppendLine();
            foreach (var run in incomplete)
            {
                text.AppendLine($"- {run.Name}: {string.Join("; ", run.Incomplete)}.");
            }
        }
        else
        {
            text.AppendLine("Coverage is complete. Every run finished its cases within its limit.");
        }

        text.AppendLine()
            .AppendLine("| Run | Cases | Limit | Time | Status |")
            .AppendLine("|---|---|---|---|---|");
        foreach (var run in Runs)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {run.Name} | {(run.Cases is { } cases ? cases.ToString("N0", CultureInfo.InvariantCulture) : "every mutation")} | {run.LimitMinutes} min | {run.ElapsedSeconds:0.0} s | {run.Status}{(run.Overrun ? ", over its limit" : "")} |"));
        }

        foreach (var run in Runs.Where(run => run.Failures.Count > 0))
        {
            foreach (var failure in run.Failures)
            {
                text.AppendLine().AppendLine($"### {run.Name}: {failure.Key}").AppendLine().AppendLine(Fenced(Cut(failure.Detail, MaxDetail)));
            }
        }

        var issues = Issues(config, _ => null).Count;
        text.AppendLine()
            .AppendLine(issues == 0
                ? "No issue to file."
                : $"The triage job files {issues} {(issues == 1 ? "issue" : "issues")} labelled `{config.Label}` for {config.Owner}.");
        return text.ToString();
    }

    // One issue for each failure and one for each incomplete run. readFile gives the text of a file the run
    // wrote, from its path in the output folder, or null when it cannot be read.
    public IReadOnlyList<CampaignIssue> Issues(CampaignConfig config, Func<string, string?> readFile)
    {
        var issues = new List<CampaignIssue>();
        foreach (var run in Runs)
        {
            foreach (var failure in run.Failures)
            {
                var body = Header(config, run, "found this failure")
                    .AppendLine("What the run printed, with the replay command of the failing case, the seed, the source or sequence, and the expected and actual observations:")
                    .AppendLine()
                    .AppendLine(Fenced(Cut(failure.Detail, MaxDetail)));
                foreach (var file in run.Files)
                {
                    if (readFile(file) is not { } content)
                    {
                        continue;
                    }

                    var section = $"\n`{file}`:\n\n{Fenced(Cut(content, MaxFile))}\n";
                    if (body.Length + section.Length > MaxBody)
                    {
                        body.AppendLine().AppendLine($"The artifact has the other files the run wrote, from `{file}` on.");
                        break;
                    }

                    body.Append(section);
                }

                issues.Add(new CampaignIssue(TitlePrefix + failure.Key, body.ToString()));
            }

            if (run.Incomplete.Count > 0)
            {
                var body = Header(config, run, run.Overrun ? "ran over a limit" : "did not finish a run")
                    .AppendLine("The run's coverage is incomplete:")
                    .AppendLine();
                foreach (var reason in run.Incomplete)
                {
                    body.AppendLine($"- {reason}.");
                }

                body.AppendLine()
                    .AppendLine($"Its limit is in `{CampaignConfig.RelativePath}`. Raise the limit or lower the case count when the run needs more time, and find the cause when it should not.");
                issues.Add(new CampaignIssue($"{TitlePrefix}the {run.Name} run is incomplete", body.ToString()));
            }
        }

        return issues;
    }

    public string IssuesJson(CampaignConfig config, Func<string, string?> readFile) =>
        JsonSerializer.Serialize(Issues(config, readFile), CampaignConfig.Options) + "\n";

    private StringBuilder Header(CampaignConfig config, CampaignRun run, string what)
    {
        var text = new StringBuilder()
            .AppendLine($"The hardening campaign {what}. {config.Owner} triages it as `docs/design/hardening.md`, The scheduled campaign as built, describes.")
            .AppendLine()
            .AppendLine("| | |")
            .AppendLine("|---|---|")
            .AppendLine($"| Workflow run | {Location ?? "a local run"} |")
            .AppendLine($"| Commit | `{Commit}` |")
            .AppendLine($"| Run | {run.Name}{(run.Test is { } test ? $", `{test}`" : "")} |");
        if (run.Seed is { } seed)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| Seed | {seed} |"));
        }

        if (run.Cases is { } cases)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| Cases | {cases} |"));
        }

        return text
            .AppendLine(string.Create(CultureInfo.InvariantCulture, $"| Time | {run.ElapsedSeconds:0.0} s of its {run.LimitMinutes}-minute limit |"))
            .AppendLine($"| Environment | {Environment} |")
            .AppendLine($"| Artifact | `{ArtifactName}`, kept {config.RetentionDays} days |")
            .AppendLine()
            .AppendLine("Replay the whole run:")
            .AppendLine()
            .AppendLine("```sh")
            .AppendLine(run.Replay)
            .AppendLine("```")
            .AppendLine();
    }

    // Four backticks, so a fence inside the text does not end the block.
    private static string Fenced(string text) => $"````text\n{text.TrimEnd()}\n````";

    private static string Cut(string text, int length) =>
        text.Length <= length ? text : string.Create(CultureInfo.InvariantCulture, $"{text[..length]}\n... {text.Length - length} more characters are in the artifact.");
}
