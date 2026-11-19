using Generator.Harness;

namespace VerificationGlue.Generator.Specs;

/// <summary>
/// Snapshots of generated code for real sample files. <c>docs/guide/generated/interceptors.md</c>
/// shows these snapshots.
/// </summary>
public class GeneratedCodeSpecs
{
    [VerifyTest]
    public void Interceptors_for_the_xunit_sample(CancellationToken cancellationToken)
    {
        var samples = Path.Combine(RepositoryRoot(), "samples");
        var compilation = GeneratorHarness.Compilation(
            (GeneratorHarness.ProjectDirectory + "OrderTests.cs", File.ReadAllText(Path.Combine(samples, "VerifyInXunit", "OrderTests.cs"))),
            ("/repo/Shop/Shop.cs", File.ReadAllText(Path.Combine(samples, "Shop", "Shop.cs"))),
            ("/repo/Project/ImplicitUsings.cs", "global using System; global using System.Threading.Tasks;"));

        var result = GeneratorHarness.Generate(compilation, new VerificationGlueGenerator(), out _);

        var generated = result.GeneratedTrees
            .OrderBy(tree => tree.FilePath, StringComparer.Ordinal)
            .Select(tree => $"// {Path.GetFileName(tree.FilePath)}\n{tree.GetText(cancellationToken)}");
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Interceptors_for_the_xunit_sample), string.Join("\n", generated));
    }

    [VerifyTest]
    public void Rewritten_method_for_the_messages_sample(CancellationToken cancellationToken)
    {
        var result = GenerateMessagesSample();

        var copy = result.GeneratedTrees.Single(tree => tree.FilePath.Contains(".Tier3Messages.VerifyShipping.", StringComparison.Ordinal));
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Rewritten_method_for_the_messages_sample), copy.GetText(cancellationToken).ToString());
    }

    [VerifyTest]
    public void Redirects_for_the_messages_sample(CancellationToken cancellationToken)
    {
        var result = GenerateMessagesSample();

        var redirects = result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith(".Redirects.g.cs", StringComparison.Ordinal));
        Snapshot.Match(typeof(GeneratedCodeSpecs), nameof(Redirects_for_the_messages_sample), redirects.GetText(cancellationToken).ToString());
    }

    [VerifyTest]
    [Row("ThrowsExactly")]
    [Row("VerifyEquivalent")]
    public void A_verify_helper_named_like_a_VerifySyntax_method_still_gets_a_redirect(string name, CancellationToken cancellationToken)
    {
        var source = $$"""
            using VerificationGlue;
            using static VerificationGlue.VerifySyntax;

            public static partial class Helpers
            {
                [Verify]
                public static void {{name}}(int count) => Verify(count == 3);
            }

            public static class Caller
            {
                public static void Run() => Helpers.{{name}}(3);
            }
            """;
        var compilation = GeneratorHarness.Compilation((GeneratorHarness.ProjectDirectory + "Input.cs", source));

        var result = GeneratorHarness.Generate(compilation, new VerificationGlueGenerator(), out _, rewrite: true);

        var redirects = result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith(".Redirects.g.cs", StringComparison.Ordinal));
        Assert.Contains($"Helpers.{name}__Verified(", redirects.GetText(cancellationToken).ToString());
    }

    private static Microsoft.CodeAnalysis.GeneratorDriverRunResult GenerateMessagesSample()
    {
        var messages = Path.Combine(RepositoryRoot(), "samples", "Messages");
        var compilation = GeneratorHarness.Compilation(
            (GeneratorHarness.ProjectDirectory + "Tier3Messages.cs", File.ReadAllText(Path.Combine(messages, "Tier3Messages.cs"))),
            (GeneratorHarness.ProjectDirectory + "Order.cs", File.ReadAllText(Path.Combine(messages, "Order.cs"))),
            ("/repo/Project/ImplicitUsings.cs", "global using System; global using System.Collections.Generic;"));

        return GeneratorHarness.Generate(compilation, new VerificationGlueGenerator(), out _, rewrite: true);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VerificationGlue.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No VerificationGlue.slnx above {AppContext.BaseDirectory}.");
    }
}
