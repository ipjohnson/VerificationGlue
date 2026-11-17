using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerificationGlue.Mocks;
using VerificationGlue.Mocks.Generator;

namespace MockSweep;

/// <summary>What happened to one mocked type.</summary>
/// <param name="TypeName">The type as written in a message.</param>
/// <param name="MockName">The generated class name.</param>
/// <param name="Source">The generated source, or null when the type was refused.</param>
/// <param name="Diagnostics">The VGM diagnostics the model reports, as "ID: message".</param>
/// <param name="Errors">The compile errors in the generated source.</param>
public sealed record MockOutcome(
    string TypeName,
    string MockName,
    string? Source,
    IReadOnlyList<MockDiagnostic> Diagnostics,
    IReadOnlyList<string> Errors)
{
    public bool Refused => Source is null;

    public bool Failed => Errors.Count > 0;
}

public sealed record MockDiagnostic(string Id, string Message);

/// <summary>Builds, emits and compiles mocks the way the generator will, without a generator driver.</summary>
public static class MockCompiler
{
    public static MetadataReference RuntimeReference { get; } = MetadataReference.CreateFromFile(typeof(MockAdapter).Assembly.Location);

    public static CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.Preview);

    /// <summary>
    /// Mocks each type, adds every generated file to <paramref name="compilation"/> and compiles
    /// it once. Errors in a generated file belong to that file's type. Errors elsewhere are returned
    /// in <paramref name="otherErrors"/>.
    /// </summary>
    public static IReadOnlyList<MockOutcome> Compile(
        CSharpCompilation compilation,
        IEnumerable<INamedTypeSymbol> types,
        out IReadOnlyList<string> otherErrors,
        out CSharpCompilation compiled)
    {
        var models = types.Select(type => MockModelBuilder.Build(type, compilation)).ToList();
        var trees = new Dictionary<SyntaxTree, int>();
        var sources = new string?[models.Count];
        for (var i = 0; i < models.Count; i++)
        {
            if (models[i].IsRefused)
            {
                continue;
            }

            sources[i] = MockEmitter.Emit(models[i]);
            trees[CSharpSyntaxTree.ParseText(sources[i]!, ParseOptions, path: $"{i:D5}.{models[i].MockName}.g.cs")] = i;
        }

        compiled = compilation.AddSyntaxTrees(trees.Keys);
        var errors = new List<string>[models.Count];
        var other = new List<string>();
        foreach (var diagnostic in compiled.GetDiagnostics())
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error)
            {
                continue;
            }

            if (diagnostic.Location.SourceTree is { } tree && trees.TryGetValue(tree, out var index))
            {
                (errors[index] ??= []).Add(Format(diagnostic, sources[index]!));
            }
            else
            {
                other.Add(diagnostic.ToString());
            }
        }

        otherErrors = other;
        return models
            .Select((model, i) => new MockOutcome(
                model.TypeName,
                model.MockName,
                sources[i],
                model.Diagnostics
                    .Select(d => new MockDiagnostic(d.Id, string.Format(MockDiagnostics.Find(d.Id).MessageFormat.ToString(), d.Arguments.ToArray())))
                    .ToList(),
                (IReadOnlyList<string>?)errors[i] ?? []))
            .ToList();
    }

    private static string Format(Diagnostic diagnostic, string source)
    {
        var line = diagnostic.Location.GetLineSpan().StartLinePosition.Line;
        var lines = source.Split('\n');
        var text = line < lines.Length ? lines[line].Trim() : "";
        return $"{diagnostic.Id}: {diagnostic.GetMessage()} [line {line + 1}: {text}]";
    }
}
