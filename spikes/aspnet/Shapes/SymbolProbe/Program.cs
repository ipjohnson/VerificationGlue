// Compiles each entry point shape as an app, then a test compilation that references it, and prints
// what the test compilation can see of the app's entry point. A generator in the test project sees
// the same compilation.
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
    .Split(Path.PathSeparator)
    .Select(path => MetadataReference.CreateFromFile(path))
    .ToArray();

(string Name, string Source)[] shapes =
[
    ("void", "System.Console.WriteLine(args.Length);"),
    ("int", "System.Console.WriteLine(args.Length);\nreturn 0;"),
    ("Task", "await System.Threading.Tasks.Task.Yield();"),
    ("Task<int>", "await System.Threading.Tasks.Task.Yield();\nreturn 0;"),
    ("explicit Main", "namespace App { public class Server { private static System.Threading.Tasks.Task<int> Main(string[] args) => System.Threading.Tasks.Task.FromResult(0); } }"),
    ("internal Program", "System.Console.WriteLine(args.Length);\ninternal partial class Program;"),
    ("internal Program, IVT", "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"Tests\")]\nSystem.Console.WriteLine(args.Length);\ninternal partial class Program;"),
    ("public Program", "System.Console.WriteLine(args.Length);\npublic partial class Program;"),
];

foreach (var (name, source) in shapes)
{
    var app = CSharpCompilation.Create(
        "App",
        [CSharpSyntaxTree.ParseText(source)],
        platform,
        new CSharpCompilationOptions(OutputKind.ConsoleApplication));
    var full = Emit(app, metadataOnly: false);
    var reference = Emit(app, metadataOnly: true);
    var entry = app.GetEntryPoint(default)!;
    Console.WriteLine($"## {name}: the app's entry point is {entry.ContainingType.ToDisplayString()}.{entry.Name}, returns {entry.ReturnType.ToDisplayString()}, PE entry point {MetadataName(full, EntryToken(full))}");

    foreach (var (kind, appReference) in new (string, MetadataReference)[]
    {
        ("CompilationReference (IDE)", app.ToMetadataReference()),
        ("full assembly", MetadataReference.CreateFromImage(full)),
        ("reference assembly (build)", MetadataReference.CreateFromImage(reference)),
    })
    {
        foreach (var import in new[] { MetadataImportOptions.Public, MetadataImportOptions.All })
        {
            var tests = CSharpCompilation.Create(
                "Tests",
                [],
                [.. platform, appReference],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: import));
            var assembly = (IAssemblySymbol)tests.GetAssemblyOrModuleSymbol(appReference)!;
            var type = assembly.GetTypeByMetadataName(entry.ContainingType.ToDisplayString());
            var members = type is null
                ? "type not found"
                : $"{type.DeclaredAccessibility} {type.Name}: " + string.Join(", ", type.GetMembers().OfType<IMethodSymbol>()
                    .Where(method => method.MethodKind == MethodKind.Ordinary)
                    .Select(method => $"{method.DeclaredAccessibility} {method.ReturnType.ToDisplayString()} {method.Name}"));
            Console.WriteLine($"  {kind}, import {import}: {members}");
        }

        Console.WriteLine($"  {kind}, EntryGenerator writes: {RunGenerator(platform, appReference)}");
    }

    Console.WriteLine($"  reference assembly read with System.Reflection.Metadata: {Methods(reference, entry.ContainingType.Name)}");
}

static string RunGenerator(MetadataReference[] platform, MetadataReference app)
{
    const string tests = """
        namespace Shapes.Tests;
        [System.AttributeUsage(System.AttributeTargets.Class)]
        public sealed class WebAppEntryAttribute(string assemblyName) : System.Attribute;
        [WebAppEntry("App")]
        public sealed partial class AppEntry;
        """;
    var compilation = CSharpCompilation.Create(
        "Tests",
        [CSharpSyntaxTree.ParseText(tests)],
        [.. platform, app],
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    var result = CSharpGeneratorDriver.Create(new Shapes.Generator.EntryGenerator()).RunGenerators(compilation).GetRunResult();
    var source = result.GeneratedTrees.SingleOrDefault()?.ToString();
    return source is null
        ? string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.GetMessage()))
        : source.Split('\n').Single(line => line.Contains("extern")).Trim().Replace("global::System.Runtime.CompilerServices.", "");
}

static ImmutableArray<byte> Emit(CSharpCompilation compilation, bool metadataOnly)
{
    using var stream = new MemoryStream();
    // A build's reference assembly, from /refout, leaves out private members.
    var result = compilation.Emit(stream, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(metadataOnly: metadataOnly, includePrivateMembers: !metadataOnly));
    if (!result.Success)
    {
        throw new InvalidOperationException(string.Join("\n", result.Diagnostics));
    }

    return [.. stream.ToArray()];
}

static int EntryToken(ImmutableArray<byte> image)
{
    using var pe = new System.Reflection.PortableExecutable.PEReader(image);
    return pe.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress;
}

static string MetadataName(ImmutableArray<byte> image, int token)
{
    using var pe = new System.Reflection.PortableExecutable.PEReader(image);
    var reader = pe.GetMetadataReader();
    var method = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(token & 0xFFFFFF));
    return $"{reader.GetString(reader.GetTypeDefinition(method.GetDeclaringType()).Name)}.{reader.GetString(method.Name)}";
}

static string Methods(ImmutableArray<byte> image, string typeName)
{
    using var pe = new System.Reflection.PortableExecutable.PEReader(image);
    var reader = pe.GetMetadataReader();
    var type = reader.TypeDefinitions.Select(reader.GetTypeDefinition).FirstOrDefault(t => reader.GetString(t.Name) == typeName);
    return type.Name.IsNil
        ? "type not found"
        : string.Join(", ", type.GetMethods().Select(handle => reader.GetString(reader.GetMethodDefinition(handle).Name)));
}
