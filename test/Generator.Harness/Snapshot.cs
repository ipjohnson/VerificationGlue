using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Generator.Harness;

/// <summary>
/// Compares text with a file named <c>Snapshots/{SpecsClass}.{Spec}.verified.txt</c> in the specs
/// project. On a difference it writes the actual text next to it as <c>.received.txt</c> and throws.
/// Set <c>VERIFICATIONGLUE_ACCEPT_SNAPSHOTS=1</c> to write the actual text as the verified file.
/// </summary>
public static class Snapshot
{
    public const string AcceptVariable = "VERIFICATIONGLUE_ACCEPT_SNAPSHOTS";
    public const string ProjectDirectoryKey = "VerificationGlue.ProjectDirectory";

    public static void Match(Type specs, string spec, string actual)
    {
        var directory = Path.Combine(ProjectDirectory(specs.Assembly), "Snapshots");
        var stem = Path.Combine(directory, $"{specs.Name}.{spec}");
        var verifiedPath = stem + ".verified.txt";
        var receivedPath = stem + ".received.txt";
        actual = Normalize(actual);

        if (Environment.GetEnvironmentVariable(AcceptVariable) == "1")
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(verifiedPath, actual);
            File.Delete(receivedPath);
            return;
        }

        string? verified = File.Exists(verifiedPath) ? Normalize(File.ReadAllText(verifiedPath)) : null;
        if (verified == actual)
        {
            File.Delete(receivedPath);
            return;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(receivedPath, actual);
        var reason = verified is null ? $"{verifiedPath} does not exist." : $"{FirstDifference(verified, actual)}";
        throw new SnapshotMismatchException(
            $"Snapshot {specs.Name}.{spec} does not match. {reason}{Environment.NewLine}" +
            $"Received: {receivedPath}{Environment.NewLine}" +
            $"Compare it with the verified file, then rerun with {AcceptVariable}=1 to accept it.");
    }

    private static string ProjectDirectory(Assembly assembly) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(attribute => attribute.Key == ProjectDirectoryKey)?.Value
        ?? throw new InvalidOperationException($"{assembly.GetName().Name} has no {ProjectDirectoryKey} assembly metadata. test/Directory.Build.props adds it to .Specs projects.");

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FirstDifference(string verified, string actual)
    {
        var expectedLines = verified.Split('\n');
        var actualLines = actual.Split('\n');
        for (var line = 0; ; line++)
        {
            var expected = line < expectedLines.Length ? expectedLines[line] : "<end of file>";
            var received = line < actualLines.Length ? actualLines[line] : "<end of file>";
            if (expected != received)
            {
                return $"Line {line + 1}: expected `{expected}`, received `{received}`.";
            }
        }
    }
}

public sealed class SnapshotMismatchException(string message) : Exception(message);

/// <summary>Snapshots of a generator run: its diagnostics, then each generated file in output order.</summary>
public static class GeneratorSnapshot
{
    public static string Render(GeneratorDriverRunResult run)
    {
        var text = new StringBuilder();
        foreach (var result in run.Results)
        {
            text.Append("// Generator: ").Append(result.Generator.GetGeneratorType().FullName).Append('\n');
            text.Append("// Diagnostics:").Append(result.Diagnostics.IsEmpty ? " none" : "").Append('\n');
            foreach (var diagnostic in result.Diagnostics)
            {
                text.Append("//   ").Append(diagnostic.ToString()).Append('\n');
            }

            foreach (var source in result.GeneratedSources)
            {
                text.Append('\n').Append("// File: ").Append(source.HintName).Append('\n');
                text.Append(source.SourceText.ToString().TrimEnd()).Append('\n');
            }
        }

        return text.ToString();
    }

    public static void Match<TSpecs>(GeneratorDriverRunResult run, [CallerMemberName] string spec = "") =>
        Snapshot.Match(typeof(TSpecs), spec, Render(run));
}
