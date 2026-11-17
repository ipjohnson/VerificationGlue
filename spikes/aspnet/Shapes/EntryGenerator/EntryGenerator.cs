using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Shapes.Generator;

/// <summary>
/// Fills in a partial class marked [WebAppEntry("AssemblyName")] with an UnsafeAccessor call to the
/// entry point of the referenced app assembly.
/// </summary>
[Generator]
public sealed class EntryGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor s_notFound = new(
        "SHAPE001",
        "No entry point",
        "Found no entry point in the referenced assembly '{0}': {1}",
        "Shapes",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Shapes.Tests.WebAppEntryAttribute",
            static (node, _) => node is ClassDeclarationSyntax,
            static (attribute, _) => (
                Namespace: attribute.TargetSymbol.ContainingNamespace.IsGlobalNamespace ? null : attribute.TargetSymbol.ContainingNamespace.ToDisplayString(),
                Name: attribute.TargetSymbol.Name,
                Assembly: attribute.Attributes[0].ConstructorArguments[0].Value as string ?? string.Empty,
                Location: attribute.TargetNode.GetLocation()));

        context.RegisterSourceOutput(targets.Combine(context.CompilationProvider), static (output, pair) =>
        {
            var (target, compilation) = pair;
            var (entry, seen) = Find((CSharpCompilation)compilation, target.Assembly);
            if (entry is null)
            {
                output.ReportDiagnostic(Diagnostic.Create(s_notFound, target.Location, target.Assembly, seen));
                return;
            }

            output.AddSource($"{target.Name}.g.cs", Write(target.Namespace, target.Name, target.Assembly, entry, seen));
        });
    }

    private static (IMethodSymbol? Entry, string Seen) Find(CSharpCompilation compilation, string assemblyName)
    {
        // A metadata reference shows private members only with MetadataImportOptions.All. A
        // reference assembly keeps one private method: the entry point the PE header names.
        var all = compilation.WithOptions(compilation.Options.WithMetadataImportOptions(MetadataImportOptions.All));
        var assembly = all.References
            .Select(all.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .FirstOrDefault(candidate => candidate.Name == assemblyName);
        if (assembly is null)
        {
            return (null, "the test project does not reference it");
        }

        var candidates = Types(assembly.GlobalNamespace)
            .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>())
            .Where(IsEntryPoint)
            .ToList();
        var seen = candidates.Count == 0
            ? "no static method named Main, <Main> or <Main>$ with an entry point signature"
            : string.Join(", ", candidates.Select(Describe));

        // <Main>$ and Main are what the user wrote. <Main> is the wrapper the compiler adds for an
        // async entry point, and it is the only one left in a reference assembly.
        var entry = candidates.FirstOrDefault(method => method.Name is "<Main>$" or "Main")
            ?? candidates.FirstOrDefault(method => method.Name == "<Main>");
        return candidates.Select(method => method.ContainingType).Distinct(SymbolEqualityComparer.Default).Count() > 1
            ? (null, "more than one type has an entry point: " + seen)
            : (entry, seen);
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceOrTypeSymbol nested)
            {
                if (nested is INamedTypeSymbol type)
                {
                    yield return type;
                }

                foreach (var inner in Types(nested))
                {
                    yield return inner;
                }
            }
        }
    }

    private static bool IsEntryPoint(IMethodSymbol method) =>
        method.IsStatic
        && method.Name is "Main" or "<Main>" or "<Main>$"
        && !method.IsGenericMethod
        && (method.Parameters.Length == 0
            || (method.Parameters.Length == 1 && method.Parameters[0].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String }))
        && ReturnKind(method) is not null;

    private static string? ReturnKind(IMethodSymbol method) => method.ReturnType switch
    {
        { SpecialType: SpecialType.System_Void } => "void",
        { SpecialType: SpecialType.System_Int32 } => "int",
        INamedTypeSymbol { Name: "Task", Arity: 0 } task when task.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" => "Task",
        INamedTypeSymbol { Name: "Task", Arity: 1 } task when task.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks"
            && task.TypeArguments[0].SpecialType == SpecialType.System_Int32 => "Task<int>",
        _ => null,
    };

    private static string Describe(IMethodSymbol method) =>
        $"{method.DeclaredAccessibility.ToString().ToLowerInvariant()} static {ReturnKind(method)} {method.ContainingType.ToDisplayString()}.{method.Name}({(method.Parameters.Length == 0 ? string.Empty : "string[]")})";

    private static string Write(string? ns, string name, string assemblyName, IMethodSymbol entry, string seen)
    {
        var kind = ReturnKind(entry)!;
        var hasArgs = entry.Parameters.Length == 1;
        var call = hasArgs ? "EntryPoint(null, args)" : "EntryPoint(null)";
        var body = kind switch
        {
            "void" => $"{call};\n        return 0;",
            "int" => $"return {call};",
            "Task" => $"{call}.GetAwaiter().GetResult();\n        return 0;",
            _ => $"return {call}.GetAwaiter().GetResult();",
        };
        var returnType = kind switch
        {
            "Task" => "global::System.Threading.Tasks.Task",
            "Task<int>" => "global::System.Threading.Tasks.Task<int>",
            _ => kind,
        };
        var typeName = MetadataName(entry.ContainingType);

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine($"// Candidates the generator saw in {assemblyName}: {seen}");
        source.AppendLine("#nullable enable");
        if (ns is not null)
        {
            source.AppendLine($"namespace {ns};");
        }

        source.AppendLine();
        source.AppendLine($"partial class {name} : global::Shapes.Tests.IWebAppEntry");
        source.AppendLine("{");
        source.AppendLine($"    public static string ApplicationName => \"{assemblyName}\";");
        source.AppendLine();
        source.AppendLine("    public static int Run(string[] args)");
        source.AppendLine("    {");
        source.AppendLine($"        {body}");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine($"    [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = \"{entry.Name}\")]");
        source.AppendLine($"    private static extern {returnType} EntryPoint([global::System.Runtime.CompilerServices.UnsafeAccessorType(\"{typeName}, {assemblyName}\")] object? type{(hasArgs ? ", string[] args" : string.Empty)});");
        source.AppendLine("}");
        return source.ToString();
    }

    private static string MetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            name = outer.MetadataName + "+" + name;
        }

        return type.ContainingNamespace.IsGlobalNamespace ? name : type.ContainingNamespace.ToDisplayString() + "." + name;
    }
}
