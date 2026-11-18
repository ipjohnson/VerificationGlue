using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace VerificationGlue.Tests.Specs;

/// <summary>
/// test/VerificationGlue.Tests.Fixture, built once per run in the configuration of these specs.
/// It is not in the solution, because some of its tests fail on purpose and <c>dotnet test --solution</c>
/// would run it.
/// </summary>
internal static class Fixture
{
    private static readonly Lazy<string> Built = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string RepositoryRoot { get; } = FindRoot();

    public static string Configuration { get; } =
        typeof(Fixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "VerificationGlue.Configuration").Value!;

    public static string ProjectPath => Path.Combine(RepositoryRoot, "test", "VerificationGlue.Tests.Fixture", "VerificationGlue.Tests.Fixture.csproj");

    /// <summary>The directory that holds the built fixture.</summary>
    public static string OutputDirectory => Built.Value;

    public static string Executable => Path.Combine(OutputDirectory, OperatingSystem.IsWindows() ? "VerificationGlue.Tests.Fixture.exe" : "VerificationGlue.Tests.Fixture");

    /// <summary>Runs the fixture executable with <paramref name="arguments"/>.</summary>
    public static ProcessResult Run(params string[] arguments) => Process(Executable, OutputDirectory, new Dictionary<string, string>(), arguments);

    /// <summary>
    /// Runs the fixture executable with <paramref name="arguments"/> and <c>VG_FIXTURE_LOG</c> set to a
    /// new file, and returns the lines the fixture logged with the result.
    /// </summary>
    public static (ProcessResult Result, string[] Log) RunLogged(IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        var log = Path.Combine(Path.GetTempPath(), "vg-fixture-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            var variables = new Dictionary<string, string>(environment) { ["VG_FIXTURE_LOG"] = log };
            var result = Process(Executable, OutputDirectory, variables, arguments);
            return (result, File.Exists(log) ? File.ReadAllLines(log) : []);
        }
        finally
        {
            File.Delete(log);
        }
    }

    /// <summary>Runs <c>dotnet</c> with <paramref name="arguments"/> in <paramref name="directory"/>.</summary>
    public static ProcessResult Dotnet(string directory, params string[] arguments) =>
        Dotnet(new Dictionary<string, string>(), directory, arguments);

    /// <summary>Runs <c>dotnet</c> with <paramref name="arguments"/> and <paramref name="environment"/> in <paramref name="directory"/>.</summary>
    public static ProcessResult Dotnet(IReadOnlyDictionary<string, string> environment, string directory, params string[] arguments)
    {
        _ = Built.Value;
        return Process("dotnet", directory, environment, arguments);
    }

    private static string Build()
    {
        var build = Process("dotnet", RepositoryRoot, new Dictionary<string, string>(), "build", ProjectPath, "-c", Configuration);
        Assert.True(build.ExitCode == 0, "dotnet build of the fixture failed:\n" + build.Output);
        return Path.Combine(RepositoryRoot, "test", "VerificationGlue.Tests.Fixture", "bin", Configuration, "net10.0");
    }

    private static ProcessResult Process(string fileName, string directory, IReadOnlyDictionary<string, string> environment, params string[] arguments)
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
        start.Environment["NO_COLOR"] = "1";
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var output = new StringBuilder();
        process.OutputDataReceived += (_, line) => { if (line.Data is not null) { lock (output) { output.AppendLine(line.Data); } } };
        process.ErrorDataReceived += (_, line) => { if (line.Data is not null) { lock (output) { output.AppendLine(line.Data); } } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"{fileName} {string.Join(' ', arguments)} did not finish in 5 minutes. Output so far:\n{output}");
        }

        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output.ToString());
    }

    private static string FindRoot()
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

internal sealed record ProcessResult(int ExitCode, string Output);
