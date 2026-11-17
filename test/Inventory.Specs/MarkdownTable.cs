using System.Text;
using System.Text.RegularExpressions;

namespace Inventory.Specs;

internal static partial class MarkdownTable
{
    // The body rows of the first table under the heading, each split into trimmed cells with
    // backticks removed.
    public static IReadOnlyList<IReadOnlyList<string>>? Read(string markdown, string heading)
    {
        var lines = markdown.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var start = lines.IndexOf(heading);
        if (start < 0)
        {
            return null;
        }

        var level = heading.TakeWhile(c => c == '#').Count();
        var rows = new List<IReadOnlyList<string>>();
        var inTable = false;
        for (var i = start + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (IsHeadingAtOrAbove(line, level))
            {
                break;
            }

            if (!line.StartsWith('|'))
            {
                if (inTable)
                {
                    break;
                }

                continue;
            }

            if (!inTable)
            {
                inTable = true;
                i++;
                continue;
            }

            rows.Add(line.Trim('|').Split('|').Select(cell => cell.Trim().Replace("`", "", StringComparison.Ordinal)).ToList());
        }

        return rows;
    }

    // The anchors GitHub gives the headings of a markdown file.
    public static IReadOnlySet<string> Anchors(string markdown)
    {
        var anchors = new HashSet<string>();
        var inFence = false;
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            var match = HeadingPattern().Match(line);
            if (inFence || !match.Success)
            {
                continue;
            }

            var slug = Slug(match.Groups["text"].Value);
            var anchor = slug;
            for (var n = 1; !anchors.Add(anchor); n++)
            {
                anchor = $"{slug}-{n}";
            }
        }

        return anchors;
    }

    private static string Slug(string heading)
    {
        var slug = new StringBuilder();
        foreach (var c in heading.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
            {
                slug.Append(c);
            }
            else if (c == ' ')
            {
                slug.Append('-');
            }
        }

        return slug.ToString();
    }

    private static bool IsHeadingAtOrAbove(string line, int level)
    {
        var match = HeadingPattern().Match(line);
        return match.Success && match.Groups["hashes"].Length <= level;
    }

    [GeneratedRegex("^(?<hashes>#{1,6}) (?<text>.+)$")]
    private static partial Regex HeadingPattern();
}
