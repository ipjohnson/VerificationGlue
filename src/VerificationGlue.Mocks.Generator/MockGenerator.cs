using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace VerificationGlue.Mocks.Generator;

/// <summary>
/// Generates a mock class for each type named in <c>Mock.Of&lt;T&gt;()</c>, <c>[Mock]</c> or
/// <c>[assembly: GenerateMock(typeof(T))]</c>, a module initializer that registers them, and one
/// file of <c>Arg</c> interceptors per source file. Diagnostics come from <see cref="MockAnalyzer"/>,
/// so no model here holds a location.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MockGenerator : IIncrementalGenerator
{
    public const string DiscoveryStep = "Discovery";
    public const string MocksStep = "Mocks";
    public const string RegistryStep = "Registry";
    public const string ArgSitesStep = "ArgSites";
    public const string ArgFilesStep = "ArgFiles";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => MockDiscovery.IsMockOfSyntax(node), static (syntax, token) => FromCall(syntax, token))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .WithTrackingName(DiscoveryStep);

        var attributed = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MockDiscovery.MockAttribute,
                static (node, _) => node is ParameterSyntax or PropertyDeclarationSyntax or VariableDeclaratorSyntax,
                static (syntax, _) => Build(MockDiscovery.TypeOfTarget(syntax.TargetSymbol), syntax.SemanticModel.Compilation))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .WithTrackingName(DiscoveryStep);

        var assembly = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MockDiscovery.GenerateMockAttribute,
                static (node, _) => node is CompilationUnitSyntax,
                static (syntax, _) => FromAssemblyAttributes(syntax))
            .SelectMany(static (models, _) => models)
            .WithTrackingName(DiscoveryStep);

        var mocks = calls.Collect()
            .Combine(attributed.Collect())
            .Combine(assembly.Collect())
            .SelectMany(static (all, _) => Distinct(all.Left.Left.Concat(all.Left.Right).Concat(all.Right)))
            .WithTrackingName(MocksStep);

        context.RegisterSourceOutput(mocks, static (output, model) => output.AddSource(model.MockName + ".g.cs", MockEmitter.Emit(model)));

        var registry = mocks
            .Select(static (model, _) => new RegistryEntry(model.Type, model.MockName, model.ExternAliases))
            .Collect()
            .Select(static (entries, _) => new EquatableArray<RegistryEntry>(entries.ToArray()))
            .WithTrackingName(RegistryStep);

        context.RegisterSourceOutput(registry, static (output, entries) =>
        {
            if (entries.Count > 0)
            {
                output.AddSource(RegistryEmitter.HintName, RegistryEmitter.Emit(entries));
            }
        });

        var argSites = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => MockDiscovery.IsArgSyntax(node), static (syntax, token) => FromArg(syntax, token))
            .Where(static site => site is not null)
            .Select(static (site, _) => site!.Value)
            .WithTrackingName(ArgSitesStep);

        var argFiles = argSites.Collect()
            .SelectMany(static (sites, _) => sites
                .GroupBy(site => site.Path)
                .OrderBy(group => group.Key, System.StringComparer.Ordinal)
                .Select(group => new ArgFile(group.Key, group.Select(site => site.Site).OrderBy(site => site.Position).ToArray())))
            .WithTrackingName(ArgFilesStep);

        context.RegisterSourceOutput(argFiles, static (output, file) =>
            output.AddSource(ArgInterceptorEmitter.HintName(file.FilePath), ArgInterceptorEmitter.Emit(file)));
    }

    private static MockModel? FromCall(GeneratorSyntaxContext syntax, CancellationToken token) =>
        syntax.SemanticModel.GetSymbolInfo(syntax.Node, token).Symbol is IMethodSymbol method && MockDiscovery.IsMockOf(method)
            ? Build(method.TypeArguments[0], syntax.SemanticModel.Compilation)
            : null;

    private static EquatableArray<MockModel> FromAssemblyAttributes(GeneratorAttributeSyntaxContext syntax)
    {
        var models = new List<MockModel>();
        foreach (var attribute in syntax.Attributes)
        {
            if (attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is ITypeSymbol type &&
                Build(type, syntax.SemanticModel.Compilation) is { } model)
            {
                models.Add(model);
            }
        }

        return models.ToArray();
    }

    private static MockModel? Build(ITypeSymbol? type, Compilation compilation) =>
        MockDiscovery.Closed(type) is { } closed ? MockModelBuilder.Build(closed, compilation) : null;

    /// <summary>One model per mock class, in name order, without the refused types, which the analyzer reports.</summary>
    private static ImmutableArray<MockModel> Distinct(IEnumerable<MockModel> models) =>
        models
            .Where(model => !model.IsRefused)
            .GroupBy(model => model.MockName)
            .Select(group => group.First())
            .OrderBy(model => model.MockName, System.StringComparer.Ordinal)
            .ToImmutableArray();

    private static (string Path, ArgSite Site)? FromArg(GeneratorSyntaxContext syntax, CancellationToken token)
    {
        var invocation = (InvocationExpressionSyntax)syntax.Node;
        if (syntax.SemanticModel.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method ||
            MockDiscovery.ArgKindOf(method) is not { } kind ||
            syntax.SemanticModel.GetOperation(invocation.Parent!, token) is not IArgumentOperation { Parameter: { } parameter } ||
            !MockDiscovery.IsMockableMember(parameter.ContainingSymbol) ||
            syntax.SemanticModel.GetInterceptableLocation(invocation, token) is not { } location)
        {
            return null;
        }

        return (invocation.SyntaxTree.FilePath, new ArgSite(invocation.SpanStart, kind, parameter.Ordinal, location.Version, location.Data));
    }
}
