using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>
/// Puts tests in a schedule group. The generator reads the method's attribute, then the class's, then
/// the assembly's. A test with none is in the group <see cref="DefaultName"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ScheduleGroupAttribute(string name) : Attribute
{
    /// <summary>The name of the group of tests that name no group.</summary>
    public const string DefaultName = "default";

    /// <summary>The group's name.</summary>
    public string Name { get; } = name;

    /// <summary>
    /// How many of the group's tests run at once. 0, the default, means the processor count. When one
    /// group is declared with different limits, the smallest applies.
    /// </summary>
    public int MaxParallel { get; set; }
}

/// <summary>Tests that hold the same key never run at the same time, in any group.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ResourceAttribute(string key) : Attribute
{
    /// <summary>The resource's key.</summary>
    public string Key { get; } = key;
}

/// <summary>
/// The schedule group of this class's tests starts after every test of <see cref="Group"/> has finished.
/// A cycle of these is build error VGT0003.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RunAfterAttribute(string group) : Attribute
{
    /// <summary>The group to wait for.</summary>
    public string Group { get; } = group;
}

/// <summary>The base of <see cref="SchedulerAttribute{T}"/>.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public abstract class SchedulerAttribute : Attribute
{
    private protected SchedulerAttribute()
    {
    }

    internal abstract ITestScheduler Create();
}

/// <summary>Runs the assembly's tests with <typeparamref name="T"/> instead of <see cref="DefaultTestScheduler"/>.</summary>
/// <typeparam name="T">The scheduler.</typeparam>
public sealed class SchedulerAttribute<T> : SchedulerAttribute
    where T : ITestScheduler, new()
{
    internal override ITestScheduler Create() => new T();
}

/// <summary>Decides when each selected test runs.</summary>
public interface ITestScheduler
{
    /// <summary>
    /// Runs <paramref name="tests"/> by calling <paramref name="run"/> at most once for each. A test the
    /// scheduler does not run is reported as skipped. Return after every call has finished.
    /// </summary>
    /// <param name="tests">The selected tests, ordered by UID.</param>
    /// <param name="run">Runs one test through its scopes and reports its result. It does not throw for a failed test.</param>
    /// <param name="cancellationToken">Cancelled when the run is cancelled. Start no more tests after it is.</param>
    ValueTask RunAsync(IReadOnlyList<ScheduledTest> tests, Func<ScheduledTest, ValueTask> run, CancellationToken cancellationToken);
}

/// <summary>A selected test, with what a scheduler needs to know about it.</summary>
public sealed class ScheduledTest
{
    internal ScheduledTest(TestDescriptor test, int maxParallel, IReadOnlyList<string> runAfter)
    {
        Test = test;
        MaxParallel = maxParallel;
        RunAfter = runAfter;
    }

    /// <summary>The test's UID.</summary>
    public string Uid => Test.Uid;

    /// <summary>The name a test explorer shows.</summary>
    public string DisplayName => Test.DisplayName;

    /// <summary>The type that declares the test method.</summary>
    public Type TestClass => Test.TestClass;

    /// <summary>The name of the test's schedule group.</summary>
    public string Group => Test.ScheduleGroup;

    /// <summary>How many tests of <see cref="Group"/> run at once. At least 1, and the same for every test of the group.</summary>
    public int MaxParallel { get; }

    /// <summary>The groups that must finish before <see cref="Group"/> starts. The same for every test of the group.</summary>
    public IReadOnlyList<string> RunAfter { get; }

    /// <summary>The <c>[Resource]</c> keys the test holds while it runs.</summary>
    public IReadOnlyList<string> Resources => Test.Resources;

    internal TestDescriptor Test { get; }

    /// <summary>
    /// The tests as a scheduler sees them. Each group takes the smallest limit its tests declare, with 0
    /// meaning the processor count, and the union of the groups its classes run after.
    /// </summary>
    internal static IReadOnlyList<ScheduledTest> From(IReadOnlyList<TestDescriptor> tests)
    {
        var limits = new Dictionary<string, int>(StringComparer.Ordinal);
        var runAfter = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var test in tests)
        {
            var limit = test.MaxParallel > 0 ? test.MaxParallel : Environment.ProcessorCount;
            limits[test.ScheduleGroup] = limits.TryGetValue(test.ScheduleGroup, out var current) ? Math.Min(current, limit) : limit;
            if (!runAfter.TryGetValue(test.ScheduleGroup, out var groups))
            {
                groups = [];
                runAfter.Add(test.ScheduleGroup, groups);
            }

            foreach (var group in test.RunAfter)
            {
                if (!groups.Contains(group, StringComparer.Ordinal))
                {
                    groups.Add(group);
                }
            }
        }

        return tests.Select(test => new ScheduledTest(test, limits[test.ScheduleGroup], runAfter[test.ScheduleGroup])).ToArray();
    }
}

/// <summary>
/// The scheduler a test assembly uses unless <see cref="SchedulerAttribute{T}"/> names another. It runs
/// each group's tests in the given order, at most <see cref="ScheduledTest.MaxParallel"/> at once, with the
/// groups side by side. It holds a test while another running test holds one of its resource keys, and it
/// starts a group only after the groups in its <see cref="ScheduledTest.RunAfter"/> have finished. A group
/// that waits on a group with no selected tests does not wait.
/// </summary>
public sealed class DefaultTestScheduler : ITestScheduler
{
    /// <inheritdoc/>
    public async ValueTask RunAsync(IReadOnlyList<ScheduledTest> tests, Func<ScheduledTest, ValueTask> run, CancellationToken cancellationToken)
    {
        var groups = new List<Group>();
        var byName = new Dictionary<string, Group>(StringComparer.Ordinal);
        foreach (var test in tests)
        {
            if (!byName.TryGetValue(test.Group, out var group))
            {
                group = new Group(test.Group, Math.Max(1, test.MaxParallel), test.RunAfter);
                byName.Add(test.Group, group);
                groups.Add(group);
            }

            group.Pending.Add(test);
            group.Unfinished++;
        }

        foreach (var group in groups)
        {
            group.WaitsOn.AddRange(group.RunAfterNames.Where(name => name != group.Name && byName.ContainsKey(name)).Select(name => byName[name]));
        }

        ThrowOnCycle(groups);

        var gate = new object();
        var finished = new SemaphoreSlim(0);
        var held = new HashSet<string>(StringComparer.Ordinal);
        var failures = new List<Exception>();
        var running = 0;
        while (true)
        {
            lock (gate)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    foreach (var group in groups)
                    {
                        if (group.WaitsOn.Any(other => other.Unfinished > 0))
                        {
                            continue;
                        }

                        for (var i = 0; i < group.Pending.Count && group.Running < group.Limit;)
                        {
                            var test = group.Pending[i];
                            if (test.Resources.Any(held.Contains))
                            {
                                i++;
                                continue;
                            }

                            group.Pending.RemoveAt(i);
                            group.Running++;
                            running++;
                            held.UnionWith(test.Resources);
                            _ = Task.Run(() => RunOneAsync(test, group));
                        }
                    }
                }

                if (running == 0)
                {
                    break;
                }
            }

            await finished.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        if (failures.Count > 0)
        {
            throw failures.Count == 1 ? failures[0] : new AggregateException(failures);
        }

        async Task RunOneAsync(ScheduledTest test, Group group)
        {
            try
            {
                await run(test).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                lock (gate)
                {
                    failures.Add(failure);
                }
            }
            finally
            {
                lock (gate)
                {
                    group.Running--;
                    group.Unfinished--;
                    running--;
                    held.ExceptWith(test.Resources);
                }

                finished.Release();
            }
        }
    }

    /// <summary>Throws when groups wait on each other, which VGT0003 reports at build time.</summary>
    private static void ThrowOnCycle(List<Group> groups)
    {
        var ordered = new HashSet<Group>();
        var progress = true;
        while (progress)
        {
            progress = false;
            foreach (var group in groups)
            {
                if (!ordered.Contains(group) && group.WaitsOn.All(ordered.Contains))
                {
                    ordered.Add(group);
                    progress = true;
                }
            }
        }

        if (ordered.Count < groups.Count)
        {
            var names = groups.Where(group => !ordered.Contains(group)).Select(group => group.Name);
            throw new InvalidOperationException(
                $"VGT0003: The schedule groups {string.Join(", ", names)} wait on each other through [RunAfter], so they cannot start.");
        }
    }

    private sealed class Group(string name, int limit, IReadOnlyList<string> runAfter)
    {
        public string Name { get; } = name;

        public int Limit { get; } = limit;

        public IReadOnlyList<string> RunAfterNames { get; } = runAfter;

        public List<Group> WaitsOn { get; } = [];

        public List<ScheduledTest> Pending { get; } = [];

        public int Running { get; set; }

        public int Unfinished { get; set; }
    }
}
