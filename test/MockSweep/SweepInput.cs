namespace MockSweep;

/// <summary>The references file that test/MockSweep.Packages writes.</summary>
public sealed class SweepInput
{
    public const string Bcl = "BCL";

    private SweepInput(List<(string Source, string Path)> references, List<string> sweptSources, List<string> packagesWithoutAssemblies)
    {
        References = references;
        SweptSources = sweptSources;
        PackagesWithoutAssemblies = packagesWithoutAssemblies;
    }

    /// <summary>Every compile reference and the package or framework it came from.</summary>
    public IReadOnlyList<(string Source, string Path)> References { get; }

    /// <summary>The BCL and each listed package that brings at least one assembly of its own.</summary>
    public IReadOnlyList<string> SweptSources { get; }

    /// <summary>
    /// Listed packages with no compile assembly of their own: metapackages, analyzers, build-only
    /// packages, and packages whose assemblies the shared framework provides.
    /// </summary>
    public IReadOnlyList<string> PackagesWithoutAssemblies { get; }

    public static SweepInput Read(string path)
    {
        var packages = new List<string>();
        var references = new List<(string Source, string Path)>();
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split('\t');
            if (parts is ["package", var id])
            {
                packages.Add(id);
            }
            else if (parts is ["reference", var source, var file])
            {
                references.Add((source.StartsWith("Microsoft.NETCore.App", StringComparison.Ordinal) ? Bcl : source, file));
            }
        }

        var withAssemblies = references.Select(r => r.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var swept = new List<string> { Bcl };
        swept.AddRange(references
            .Select(r => r.Source)
            .Distinct()
            .Where(source => packages.Contains(source, StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase));
        var without = packages.Where(p => !withAssemblies.Contains(p)).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return new SweepInput(references, swept, without);
    }
}
