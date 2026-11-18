using System;
using System.Linq;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Tests.AspNetCore.Generator;

/// <summary>
/// Makes each partial class marked <c>[WebAppEntry("App")]</c> implement <c>IWebAppEntry</c> for the
/// referenced assembly App, and reports VGA0001 and VGA0002.
/// </summary>
/// <remarks>
/// The entry point is read from the app's reference alone, in a stage whose input is that reference
/// and the set of names the attributes ask for. An edit to the test project changes neither, so the
/// stage does not run again.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class EntryGenerator : IIncrementalGenerator
{
    public const string EntriesStep = "Entries";
    public const string AppsStep = "Apps";

    private const string WebAppEntryAttribute = "VerificationGlue.Tests.AspNetCore.WebAppEntryAttribute";

    /// <summary>VGA0001 does not list these assemblies, or those whose names start with one of them and a dot, because they are never an app.</summary>
    private static readonly string[] Unlisted = ["System", "Microsoft", "mscorlib", "netstandard", "WindowsBase", "VerificationGlue"];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                WebAppEntryAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntax, _) => Entry((INamedTypeSymbol)syntax.TargetSymbol, (ClassDeclarationSyntax)syntax.TargetNode, syntax.Attributes[0]))
            .WithTrackingName(EntriesStep);

        var wanted = entries
            .Where(static entry => entry.Problem is null)
            .Select(static (entry, _) => entry.AssemblyName)
            .Collect()
            .Select(static (names, _) => new EquatableArray<string>(names.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray()));

        var references = context.MetadataReferencesProvider
            .Select(static (reference, _) => (Name: EntryPointReader.AssemblyName(reference), Reference: reference));

        var apps = references
            .Combine(wanted)
            .Where(static pair => pair.Left.Name is not null && pair.Right.Contains(pair.Left.Name))
            .Select(static (pair, _) => EntryPointReader.Read(pair.Left.Name!, pair.Left.Reference))
            .WithTrackingName(AppsStep)
            .Collect();

        var referenced = references
            .Select(static (pair, _) => pair.Name)
            .Collect()
            .Select(static (names, _) => new ReferencedAssemblies(names
                .OfType<string>()
                .Where(name => !Unlisted.Any(unlisted => name == unlisted || name.StartsWith(unlisted + ".", StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()));

        context.RegisterSourceOutput(entries.Combine(apps).Combine(referenced), static (output, input) =>
        {
            var ((entry, found), referencedAssemblies) = input;
            if (entry.Problem is not null)
            {
                output.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidEntryClass, entry.ClassLocation.ToLocation(), entry.Name, entry.Problem));
                return;
            }

            var app = found.FirstOrDefault(candidate => candidate.AssemblyName == entry.AssemblyName);
            if (app?.EntryPoint is null)
            {
                output.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.EntryPointNotFound,
                    entry.AttributeLocation.ToLocation(),
                    entry.AssemblyName,
                    app?.Problem ?? NotReferenced(referencedAssemblies)));
                return;
            }

            output.AddSource(EntryEmitter.HintName(entry), EntryEmitter.Emit(entry, app.EntryPoint));
        });
    }

    private static EntryClassModel Entry(INamedTypeSymbol type, ClassDeclarationSyntax declaration, AttributeData attribute)
    {
        string? problem = !declaration.Modifiers.Any(SyntaxKind.PartialKeyword) ? "is not partial"
            : type.IsGenericType ? "is generic"
            : type.ContainingType is { } outer ? $"is nested in '{outer.ToDisplayString()}'"
            : null;
        var attributeLocation = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? declaration.Identifier.GetLocation();
        return new EntryClassModel(
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            type.Name,
            attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string name ? name : "",
            problem,
            LocationModel.From(declaration.Identifier.GetLocation()),
            LocationModel.From(attributeLocation));
    }

    private static string NotReferenced(ReferencedAssemblies referenced) =>
        referenced.Names.Count == 0
            ? "the project does not reference it. It references no assembly outside .NET and VerificationGlue"
            : "the project does not reference it. It references these assemblies outside .NET and VerificationGlue: " + string.Join(", ", referenced.Names);
}
