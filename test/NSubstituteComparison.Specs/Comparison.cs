extern alias Glue;
extern alias NSub;

using System.Reflection;

namespace NSubstituteComparison.Specs;

/// <summary>The outcome of one case under both libraries.</summary>
public sealed record CaseResult(ComparisonCase Case, IReadOnlyList<Observation> NSubstitute, IReadOnlyList<Observation> Glue)
{
    public bool Differs => !NSubstitute.SequenceEqual(Glue);
}

/// <summary>The two case assemblies and their runs.</summary>
public static class Comparison
{
    public static readonly Assembly GlueCases = typeof(Glue::NSubstituteComparison.Cases.Matching).Assembly;

    public static readonly Assembly NSubstituteCases = typeof(NSub::NSubstituteComparison.Cases.Matching).Assembly;

    /// <summary>The NSubstitute version the cases ran against, such as 6.2.0.</summary>
    public static string NSubstituteVersion =>
        NSubstituteCases.GetReferencedAssemblies().Single(name => name.Name == "NSubstitute").Version!.ToString(3);

    public static IReadOnlyList<ComparisonCase> Cases(Assembly assembly) => ComparisonCase.All(assembly);

    public static async Task<CaseResult> Run(string id)
    {
        var glue = Cases(GlueCases).Single(@case => @case.Id == id);
        var nsubstitute = Cases(NSubstituteCases).Single(@case => @case.Id == id);
        return new CaseResult(glue, await nsubstitute.Run(), await glue.Run());
    }

    public static async Task<IReadOnlyList<CaseResult>> RunAll()
    {
        var results = new List<CaseResult>();
        foreach (var @case in Cases(GlueCases))
        {
            results.Add(await Run(@case.Id));
        }

        return results;
    }

    /// <summary>The observations of both runs side by side, for a failure message.</summary>
    public static string Describe(CaseResult result)
    {
        var lines = new List<string> { $"{result.Case.Id}:" };
        var count = Math.Max(result.NSubstitute.Count, result.Glue.Count);
        for (var i = 0; i < count; i++)
        {
            var nsubstitute = i < result.NSubstitute.Count ? result.NSubstitute[i] : null;
            var glue = i < result.Glue.Count ? result.Glue[i] : null;
            var mark = nsubstitute == glue ? "  " : "! ";
            lines.Add($"{mark}{nsubstitute?.Label ?? glue?.Label}: NSubstitute {nsubstitute?.Value ?? "(none)"}, VerificationGlue.Mocks {glue?.Value ?? "(none)"}");
        }

        return string.Join("\n", lines);
    }
}
