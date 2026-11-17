using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VerificationGlue.Mocks.Generator.Specs;

/// <summary>MSBuild properties as a generator or analyzer sees them, each listed in <c>CompilerVisibleProperty</c>.</summary>
internal sealed class BuildProperties(Dictionary<string, string> properties) : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions None = new Options([]);

    public override AnalyzerConfigOptions GlobalOptions { get; } =
        new Options(properties.ToDictionary(pair => "build_property." + pair.Key, pair => pair.Value));

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => None;

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
