namespace Hardening.Lifecycle.Specs;

/// <summary>
/// Injects an exception or a cancellation at each lifecycle boundary of docs/design/hardening.md,
/// "Failure injection and ownership", for the test, class and assembly scopes (LIFE-013). Each case runs
/// test/Hardening.Lifecycle.Fixture once and checks its log and its TRX report.
/// </summary>
public class InjectionSpecs
{
    private const string TestScoped = "name:Lifecycle.TestScoped.*";
    private const string ContainerOwned = "name:Lifecycle.ContainerOwned.*";
    private const string ClassScoped = "name:Lifecycle.ClassScoped.*";
    private const string AssemblyScoped = "name:Lifecycle.AssemblyScoped*";
    private const string Assembly = "Hardening.Lifecycle.Fixture";
    private const string AssemblyNode = Assembly + "/Disposing the assembly scope " + Assembly;

    private static readonly string[] SetupBoundaries = ["scope-creation", "container", "middleware-entry", "class-construction", "parameter"];

    /// <summary>
    /// One fault at each boundary that applies to each scope level. A fault at a boundary that each test
    /// passes fires in the test named First only, so Second shows that the next test is not affected.
    /// </summary>
    internal static readonly InjectionCase[] Boundaries =
    [
        One("test", "scope-creation", TestScoped, Test("TestScoped", "First")),
        One("test", "container", TestScoped, Test("TestScoped", "First")),
        One("test", "middleware-entry", TestScoped, Test("TestScoped", "First")),
        One("test", "class-construction", TestScoped, Test("TestScoped", "First")),
        One("test", "class-construction", ContainerOwned, Test("ContainerOwned", "First")) with { Name = "test class-construction, container-owned instance" },
        One("test", "parameter", TestScoped, Test("TestScoped", "First")),
        One("test", "body", TestScoped, Test("TestScoped", "First")),
        One("test", "middleware-exit", TestScoped, Test("TestScoped", "First")),
        new(
            "test disposal",
            "test:disposal",
            TestScoped,
            ["test disposal (instance)", "test disposal (listener)", "test disposal (container)"],
            [(Test("TestScoped", "First"), ["test disposal (instance)", "test disposal (listener)", "test disposal (container)"])]),
        new(
            "test disposal, container-owned instance",
            "test:disposal",
            ContainerOwned,
            ["test disposal (listener)", "test disposal (instance)", "test disposal (container)"],
            [(Test("ContainerOwned", "First"), ["test disposal (listener)", "test disposal (instance)", "test disposal (container)"])]),

        One("class", "scope-creation", ClassScoped, Test("ClassScoped", "First"), Test("ClassScoped", "Second")),
        One("class", "container", ClassScoped, Test("ClassScoped", "First"), Test("ClassScoped", "Second")),
        One("class", "middleware-entry", ClassScoped, Test("ClassScoped", "First")),
        One("class", "parameter", ClassScoped, Test("ClassScoped", "First")),
        One("class", "middleware-exit", ClassScoped, Test("ClassScoped", "First")),
        new(
            "class disposal",
            "class:disposal",
            ClassScoped,
            ["class disposal (listener)", "class disposal (container)"],
            [(ClassNode("ClassScoped"), ["class disposal (listener)", "class disposal (container)"])]),

        One("assembly", "scope-creation", AssemblyScoped, Test("AssemblyScoped", "First"), Test("AssemblyScoped", "Second"), Test("AssemblyScopedOther", "Third")),
        One("assembly", "container", AssemblyScoped, Test("AssemblyScoped", "First"), Test("AssemblyScoped", "Second"), Test("AssemblyScopedOther", "Third")),
        One("assembly", "middleware-entry", AssemblyScoped, Test("AssemblyScoped", "First")),
        One("assembly", "parameter", AssemblyScoped, Test("AssemblyScoped", "First")),
        One("assembly", "middleware-exit", AssemblyScoped, Test("AssemblyScoped", "First")),
        new(
            "assembly disposal",
            "assembly:disposal",
            AssemblyScoped,
            ["assembly disposal (listener)", "assembly disposal (container)"],
            [(AssemblyNode, ["assembly disposal (listener)", "assembly disposal (container)"])]),
    ];

    /// <summary>Several faults in one run, which the failures of one test or scope must all report, in the order they happened.</summary>
    internal static readonly InjectionCase[] Combinations =
    [
        new(
            "body and several cleanup failures",
            "test:body;test:middleware-exit;test:disposal",
            TestScoped,
            ["test body", "test disposal (instance)", "test middleware-exit", "test disposal (listener)", "test disposal (container)"],
            [(Test("TestScoped", "First"), ["test body", "test disposal (instance)", "test middleware-exit", "test disposal (listener)", "test disposal (container)"])]),
        new(
            "middleware entry and cleanup failures",
            "test:middleware-entry;test:disposal",
            TestScoped,
            ["test middleware-entry", "test disposal (listener)", "test disposal (container)"],
            [(Test("TestScoped", "First"), ["test middleware-entry", "test disposal (listener)", "test disposal (container)"])]),
        new(
            "listener entry failure and class scope cleanup failures",
            "test:scope-creation;class:disposal",
            ClassScoped,
            ["test scope-creation", "class disposal (listener)", "class disposal (container)"],
            [
                (Test("ClassScoped", "First"), ["test scope-creation"]),
                (ClassNode("ClassScoped"), ["class disposal (listener)", "class disposal (container)"]),
            ]),
        new(
            "every scope fails to dispose",
            "test:disposal;class:disposal;assembly:disposal",
            AssemblyScoped + "|" + ClassScoped,
            [
                "test disposal (listener)", "test disposal (container)", "class disposal (listener)",
                "class disposal (listener)",
                "test disposal (listener)", "test disposal (container)", "class disposal (listener)", "class disposal (container)",
                "assembly disposal (listener)", "assembly disposal (container)",
            ],
            [
                (Test("AssemblyScoped", "First"), ["test disposal (listener)", "test disposal (container)"]),
                (ClassNode("AssemblyScoped"), ["class disposal (listener)"]),
                (ClassNode("AssemblyScopedOther"), ["class disposal (listener)"]),
                (Test("ClassScoped", "First"), ["test disposal (listener)", "test disposal (container)"]),
                (ClassNode("ClassScoped"), ["class disposal (listener)", "class disposal (container)"]),
                (AssemblyNode, ["assembly disposal (listener)", "assembly disposal (container)"]),
            ]),
    ];

    private static readonly Lazy<Dictionary<string, Task<FixtureRun>>> Runs = new(StartRuns);

    public static TheoryData<string> ExceptionCases => [.. Boundaries.Concat(Combinations).Select(@case => @case.Name)];

    public static TheoryData<string> CancellationCases => [.. Boundaries.Select(@case => @case.Name)];

    [Theory]
    [MemberData(nameof(ExceptionCases))]
    public async Task An_exception_is_reported_against_its_test_or_scope_and_every_resource_is_released(string name)
    {
        var @case = Boundaries.Concat(Combinations).Single(candidate => candidate.Name == name);
        var run = await Runs.Value["exception " + name];

        Assert.True(run.ExitCode == 2, run.ToString());
        Assert.Equal(@case.Fired, Fired(run.Log));
        AssertEachResourceReleasedOnce(run);

        foreach (var (node, places) in @case.Failed)
        {
            Assert.True(run.Results.TryGetValue(node, out var result), $"No result for {node}.\n{run}");
            Assert.True(result.Outcome == "Failed", $"{node} is {result.Outcome}.\n{run}");
            AssertInOrder(result.Message, places.Select(place => $"Injected at {place}."), run);
            AssertInOrder(@case.Fired, places, run);
            if (Body(node) is { } body)
            {
                var setupFailed = places.Any(place => SetupBoundaries.Contains(place.Split(' ')[1]));
                Assert.True(setupFailed != run.Log.Any(line => line.StartsWith(body, StringComparison.Ordinal)), $"{node}: setup failed is {setupFailed}, so its body must {(setupFailed ? "not " : "")}run.\n{run}");
            }
        }

        var failed = @case.Failed.Select(entry => entry.Node).ToHashSet();
        Assert.All(run.Results.Where(result => !failed.Contains(result.Key)), result =>
        {
            Assert.True(result.Value.Outcome == "Passed", $"{result.Key} is {result.Value.Outcome}.\n{run}");
            Assert.Contains(run.Log, line => line.StartsWith(Body(result.Key)!, StringComparison.Ordinal));
        });
    }

    [Theory]
    [MemberData(nameof(CancellationCases))]
    public async Task Cancellation_releases_every_resource_and_starts_no_more_work(string name)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fixture cancels the run with SIGINT, which Windows does not have.");
        var @case = Boundaries.Single(candidate => candidate.Name == name);
        var run = await Runs.Value["cancellation " + name];

        Assert.True(run.ExitCode == 3, run.ToString());
        Assert.Equal(@case.Fired, Fired(run.Log));
        AssertEachResourceReleasedOnce(run);
        var cancelled = Array.FindIndex(run.Log, line => line.StartsWith("inject ", StringComparison.Ordinal));
        Assert.All(run.Log.Skip(cancelled), line =>
            Assert.False(line.StartsWith("acquire ", StringComparison.Ordinal) || line.StartsWith("body ", StringComparison.Ordinal), $"'{line}' came after the run was cancelled.\n{run}"));
    }

    /// <summary>Starts every run at once, as many at a time as there are processors, so the cases share the wait.</summary>
    private static Dictionary<string, Task<FixtureRun>> StartRuns()
    {
        var gate = new SemaphoreSlim(Environment.ProcessorCount);
        var runs = new Dictionary<string, Task<FixtureRun>>();
        foreach (var @case in Boundaries.Concat(Combinations))
        {
            runs.Add("exception " + @case.Name, Start(@case.Inject.Replace(";", ":exception;") + ":exception", @case.Filter));
        }

        if (!OperatingSystem.IsWindows())
        {
            foreach (var @case in Boundaries)
            {
                runs.Add("cancellation " + @case.Name, Start(@case.Inject.Replace(";", ":cancellation;") + ":cancellation", @case.Filter));
            }
        }

        return runs;

        Task<FixtureRun> Start(string inject, string filter) => Task.Run(async () =>
        {
            await gate.WaitAsync();
            try
            {
                return LifecycleFixture.Run(inject, filter);
            }
            finally
            {
                gate.Release();
            }
        });
    }

    /// <summary>The places where a fault fired, in the order they fired.</summary>
    private static string[] Fired(string[] log) =>
        [.. log.Where(line => line.StartsWith("inject ", StringComparison.Ordinal)).Select(line => line[(line.IndexOf(" at ", StringComparison.Ordinal) + 4)..])];

    /// <summary>Asserts that each resource the log acquires is released exactly once, after it is acquired.</summary>
    private static void AssertEachResourceReleasedOnce(FixtureRun run)
    {
        var held = new HashSet<string>();
        var released = new HashSet<string>();
        foreach (var line in run.Log)
        {
            if (line.StartsWith("acquire ", StringComparison.Ordinal))
            {
                var resource = line["acquire ".Length..];
                Assert.True(!released.Contains(resource) && held.Add(resource), $"'{resource}' was acquired twice.\n{run}");
            }
            else if (line.StartsWith("release ", StringComparison.Ordinal))
            {
                var resource = line["release ".Length..];
                Assert.True(held.Remove(resource) && released.Add(resource), $"'{resource}' was released without being held.\n{run}");
            }
        }

        Assert.True(held.Count == 0, $"Never released: {string.Join(", ", held)}.\n{run}");
    }

    /// <summary>Asserts that <paramref name="expected"/> appear in <paramref name="text"/> in this order.</summary>
    private static void AssertInOrder(string text, IEnumerable<string> expected, FixtureRun run)
    {
        var position = 0;
        foreach (var part in expected)
        {
            var found = text.IndexOf(part, position, StringComparison.Ordinal);
            Assert.True(found >= 0, $"'{part}' is not in the message after position {position}:\n{text}\n\n{run}");
            position = found + part.Length;
        }
    }

    /// <summary>Asserts that <paramref name="expected"/> is a subsequence of <paramref name="sequence"/>.</summary>
    private static void AssertInOrder(IReadOnlyList<string> sequence, IEnumerable<string> expected, FixtureRun run)
    {
        var position = 0;
        foreach (var item in expected)
        {
            while (position < sequence.Count && sequence[position] != item)
            {
                position++;
            }

            Assert.True(position < sequence.Count, $"'{item}' is reported out of the order the faults fired in.\n{run}");
            position++;
        }
    }

    private static InjectionCase One(string level, string boundary, string filter, params string[] failed) =>
        new($"{level} {boundary}", $"{level}:{boundary}", filter, [$"{level} {boundary}"], [.. failed.Select(node => (node, new[] { $"{level} {boundary}" }))]);

    private static string Test(string type, string method) => $"Lifecycle.{type}/{method}";

    private static string ClassNode(string type) => $"Lifecycle.{type}/Disposing the class scope Lifecycle.{type}";

    /// <summary>The start of the log line a test's body writes, or null for a scope node.</summary>
    private static string? Body(string node) =>
        node.Contains("/Disposing the ", StringComparison.Ordinal) ? null : "body " + node.Replace('/', '.') + "(";
}

/// <param name="Name">The case's name.</param>
/// <param name="Inject">The faults, as <c>level:boundary</c> separated by <c>;</c>. The run adds the fault kind.</param>
/// <param name="Filter">The <c>--vg-filter</c> that selects the tests.</param>
/// <param name="Fired">Where each fault fires, in the order it fires.</param>
/// <param name="Failed">The nodes that fail and the faults each reports, in the order of its message. Every other node passes.</param>
internal sealed record InjectionCase(string Name, string Inject, string Filter, string[] Fired, (string Node, string[] Places)[] Failed);
