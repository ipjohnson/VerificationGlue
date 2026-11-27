using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;

namespace VerificationGlue.Tests.DependencyInjection.Generator;

/// <summary>
/// Writes a module initializer that registers the <c>Configure</c> call of each <c>T</c> that a
/// <c>[ServiceCollection&lt;T&gt;]</c> names, so the bootstrap calls it with no reflection. Reports VGD0001
/// for a <c>T</c> with no <c>Configure</c> method it can call.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ConfigureGenerator : IIncrementalGenerator
{
    public const string SourcesStep = "Sources";

    public const string HintName = "VerificationGlue.Tests.DependencyInjection.ServiceCollectionSources.g.cs";

    private const string ServiceCollectionAttribute = "VerificationGlue.Tests.DependencyInjection.ServiceCollectionAttribute`1";
    private const string IServiceCollection = "Microsoft.Extensions.DependencyInjection.IServiceCollection";
    private const string TestContainerContext = "VerificationGlue.Tests.TestContainerContext";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sources = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ServiceCollectionAttribute,
                static (_, _) => true,
                static (syntax, _) => new EquatableArray<SourceModel>(syntax.Attributes
                    .Select(attribute => Source(attribute, syntax.SemanticModel.Compilation, syntax.TargetNode.GetLocation()))
                    .OfType<SourceModel>()
                    .ToArray()))
            .WithTrackingName(SourcesStep)
            .SelectMany(static (models, _) => models)
            .Collect();

        context.RegisterSourceOutput(sources, static (output, models) =>
        {
            foreach (var model in models.Where(model => model.Problem is not null))
            {
                output.ReportDiagnostic(Diagnostic.Create(Diagnostics.NoConfigureMethod, model.Location.ToLocation(), model.DisplayName, model.Problem));
            }

            var registered = models
                .Where(model => model.Problem is null)
                .GroupBy(model => model.TypeName, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(model => model.TypeName, StringComparer.Ordinal)
                .ToList();
            if (registered.Count > 0)
            {
                output.AddSource(HintName, Emit(registered));
            }
        });
    }

    private static SourceModel? Source(AttributeData attribute, Compilation compilation, Location target)
    {
        if (attribute.AttributeClass is not { TypeArguments.Length: 1 } attributeClass || attributeClass.TypeArguments[0] is not { TypeKind: not TypeKind.Error } type)
        {
            return null;
        }

        var location = LocationModel.From(attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? target);
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var displayName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var candidates = Configures(type).ToList();
        var accepted = candidates
            .Select(method => (Method: method, Shape: Shape(method, compilation)))
            .Where(candidate => candidate.Shape is not null)
            .OrderByDescending(candidate => candidate.Shape)
            .FirstOrDefault();
        if (accepted.Method is not null)
        {
            return new SourceModel(typeName, displayName, accepted.Shape == 2, null, location);
        }

        var problem = candidates.Count == 0
            ? "it has no method named Configure"
            : (candidates.Count == 1 ? "its method " : "its methods ") +
              string.Join(" and ", candidates.Select(method => "'" + method.ToDisplayString(SignatureFormat) + "'")) +
              (candidates.Count == 1 ? " does not have one of these signatures" : " do not have one of these signatures");
        return new SourceModel(typeName, displayName, false, problem, location);
    }

    private static readonly SymbolDisplayFormat SignatureFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers |
            SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeRef,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeName,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>The methods named Configure on <paramref name="type"/> and its base types.</summary>
    private static IEnumerable<IMethodSymbol> Configures(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var method in current.GetMembers("Configure").OfType<IMethodSymbol>())
            {
                if (method.MethodKind == MethodKind.Ordinary)
                {
                    yield return method;
                }
            }
        }
    }

    /// <summary>1 for <c>Configure(IServiceCollection)</c>, 2 for the shape with <c>TestContainerContext</c>, or null.</summary>
    private static int? Shape(IMethodSymbol method, Compilation compilation)
    {
        if (!method.IsStatic || method.IsAbstract || method.IsGenericMethod || !method.ReturnsVoid ||
            method.DeclaredAccessibility != Accessibility.Public || !compilation.IsSymbolAccessibleWithin(method, compilation.Assembly) ||
            method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.IsParams) ||
            method.Parameters.Length is < 1 or > 2 || FullName(method.Parameters[0].Type) != IServiceCollection)
        {
            return null;
        }

        return method.Parameters.Length == 1 ? 1
            : FullName(method.Parameters[1].Type) == TestContainerContext ? 2
            : null;
    }

    private static string FullName(ITypeSymbol type) =>
        type.ContainingType is null && type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString() + "." + type.MetadataName
            : type.MetadataName;

    internal static string Emit(IReadOnlyList<SourceModel> sources)
    {
        var text = new StringBuilder("// <auto-generated/>\n#nullable enable\n#pragma warning disable\n");
        text.Append("namespace VerificationGlue.Tests.DependencyInjection.Generated\n{\n");
        text.Append("    internal static class ServiceCollectionSources\n    {\n");
        text.Append("        [global::System.Runtime.CompilerServices.ModuleInitializer]\n");
        text.Append("        internal static void Register()\n        {\n");
        foreach (var source in sources)
        {
            text.Append("            global::VerificationGlue.Tests.DependencyInjection.ServiceCollectionRegistry.Register<").Append(source.TypeName)
                .Append(">(static (services, context) => ").Append(source.TypeName)
                .Append(source.TakesContext ? ".Configure(services, context));\n" : ".Configure(services));\n");
        }

        text.Append("        }\n    }\n}\n");
        return text.ToString();
    }
}
