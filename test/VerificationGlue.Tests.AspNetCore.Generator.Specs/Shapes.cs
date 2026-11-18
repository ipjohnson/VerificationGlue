using System.Reflection;
using System.Runtime.Loader;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace VerificationGlue.Tests.AspNetCore.Generator.Specs;

/// <summary>
/// The entry point shapes of docs/research/aspnet-report.md, without ASP.NET Core. Each app stores
/// the arguments it was called with in <see cref="AppContext"/> under its own name, and an app whose
/// entry point returns a number returns 7.
/// </summary>
internal static class Shapes
{
    public const int ExitCode = 7;

    private const string Record = "System.AppContext.SetData(Name, string.Join(\" \", args));";

    private static readonly Dictionary<string, Shape> s_all = new Shape[]
    {
        new("VoidApp", 0, [$"const string Name = \"VoidApp\"; {Record}"]),
        new("IntApp", ExitCode, [$"const string Name = \"IntApp\"; {Record} return {ExitCode};"]),
        new("TaskApp", 0, [$"const string Name = \"TaskApp\"; await System.Threading.Tasks.Task.Yield(); {Record}"]),
        new("TaskIntApp", ExitCode, [$"const string Name = \"TaskIntApp\"; await System.Threading.Tasks.Task.Yield(); {Record} return {ExitCode};"]),
        new("MainApp", ExitCode, [$$"""
            namespace MainApp;

            public static class Server
            {
                private const string Name = "MainApp";

                private static async System.Threading.Tasks.Task<int> Main(string[] args)
                {
                    await System.Threading.Tasks.Task.Yield();
                    {{Record}}
                    return {{ExitCode}};
                }
            }
            """]),
        new("InternalApp", 0, [$"const string Name = \"InternalApp\"; {Record}", "internal partial class Program;"]),
        new("InternalIvtApp", 0,
        [
            $"const string Name = \"InternalIvtApp\"; {Record}",
            "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"SpecsInput\")] internal partial class Program;",
        ]),
        new("StartupApp", 0,
        [
            "new StartupApp.Startup().Configure(args);",
            $$"""
            namespace StartupApp;

            public sealed class Startup
            {
                private const string Name = "StartupApp";

                public void Configure(string[] args) => {{Record}}
            }
            """,
        ]),
    }.ToDictionary(shape => shape.Name);

    public static IEnumerable<string> Names => s_all.Keys;

    public static Shape Get(string name) => s_all[name];
}

/// <summary>An app with one entry point shape, compiled in memory.</summary>
internal sealed record Shape(string Name, int ExpectedRun, string[] Sources)
{
    private readonly Lazy<(byte[] Full, byte[] Reference)> _images = new(() => Compile(Name, Sources, OutputKind.ConsoleApplication));

    /// <summary>The assembly the app runs as.</summary>
    public byte[] Full => _images.Value.Full;

    /// <summary>The reference assembly that a project reference passes to the compiler in a build.</summary>
    public byte[] Reference => _images.Value.Reference;

    public static (byte[] Full, byte[] Reference) Compile(string name, string[] sources, OutputKind kind)
    {
        var compilation = CSharpCompilation.Create(
            name,
            sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: $"{name}{index}.cs")),
            PlatformReferences.All,
            new CSharpCompilationOptions(kind, nullableContextOptions: NullableContextOptions.Enable));
        return (Emit(compilation, new EmitOptions()), Emit(compilation, new EmitOptions(metadataOnly: true, includePrivateMembers: false)));
    }

    private static byte[] Emit(CSharpCompilation compilation, EmitOptions options)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, options: options);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return stream.ToArray();
    }
}

/// <summary>Loads a test assembly and the app it calls, so the generated accessor resolves the app by name.</summary>
internal sealed class ShapeLoadContext(Dictionary<string, byte[]> images) : AssemblyLoadContext(isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName) =>
        assemblyName.Name is { } name && images.TryGetValue(name, out var image) ? LoadFromStream(new MemoryStream(image)) : null;
}
