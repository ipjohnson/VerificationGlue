using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace VerificationGlue.Tests.Generator.Specs;

/// <summary>Compiles a test source with the generators, loads it, and runs its registered tests through the runner.</summary>
internal static class Registrations
{
    /// <summary>The emitted tests by ID, each as a function that runs it through the runner and returns its failures.</summary>
    public static Dictionary<string, Func<Task<IReadOnlyList<Exception>>>> Load(string source, bool rewrite, params IIncrementalGenerator[] generators)
    {
        var input = GeneratorHarness.Compilation([("/repo/Tests/OrderTests.cs", source)]);
        GeneratorHarness.Driver(rewrite, generators)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics, TestContext.Current.CancellationToken);
        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        using var stream = new MemoryStream();
        var emitted = output.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        stream.Position = 0;
        var assembly = new AssemblyLoadContext("Registrations", isCollectible: true).LoadFromStream(stream);
        var registry = assembly.GetType("VerificationGlue.Tests.TestRegistry", throwOnError: true)!;
        var descriptorType = assembly.GetType("VerificationGlue.Tests.TestDescriptor", throwOnError: true)!;
        var runnerType = assembly.GetType("VerificationGlue.Tests.TestRunner", throwOnError: true)!;
        var uid = descriptorType.GetProperty("Uid")!;
        var runAsync = runnerType.GetMethod("RunAsync")!;

        var descriptors = ((System.Collections.IEnumerable)registry.GetProperty("Tests")!.GetValue(null)!).Cast<object>().ToArray();
        var selected = Array.CreateInstance(descriptorType, descriptors.Length);
        Array.Copy(descriptors, selected, descriptors.Length);
        var runner = Activator.CreateInstance(runnerType, [selected, CancellationToken.None])!;

        var tests = new Dictionary<string, Func<Task<IReadOnlyList<Exception>>>>();
        foreach (var descriptor in descriptors)
        {
            tests.Add((string)uid.GetValue(descriptor)!, async () =>
            {
                var task = (Task)runAsync.Invoke(runner, [descriptor])!;
                await task;
                var outcome = task.GetType().GetProperty("Result")!.GetValue(task)!;
                return (IReadOnlyList<Exception>)outcome.GetType().GetProperty("Failures")!.GetValue(outcome)!;
            });
        }

        return tests;
    }
}
