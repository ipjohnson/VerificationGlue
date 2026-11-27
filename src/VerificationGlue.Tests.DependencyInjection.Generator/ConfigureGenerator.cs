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
/// for a <c>T</c> with no <c>Configure</c> method it can call that is not a DependencyModules module.
/// When the compilation references <c>DependencyModules.Runtime</c> and uses either attribute, it also
/// writes the source that adds the DependencyModules modules of the test's attributes.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ConfigureGenerator : IIncrementalGenerator
{
    public const string SourcesStep = "Sources";

    public const string HintName = "VerificationGlue.Tests.DependencyInjection.ServiceCollectionSources.g.cs";

    public const string ModulesHintName = "VerificationGlue.Tests.DependencyInjection.DependencyModules.g.cs";

    private const string ServiceCollectionAttribute = "VerificationGlue.Tests.DependencyInjection.ServiceCollectionAttribute`1";
    private const string PlainServiceCollectionAttribute = "VerificationGlue.Tests.DependencyInjection.ServiceCollectionAttribute";
    private const string IServiceCollection = "Microsoft.Extensions.DependencyInjection.IServiceCollection";
    private const string TestContainerContext = "VerificationGlue.Tests.TestContainerContext";
    private const string DependencyModulesRuntime = "DependencyModules.Runtime";
    private const string IDependencyModule = "DependencyModules.Runtime.Interfaces.IDependencyModule";
    private const string DependencyModuleAttribute = "DependencyModules.Runtime.Attributes.DependencyModuleAttribute";

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

        var usesPlain = context.SyntaxProvider
            .ForAttributeWithMetadataName(PlainServiceCollectionAttribute, static (_, _) => true, static (_, _) => true)
            .Collect()
            .Select(static (uses, _) => uses.Length > 0);

        var referencesModules = context.CompilationProvider
            .Select(static (compilation, _) => compilation.ReferencedAssemblyNames.Any(identity => identity.Name == DependencyModulesRuntime));

        context.RegisterSourceOutput(sources.Combine(usesPlain).Combine(referencesModules), static (output, input) =>
        {
            var ((models, plain), modules) = input;
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

            if (modules && (plain || models.Length > 0))
            {
                output.AddSource(ModulesHintName, EmitModules(registered.Where(model => model.IsModule).ToList()));
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
            return new SourceModel(typeName, displayName, accepted.Shape == 2, false, null, location);
        }

        if (IsModule(type, compilation))
        {
            return new SourceModel(typeName, displayName, false, true, WhyNotCreatable(type, compilation), location);
        }

        var problem = candidates.Count == 0
            ? "it has no method named Configure"
            : (candidates.Count == 1 ? "its method " : "its methods ") +
              string.Join(" and ", candidates.Select(method => "'" + method.ToDisplayString(SignatureFormat) + "'")) +
              (candidates.Count == 1 ? " does not have one of these signatures" : " do not have one of these signatures");
        return new SourceModel(typeName, displayName, false, false, problem, location);
    }

    /// <summary>
    /// True when <paramref name="type"/> implements <c>IDependencyModule</c>, or is a class marked
    /// <c>[DependencyModule]</c>. The DependencyModules generator adds the interface to such a class in
    /// output this generator cannot see.
    /// </summary>
    private static bool IsModule(ITypeSymbol type, Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(IDependencyModule) is not { } module)
        {
            return false;
        }

        if (type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, module)))
        {
            return true;
        }

        var marker = compilation.GetTypeByMetadataName(DependencyModuleAttribute);
        return type.TypeKind == TypeKind.Class && marker is not null &&
            type.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, marker));
    }

    /// <summary>Why the generated code cannot write <c>new T()</c> for the module <paramref name="type"/>, or null.</summary>
    private static string? WhyNotCreatable(ITypeSymbol type, Compilation compilation)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } named)
        {
            return "it is a DependencyModules module that is not a class 'new T()' can create";
        }

        return named.InstanceConstructors.Any(constructor =>
                constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public &&
                compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly))
            ? null
            : "it is a DependencyModules module without a public parameterless constructor";
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
                .Append(source.IsModule
                    ? ">(static (services, context) => { });\n"
                    : ">(static (services, context) => " + source.TypeName + (source.TakesContext ? ".Configure(services, context));\n" : ".Configure(services));\n"));
        }

        text.Append("        }\n    }\n}\n");
        return text.ToString();
    }

    /// <summary>
    /// The source that adds the DependencyModules modules of a container's attributes in one
    /// <c>AddModules</c> call: each <c>IDependencyModuleProvider</c> through <c>GetModule()</c>, each
    /// <c>IDependencyModule</c>, and <c>new T()</c> for each <c>[ServiceCollection&lt;T&gt;]</c> in
    /// <paramref name="modules"/>.
    /// </summary>
    internal static string EmitModules(IReadOnlyList<SourceModel> modules)
    {
        const string Module = "global::DependencyModules.Runtime.Interfaces.IDependencyModule";
        var text = new StringBuilder("// <auto-generated/>\n#nullable enable\n#pragma warning disable\n");
        text.Append("namespace VerificationGlue.Tests.DependencyInjection.Generated\n{\n");
        text.Append("    internal static class DependencyModulesSource\n    {\n");
        text.Append("        [global::System.Runtime.CompilerServices.ModuleInitializer]\n");
        text.Append("        internal static void Register() =>\n");
        text.Append("            global::VerificationGlue.Tests.DependencyInjection.ServiceCollectionRegistry.AddSource(AddModules);\n\n");
        text.Append("        // AddModules inserts each module before the ones it has, so the attributes are read from the\n");
        text.Append("        // method back to the assembly, and the method's modules are applied last.\n");
        text.Append("        private static void AddModules(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, ");
        text.Append("global::System.Collections.Generic.IReadOnlyList<global::System.Attribute> attributes)\n        {\n");
        text.Append("            var modules = new global::System.Collections.Generic.List<").Append(Module).Append(">();\n");
        text.Append("            for (var i = attributes.Count - 1; i >= 0; i--)\n            {\n");
        text.Append("                switch (attributes[i])\n                {\n");
        foreach (var module in modules)
        {
            text.Append("                    case global::VerificationGlue.Tests.DependencyInjection.ServiceCollectionAttribute<").Append(module.TypeName).Append(">:\n");
            text.Append("                        modules.Add(new ").Append(module.TypeName).Append("());\n");
            text.Append("                        break;\n");
        }

        text.Append("                    case global::DependencyModules.Runtime.Interfaces.IDependencyModuleProvider provider:\n");
        text.Append("                        modules.Add(provider.GetModule());\n");
        text.Append("                        break;\n");
        text.Append("                    case ").Append(Module).Append(" module:\n");
        text.Append("                        modules.Add(module);\n");
        text.Append("                        break;\n");
        text.Append("                }\n            }\n\n");
        text.Append("            if (modules.Count > 0)\n            {\n");
        text.Append("                global::DependencyModules.Runtime.ServiceCollectionExtensions.AddModules(services, modules.ToArray());\n");
        text.Append("            }\n        }\n    }\n}\n");
        return text.ToString();
    }
}
