using System.Text;
using System.Text.Json;

namespace MockSweep;

public sealed record SourceResult(
    string Source,
    IReadOnlyList<string> Assemblies,
    IReadOnlyList<MockOutcome> Outcomes,
    IReadOnlyList<string> OtherErrors,
    TimeSpan Elapsed)
{
    public int Compiled => Outcomes.Count(o => !o.Refused && !o.Failed);

    public int WithThrowingMembers => Outcomes.Count(o => !o.Refused && !o.Failed && o.Diagnostics.Count > 0);

    public int Refused => Outcomes.Count(o => o.Refused);

    public int Failed => Outcomes.Count(o => o.Failed);
}

/// <summary>Writes the sweep results as Markdown for people and JSON for tools.</summary>
public sealed class SweepReport(IReadOnlyList<SourceResult> sources, IReadOnlyList<string> packagesWithoutAssemblies)
{
    private const string Vgm0001 = "VGM0001";
    private const string Vgm0003 = "VGM0003";

    public bool Passed => sources.All(s => s.Failed == 0 && s.OtherErrors.Count == 0);

    public string Summary()
    {
        var interfaces = sources.Sum(s => s.Outcomes.Count);
        return $"{interfaces} interfaces in {sources.Count} sources: {sources.Sum(s => s.Compiled)} compiled " +
            $"({sources.Sum(s => s.WithThrowingMembers)} with members that throw, {Vgm0003}), " +
            $"{sources.Sum(s => s.Refused)} refused ({Vgm0001}), {sources.Sum(s => s.Failed)} failed to compile.";
    }

    public string Markdown()
    {
        var text = new StringBuilder();
        text.AppendLine("# Mock shape sweep");
        text.AppendLine();
        text.AppendLine(Summary());
        text.AppendLine();
        text.AppendLine("| Source | Interfaces | Compiled | With VGM0003 | Refused, VGM0001 | Failed | Seconds |");
        text.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var source in sources)
        {
            text.AppendLine($"| {source.Source} | {source.Outcomes.Count} | {source.Compiled} | {source.WithThrowingMembers} | {source.Refused} | {source.Failed} | {source.Elapsed.TotalSeconds:F1} |");
        }

        text.AppendLine();
        text.AppendLine("## Failed to compile");
        text.AppendLine();
        var failed = sources.SelectMany(s => s.Outcomes.Where(o => o.Failed).Select(o => (s.Source, Outcome: o))).ToList();
        if (failed.Count == 0)
        {
            text.AppendLine("None.");
        }

        foreach (var (source, outcome) in failed)
        {
            text.AppendLine($"- {source}: `{outcome.TypeName}`");
            foreach (var error in outcome.Errors.Take(5))
            {
                text.AppendLine($"  - {error}");
            }
        }

        foreach (var error in sources.SelectMany(s => s.OtherErrors))
        {
            text.AppendLine($"- Outside a mock: {error}");
        }

        text.AppendLine();
        text.AppendLine("## Refused with VGM0001, by reason");
        text.AppendLine();
        AppendReasons(text, Vgm0001, includeMember: false);

        text.AppendLine();
        text.AppendLine("## Members that throw, reported by VGM0003, by reason");
        text.AppendLine();
        AppendReasons(text, Vgm0003, includeMember: true);

        text.AppendLine();
        text.AppendLine("## Listed packages with no assembly of their own");
        text.AppendLine();
        text.AppendLine("These are metapackages, analyzers, build-only packages, or packages whose assemblies the shared framework provides, which the BCL row covers.");
        text.AppendLine();
        foreach (var package in packagesWithoutAssemblies)
        {
            text.AppendLine($"- {package}");
        }

        return text.ToString();
    }

    public string Json() => JsonSerializer.Serialize(
        new
        {
            passed = Passed,
            sources = sources.Select(s => new
            {
                source = s.Source,
                assemblies = s.Assemblies,
                interfaces = s.Outcomes.Count,
                compiled = s.Compiled,
                withThrowingMembers = s.WithThrowingMembers,
                refused = s.Refused,
                failed = s.Failed,
                seconds = Math.Round(s.Elapsed.TotalSeconds, 2),
                outcomes = s.Outcomes.Select(o => new
                {
                    type = o.TypeName,
                    status = o.Failed ? "failed" : o.Refused ? "refused" : o.Diagnostics.Count > 0 ? "compiled with VGM0003" : "compiled",
                    diagnostics = o.Diagnostics,
                    errors = o.Errors,
                }),
            }),
            packagesWithoutAssemblies,
        },
        new JsonSerializerOptions { WriteIndented = true });

    /// <summary>
    /// Groups diagnostics by the reason after "cannot be mocked:", with type parameter and member
    /// names kept, so each distinct cause is one row.
    /// </summary>
    private void AppendReasons(StringBuilder text, string id, bool includeMember)
    {
        var rows = sources
            .SelectMany(s => s.Outcomes.SelectMany(o => o.Diagnostics.Where(d => d.Id == id).Select(d => (s.Source, o.TypeName, d.Message))))
            .GroupBy(row => Reason(row.Message))
            .OrderByDescending(group => group.Count())
            .ToList();
        if (rows.Count == 0)
        {
            text.AppendLine("None.");
            return;
        }

        text.AppendLine("| Reason | Count | Interfaces | Examples |");
        text.AppendLine("|---|---|---|---|");
        foreach (var group in rows)
        {
            var examples = group.Select(row => includeMember ? MemberOf(row.Message) : row.TypeName).Distinct().Take(3);
            text.AppendLine($"| {group.Key} | {group.Count()} | {group.Select(r => r.TypeName).Distinct().Count()} | {string.Join(", ", examples.Select(e => "`" + e + "`"))} |");
        }
    }

    private static string Reason(string message)
    {
        var start = message.IndexOf("cannot be mocked: ", StringComparison.Ordinal);
        var reason = start < 0 ? message : message[(start + "cannot be mocked: ".Length)..];
        var end = reason.IndexOf(". The generated member", StringComparison.Ordinal);
        reason = end < 0 ? reason : reason[..end];

        // "parameter 'span' is the ref struct 'Span<byte>'" and similar collapse to their kind.
        foreach (var kind in new[] { "is the ref struct", "returns the ref struct", "type is the ref struct", "is the pointer type", "returns the pointer type", "type is the pointer type" })
        {
            if (reason.Contains(kind, StringComparison.Ordinal))
            {
                return reason.StartsWith("parameter", StringComparison.Ordinal) ? "a parameter " + kind : (reason[..reason.IndexOf(kind, StringComparison.Ordinal)] + kind).Trim() + " ...";
            }
        }

        return reason.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string MemberOf(string message)
    {
        var end = message.IndexOf("' cannot be mocked", StringComparison.Ordinal);
        return end < 1 ? message : message[1..end];
    }
}
