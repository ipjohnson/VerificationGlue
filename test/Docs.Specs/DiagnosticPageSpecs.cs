using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Docs.Specs;

public class DiagnosticPageSpecs
{
    private const string HelpLinkBase = "https://github.com/ipjohnson/VerificationGlue/blob/main/docs/diagnostics/";

    private static readonly string[] GeneratorAssemblies =
    [
        "VerificationGlue.Generator",
        "VerificationGlue.Mocks.Generator",
        "VerificationGlue.Tests.Generator",
        "VerificationGlue.Tests.AspNetCore.Generator",
    ];

    private static readonly string[] PageSections =
    [
        "## What it means",
        "## Example that triggers it",
        "## The fix",
        "## When to suppress it",
    ];

    private static readonly string DiagnosticsDirectory = Path.Combine(RepositoryRoot.Path, "docs", "diagnostics");

    [Fact]
    public void Descriptors_are_found()
    {
        Assert.Contains(Descriptors(), descriptor => descriptor.Id == "VGT0007");
    }

    [Fact]
    public void Every_descriptor_has_a_page()
    {
        var missing = Descriptors()
            .Where(descriptor => !File.Exists(PagePath(descriptor.Id)))
            .Select(descriptor => $"{descriptor.Id} has no page at docs/diagnostics/{descriptor.Id}.md")
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Build_errors_are_found()
    {
        Assert.Contains(BuildErrors(), error => error.Id == "VGT1001");
    }

    [Fact]
    public void Every_build_error_has_a_page_and_links_to_it()
    {
        var problems = BuildErrors()
            .SelectMany(error => new[]
            {
                File.Exists(PagePath(error.Id)) ? null : $"{error.Id} in {error.File} has no page at docs/diagnostics/{error.Id}.md",
                error.HelpLink == HelpLinkBase + error.Id + ".md" ? null : $"{error.Id} in {error.File} has HelpLink '{error.HelpLink}'",
            })
            .OfType<string>()
            .ToList();

        Assert.Empty(problems);
    }

    [Fact]
    public void Every_page_has_a_descriptor()
    {
        var ids = Descriptors().Select(descriptor => descriptor.Id).Concat(BuildErrors().Select(error => error.Id)).ToHashSet();

        var orphans = Pages()
            .Select(Path.GetFileNameWithoutExtension)
            .Where(id => !ids.Contains(id!))
            .Select(id => $"docs/diagnostics/{id}.md names no descriptor")
            .ToList();

        Assert.Empty(orphans);
    }

    [Fact]
    public void Every_descriptor_links_to_its_page()
    {
        var wrong = Descriptors()
            .Where(descriptor => descriptor.HelpLinkUri != HelpLinkBase + descriptor.Id + ".md")
            .Select(descriptor => $"{descriptor.Id} has HelpLinkUri '{descriptor.HelpLinkUri}'")
            .ToList();

        Assert.Empty(wrong);
    }

    [Fact]
    public void Every_page_has_the_standard_sections()
    {
        var problems = new List<string>();
        foreach (var page in Pages())
        {
            var id = Path.GetFileNameWithoutExtension(page);
            var lines = File.ReadAllLines(page);
            if (lines.Length == 0 || !lines[0].StartsWith($"# {id}: ", StringComparison.Ordinal))
            {
                problems.Add($"{id}.md does not start with '# {id}: <title>'");
            }

            problems.AddRange(PageSections
                .Where(section => !lines.Contains(section))
                .Select(section => $"{id}.md has no '{section}' section"));
        }

        Assert.Empty(problems);
    }

    private static string PagePath(string id) => Path.Combine(DiagnosticsDirectory, id + ".md");

    /// <summary>The <c>Error</c> tasks with a VG code in the packages' MSBuild files, such as VGT1001.</summary>
    private static List<(string Id, string HelpLink, string File)> BuildErrors() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot.Path, "src"), "*.targets", SearchOption.AllDirectories)
            .SelectMany(file => System.Xml.Linq.XDocument.Load(file).Descendants()
                .Where(element => element.Name.LocalName == "Error" && (element.Attribute("Code")?.Value.StartsWith("VG", StringComparison.Ordinal) ?? false))
                .Select(element => (element.Attribute("Code")!.Value, element.Attribute("HelpLink")?.Value ?? "", Path.GetRelativePath(RepositoryRoot.Path, file))))
            .ToList();

    private static IEnumerable<string> Pages() =>
        Directory.Exists(DiagnosticsDirectory) ? Directory.GetFiles(DiagnosticsDirectory, "*.md") : [];

    private static List<DiagnosticDescriptor> Descriptors() =>
        GeneratorAssemblies
            .Select(Assembly.Load)
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
            .Select(field => (DiagnosticDescriptor)field.GetValue(null)!)
            .DistinctBy(descriptor => descriptor.Id)
            .ToList();
}
