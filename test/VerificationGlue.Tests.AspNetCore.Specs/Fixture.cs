using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace VerificationGlue.Tests.AspNetCore.Specs;

/// <summary>
/// Builds test/VerificationGlue.Tests.AspNetCore.Fixture once in the configuration of these specs, runs
/// every test in it with <c>--report-trx</c>, and reads the results by test name. <see cref="SerialResults"/>
/// runs it again with RuntimeFeature.IsDynamicCodeSupported switched off, as it is under Native AOT.
/// </summary>
internal static class Fixture
{
    private const string DynamicCodeSwitch = "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported";
    private const string Executable = "VerificationGlue.Tests.AspNetCore.Fixture";
    private static readonly Lazy<string> s_output = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<Dictionary<string, TrxResult>> s_results = new(() => Run(s_output.Value), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<Dictionary<string, TrxResult>> s_serialResults = new(RunWithoutDynamicCode, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyDictionary<string, TrxResult> Results => s_results.Value;

    public static IReadOnlyDictionary<string, TrxResult> SerialResults => s_serialResults.Value;

    private static string Build()
    {
        var configuration = typeof(Fixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "VerificationGlue.Configuration").Value!;
        var project = Path.Combine(FindRoot(), "test", "VerificationGlue.Tests.AspNetCore.Fixture");
        var build = Process("dotnet", project, "build", "-c", configuration);
        Assert.True(build.ExitCode == 0, "dotnet build of the fixture failed:\n" + build.Output);
        return Path.Combine(project, "bin", configuration, "net10.0");
    }

    /// <summary>Runs a copy of the fixture whose runtimeconfig.json turns dynamic code off.</summary>
    private static Dictionary<string, TrxResult> RunWithoutDynamicCode()
    {
        var copy = Directory.CreateTempSubdirectory("vg-aspnet-serial-");
        try
        {
            foreach (var file in Directory.EnumerateFiles(s_output.Value, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(copy.FullName, Path.GetRelativePath(s_output.Value, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }

            var runtimeConfig = Path.Combine(copy.FullName, Executable + ".runtimeconfig.json");
            var json = JsonNode.Parse(File.ReadAllText(runtimeConfig))!;
            var options = json["runtimeOptions"]!.AsObject();
            if (options["configProperties"] is not JsonObject properties)
            {
                options["configProperties"] = properties = [];
            }

            properties[DynamicCodeSwitch] = false;
            File.WriteAllText(runtimeConfig, json.ToJsonString());
            return Run(copy.FullName);
        }
        finally
        {
            copy.Delete(recursive: true);
        }
    }

    private static Dictionary<string, TrxResult> Run(string output)
    {
        var results = Directory.CreateTempSubdirectory("vg-aspnet-trx-");
        try
        {
            var executable = Path.Combine(output, OperatingSystem.IsWindows() ? Executable + ".exe" : Executable);
            // The results directory is the current directory, so a test that passes reads appsettings.json
            // from the content root the bootstrap passes, not from the current directory.
            var run = Process(executable, results.FullName, "--report-trx", "--report-trx-filename", "fixture.trx", "--results-directory", results.FullName);
            Assert.True(run.ExitCode == 2, run.Output);

            XNamespace trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            return XDocument.Load(Path.Combine(results.FullName, "fixture.trx")).Descendants(trx + "UnitTestResult").ToDictionary(
                test => test.Attribute("testName")!.Value,
                test => new TrxResult(
                    test.Attribute("outcome")!.Value,
                    test.Descendants(trx + "Message").FirstOrDefault()?.Value ?? "",
                    test.Descendants(trx + "StackTrace").FirstOrDefault()?.Value ?? "",
                    test.Descendants(trx + "StdOut").FirstOrDefault()?.Value.Replace("\r\n", "\n") ?? ""));
        }
        finally
        {
            results.Delete(recursive: true);
        }
    }

    private static (int ExitCode, string Output) Process(string fileName, string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // An MTP application started by these specs would otherwise take them for its dotnet test host.
        foreach (var variable in start.Environment.Keys.Where(key => key.StartsWith("TESTINGPLATFORM_", StringComparison.Ordinal)).ToList())
        {
            start.Environment.Remove(variable);
        }

        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        using var process = System.Diagnostics.Process.Start(start)!;
        var standardError = process.StandardError.ReadToEndAsync();
        var standardOutput = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"{fileName} did not finish in 5 minutes.");
        }

        return (process.ExitCode, standardOutput + standardError.Result);
    }

    public static string FindRoot()
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

internal sealed record TrxResult(string Outcome, string Message, string StackTrace, string Output);
