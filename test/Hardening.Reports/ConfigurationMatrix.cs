using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hardening.Reports;

// The shape of docs/design/hardening-matrix.json. A setting is one factor value, written factor=value.
// A profile is the configuration of a CI step apart from its OS, which the run takes from the machine.
public sealed record ConfigurationMatrix
{
    public const string RelativePath = "docs/design/hardening-matrix.json";

    public const string OsFactor = "os";

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public required IReadOnlyList<Factor> Factors { get; init; }

    public required IReadOnlyList<Exclusion> Exclusions { get; init; }

    public required IReadOnlyList<Profile> Profiles { get; init; }

    public static ConfigurationMatrix Parse(string json) =>
        JsonSerializer.Deserialize<ConfigurationMatrix>(json, Options) ?? throw new JsonException("The matrix is null.");

    public static ConfigurationMatrix Load(string repositoryRoot) =>
        Parse(File.ReadAllText(Path.Combine(repositoryRoot, RelativePath)));

    public Profile? Profile(string name) => Profiles.FirstOrDefault(profile => profile.Name == name);

    // The settings of a profile on an OS, in factor order.
    public IReadOnlyList<string> Configuration(Profile profile, string os) =>
        [.. Factors.Select(factor => $"{factor.Name}={(factor.Name == OsFactor ? os : profile.Values.GetValueOrDefault(factor.Name, "?"))}")];

    public bool Excludes(string first, string second) =>
        Exclusions.Any(exclusion => exclusion.Pair.Contains(first) && exclusion.Pair.Contains(second) && first != second);

    // Every pair of settings of two factors that no exclusion removes, in factor order.
    public IReadOnlyList<SettingPair> ValidPairs()
    {
        var pairs = new List<SettingPair>();
        for (var i = 0; i < Factors.Count; i++)
        {
            for (var j = i + 1; j < Factors.Count; j++)
            {
                foreach (var first in Factors[i].Settings)
                {
                    pairs.AddRange(Factors[j].Settings.Where(second => !Excludes(first, second)).Select(second => new SettingPair(first, second)));
                }
            }
        }

        return pairs;
    }

    // A pairwise selection. It starts with the configuration of each profile on each OS that CI runs it on,
    // then adds combinations until every valid pair is in one. Each added combination starts from the
    // first pair still missing and takes, factor by factor, the allowed value that adds the most missing pairs.
    public IReadOnlyList<Combination> Select()
    {
        var combinations = new List<Combination>();
        foreach (var profile in Profiles)
        {
            foreach (var os in profile.Os)
            {
                var settings = Configuration(profile, os);
                if (!combinations.Any(combination => combination.Settings.SequenceEqual(settings)))
                {
                    combinations.Add(new Combination(settings, profile.Name));
                }
            }
        }

        var missing = ValidPairs().ToList();
        missing.RemoveAll(pair => combinations.Any(combination => combination.Covers(pair)));
        while (missing.Count > 0)
        {
            var chosen = new string?[Factors.Count];
            chosen[FactorOf(missing[0].First)] = missing[0].First;
            chosen[FactorOf(missing[0].Second)] = missing[0].Second;
            for (var i = 0; i < Factors.Count; i++)
            {
                if (chosen[i] is not null)
                {
                    continue;
                }

                var allowed = Factors[i].Settings.Where(setting => chosen.All(other => other is null || !Excludes(setting, other))).ToList();
                if (allowed.Count == 0)
                {
                    throw new InvalidOperationException($"No value of {Factors[i].Name} goes with {string.Join(" ", chosen.OfType<string>())}.");
                }

                chosen[i] = allowed.MaxBy(setting => missing.Count(pair => chosen.Any(other => other is not null && pair.Is(setting, other))));
            }

            var combination = new Combination([.. chosen.Select(setting => setting!)], "pairwise");
            combinations.Add(combination);
            missing.RemoveAll(combination.Covers);
        }

        return combinations;
    }

    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        var settings = new HashSet<string>();
        foreach (var factor in Factors)
        {
            if (Factors.Count(other => other.Name == factor.Name) > 1)
            {
                problems.Add($"the factor {factor.Name} is declared more than once");
            }

            if (factor.Values.Count < 2)
            {
                problems.Add($"the factor {factor.Name} has fewer than two values");
            }

            foreach (var setting in factor.Settings.Where(setting => !settings.Add(setting)))
            {
                problems.Add($"the value {setting} is listed more than once");
            }
        }

        if (Factors.All(factor => factor.Name != OsFactor))
        {
            problems.Add($"the matrix has no {OsFactor} factor");
        }

        foreach (var exclusion in Exclusions)
        {
            var name = string.Join(" ", exclusion.Pair);
            if (exclusion.Pair.Count != 2)
            {
                problems.Add($"the exclusion {name} does not name two values");
                continue;
            }

            foreach (var setting in exclusion.Pair.Where(setting => !settings.Contains(setting)))
            {
                problems.Add($"the exclusion {name} names the unknown value {setting}");
            }

            if (settings.Contains(exclusion.Pair[0]) && settings.Contains(exclusion.Pair[1]) && FactorOf(exclusion.Pair[0]) == FactorOf(exclusion.Pair[1]))
            {
                problems.Add($"the exclusion {name} names two values of one factor");
            }

            if (string.IsNullOrWhiteSpace(exclusion.Reason))
            {
                problems.Add($"the exclusion {name} has no reason");
            }

            if (Exclusions.Count(other => other.Pair.Order().SequenceEqual(exclusion.Pair.Order())) > 1)
            {
                problems.Add($"the exclusion {name} is listed more than once");
            }
        }

        foreach (var profile in Profiles)
        {
            if (Profiles.Count(other => other.Name == profile.Name) > 1)
            {
                problems.Add($"the profile {profile.Name} is declared more than once");
            }

            foreach (var factor in Factors.Where(factor => factor.Name != OsFactor))
            {
                if (!profile.Values.TryGetValue(factor.Name, out var value))
                {
                    problems.Add($"the profile {profile.Name} gives no {factor.Name}");
                }
                else if (!factor.Values.Contains(value))
                {
                    problems.Add($"the profile {profile.Name} gives {factor.Name}={value}, which the matrix does not list");
                }
            }

            foreach (var key in profile.Values.Keys.Where(key => key == OsFactor || Factors.All(factor => factor.Name != key)))
            {
                problems.Add($"the profile {profile.Name} gives {key}, which is not a factor it can set");
            }

            foreach (var os in profile.Os)
            {
                if (!settings.Contains($"{OsFactor}={os}"))
                {
                    problems.Add($"the profile {profile.Name} runs on {os}, which the matrix does not list");
                    continue;
                }

                var configuration = Configuration(profile, os);
                foreach (var exclusion in Exclusions.Where(exclusion => exclusion.Pair.All(configuration.Contains)))
                {
                    problems.Add($"the profile {profile.Name} on {os} has the excluded pair {string.Join(" ", exclusion.Pair)}");
                }
            }
        }

        return problems;
    }

    private int FactorOf(string setting)
    {
        var name = setting[..setting.IndexOf('=')];
        for (var i = 0; i < Factors.Count; i++)
        {
            if (Factors[i].Name == name)
            {
                return i;
            }
        }

        throw new ArgumentException($"{setting} names no factor of the matrix.", nameof(setting));
    }
}

public sealed record Factor
{
    public required string Name { get; init; }

    public required IReadOnlyList<string> Values { get; init; }

    [JsonIgnore]
    public IEnumerable<string> Settings => Values.Select(value => $"{Name}={value}");
}

public sealed record Exclusion
{
    public required IReadOnlyList<string> Pair { get; init; }

    public required string Reason { get; init; }
}

public sealed record Profile
{
    public required string Name { get; init; }

    // The project of the step, whose tests the inventory's evidence names.
    public required string Project { get; init; }

    public required IReadOnlyDictionary<string, string> Values { get; init; }

    // The OS values that CI runs the profile on. The pairwise selection starts with these configurations.
    public required IReadOnlyList<string> Os { get; init; }
}

public readonly record struct SettingPair(string First, string Second)
{
    public bool Is(string a, string b) => (First == a && Second == b) || (First == b && Second == a);

    public override string ToString() => $"{First} {Second}";
}

// A selected combination. Origin is the profile it comes from, or pairwise.
public sealed record Combination(IReadOnlyList<string> Settings, string Origin)
{
    public string Key => string.Join(" ", Settings);

    public bool Covers(SettingPair pair) => Settings.Contains(pair.First) && Settings.Contains(pair.Second);
}
