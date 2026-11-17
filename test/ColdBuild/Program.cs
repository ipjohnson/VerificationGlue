using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColdBuild;

/// <summary>
/// Writes the benchmark assemblies, rebuilds each one cold with the generator off, with tier 2 alone
/// and with tier 3, and reports the added seconds and the ratio to the generator-off build.
/// </summary>
/// <remarks>
/// Options: <c>--tests</c> (10000), <c>--calls-per-test</c> (10), <c>--assemblies</c> (3),
/// <c>--repeat</c> (3) and <c>--output</c> (artifacts/cold-build).
/// </remarks>
public static class Program
{
    public static int Main(string[] args)
    {
        var options = Options.Parse(args);
        var output = Path.GetFullPath(options.Output);
        var work = Path.Combine(output, "work");
        if (Directory.Exists(work))
        {
            Directory.Delete(work, recursive: true);
        }

        Directory.CreateDirectory(work);
        var lib = Path.Combine(work, "lib");
        Directory.CreateDirectory(lib);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "ColdBuild.Runtime.dll"), Path.Combine(lib, "ColdBuild.Runtime.dll"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "generator", "ColdBuild.Generator.dll"), Path.Combine(lib, "ColdBuild.Generator.dll"));

        // Keeps the repository's props, targets and central package versions out of the benchmark projects.
        foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
        {
            File.WriteAllText(Path.Combine(work, name), "<Project />\n");
        }

        var projects = new List<string>();
        var calls = 0;
        for (var index = 0; index < options.Assemblies; index++)
        {
            var name = $"Bench.Tests{index}";
            var tests = (options.Tests / options.Assemblies) + (index < options.Tests % options.Assemblies ? 1 : 0);
            var sources = new TestSources(name, tests, options.CallsPerTest);
            calls += sources.Calls;
            var directory = Path.Combine(work, name);
            Directory.CreateDirectory(directory);
            foreach (var (path, text) in sources.Files())
            {
                File.WriteAllText(Path.Combine(directory, path), text);
            }

            var project = Path.Combine(directory, name + ".csproj");
            File.WriteAllText(project, ProjectFile);
            projects.Add(project);
            Dotnet("restore", project, "--nologo", "-v:q");
        }

        Console.WriteLine($"Wrote {options.Tests:N0} tests with {calls:N0} Verify calls in {options.Assemblies} assemblies under {work}.");

        var tiers = Enum.GetValues<Tier>();
        var runs = new List<Run>();
        var generated = new Dictionary<Tier, GeneratedCounts>();
        for (var repeat = 0; repeat < options.Repeat; repeat++)
        {
            // Rotating the order spreads any drift in machine load across the three configurations.
            for (var step = 0; step < tiers.Length; step++)
            {
                var tier = tiers[(repeat + step) % tiers.Length];
                var seconds = projects.Select(project => Build(project, tier)).ToArray();
                runs.Add(new Run(tier, repeat, seconds));
                Console.WriteLine($"Run {repeat + 1}, {Report.Name(tier)}: {string.Join(" + ", seconds.Select(value => value.ToString("F1", CultureInfo.InvariantCulture)))} s");
                generated[tier] = Count(projects);
            }
        }

        var report = new Report(options.Assemblies, options.Tests, calls, runs, generated, Describe());
        Check(report);
        var markdown = report.ToMarkdown();
        Console.WriteLine();
        Console.WriteLine(markdown);
        File.WriteAllText(Path.Combine(output, "report.md"), markdown);
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            report.Assemblies,
            report.Tests,
            report.Calls,
            report.Environment,
            Results = report.Results.Select(result => new
            {
                Build = Report.Name(result.Tier),
                result.MedianSeconds,
                result.AddedSeconds,
                result.AddedSecondsAt100000Calls,
                result.RatioToGeneratorOff,
                result.Generated,
            }),
            Runs = report.Runs.Select(run => new { Build = Report.Name(run.Tier), Run = run.Repeat + 1, run.AssemblySeconds, run.TotalSeconds }),
        }, new JsonSerializerOptions { WriteIndented = true }));

        if (System.Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summary)
        {
            File.AppendAllText(summary, markdown);
        }

        return 0;
    }

    private const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <LangVersion>latest</LangVersion>
            <InterceptorsNamespaces>$(InterceptorsNamespaces);VerificationGlue.Generated</InterceptorsNamespaces>
            <ColdBuildRewrite Condition="'$(ColdBuildTier)' == 'Tier3'">true</ColdBuildRewrite>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="ColdBuild.Runtime" HintPath="../lib/ColdBuild.Runtime.dll" />
            <Analyzer Include="../lib/ColdBuild.Generator.dll" Condition="'$(ColdBuildTier)' != 'GeneratorOff'" />
            <CompilerVisibleProperty Include="ColdBuildRewrite" />
          </ItemGroup>
        </Project>

        """;

    private static double Build(string project, Tier tier)
    {
        var timer = Stopwatch.StartNew();
        Dotnet("build", project, "-c", "Release", "--no-restore", "--no-incremental", "--disable-build-servers", "--nologo", "-v:q", $"-p:ColdBuildTier={tier}");
        return timer.Elapsed.TotalSeconds;
    }

    /// <summary>Counts the interceptors, copies and redirects in the assemblies the last build wrote.</summary>
    private static GeneratedCounts Count(IEnumerable<string> projects)
    {
        int interceptors = 0, copies = 0, redirects = 0;
        foreach (var project in projects)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var assembly = Path.Combine(Path.GetDirectoryName(project)!, "bin", "Release", "net10.0", name + ".dll");
            using var stream = File.OpenRead(assembly);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            foreach (var handle in metadata.MethodDefinitions)
            {
                var method = metadata.GetString(metadata.GetMethodDefinition(handle).Name);
                interceptors += method.StartsWith("Verify_", StringComparison.Ordinal) ? 1 : 0;
                copies += method.EndsWith("__Verified", StringComparison.Ordinal) ? 1 : 0;
                redirects += method.StartsWith("Redirect_", StringComparison.Ordinal) ? 1 : 0;
            }
        }

        return new GeneratedCounts(interceptors, copies, redirects);
    }

    /// <summary>Fails the run when a configuration did not get the generator output it is meant to time.</summary>
    private static void Check(Report report)
    {
        foreach (var result in report.Results)
        {
            var generated = result.Generated;
            var expected = result.Tier switch
            {
                Tier.GeneratorOff => generated is { Interceptors: 0, Copies: 0, Redirects: 0 },
                Tier.Tier2 => generated.Interceptors == report.Calls && generated is { Copies: 0, Redirects: 0 },
                _ => generated.Copies > 0 && generated.Redirects > 0 && generated.Interceptors < report.Calls,
            };
            if (!expected)
            {
                throw new InvalidOperationException($"{Report.Name(result.Tier)} built with unexpected generator output: {generated}.");
            }
        }
    }

    private static string Describe() =>
        $"Machine: {RuntimeInformation.OSDescription.Trim()}, {RuntimeInformation.OSArchitecture}, {System.Environment.ProcessorCount} logical processors, .NET SDK {Capture("--version").Trim()}.";

    private static void Dotnet(params string[] args)
    {
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet {string.Join(' ', args)} exited with {process.ExitCode}.\n{stdout.Result}\n{stderr}");
        }
    }

    private static string Capture(string arg)
    {
        var info = new ProcessStartInfo("dotnet", arg) { RedirectStandardOutput = true };
        using var process = Process.Start(info)!;
        var text = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return text;
    }

    private sealed record Options(int Tests, int CallsPerTest, int Assemblies, int Repeat, string Output)
    {
        public static Options Parse(string[] args)
        {
            var options = new Options(10_000, 10, 3, 3, Path.Combine("artifacts", "cold-build"));
            for (var index = 0; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"{args[index]} needs a value.");
                }

                var value = args[index + 1];
                options = args[index] switch
                {
                    "--tests" => options with { Tests = int.Parse(value, CultureInfo.InvariantCulture) },
                    "--calls-per-test" => options with { CallsPerTest = int.Parse(value, CultureInfo.InvariantCulture) },
                    "--assemblies" => options with { Assemblies = int.Parse(value, CultureInfo.InvariantCulture) },
                    "--repeat" => options with { Repeat = int.Parse(value, CultureInfo.InvariantCulture) },
                    "--output" => options with { Output = value },
                    _ => throw new ArgumentException($"Unknown option {args[index]}."),
                };
            }

            return options;
        }
    }
}
