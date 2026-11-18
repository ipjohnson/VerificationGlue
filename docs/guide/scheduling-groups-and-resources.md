# Scheduling groups and resources

At the end of this page you can choose how many tests run at once, keep tests that share something from overlapping, and make one set of tests wait for another.

## Tests run in parallel by group

Each test belongs to one schedule group. Tests of the same group run up to the group's limit at once, and different groups run side by side. A test that names no group is in the group `default`, whose limit is the processor count.

`[ScheduleGroup]` sets the group on a method, a class or the assembly. The generator reads the method's first, then the class's, then the assembly's, and the narrowest one wins. Its `MaxParallel` sets the limit, and 0, the default, means the processor count.

This line on the assembly states the default and can change it:

<!-- snippet: tests-default-group -->
<a id='snippet-tests-default-group'></a>
```cs
[assembly: ScheduleGroup("default", MaxParallel = 0)]
```
<sup><a href='/samples/TestsScheduling/SchedulingTests.cs#L4-L6' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-default-group' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`[assembly: ScheduleGroup("default", MaxParallel = 1)]` runs the tests without a group one at a time.

## Limit a group

These tests share a database that takes two connections at a time, so their group runs at most two of them at once:

<!-- snippet: tests-schedule-group -->
<a id='snippet-tests-schedule-group'></a>
```cs
[ScheduleGroup("database", MaxParallel = 2)]
public partial class OrderRepositoryTests
{
    [VerifyTest]
    public async Task Saves_an_order() => Verify(await OrderDatabase.QueryAsync() <= 2);

    [VerifyTest]
    public async Task Loads_an_order() => Verify(await OrderDatabase.QueryAsync() <= 2);

    [VerifyTest]
    public async Task Deletes_an_order() => Verify(await OrderDatabase.QueryAsync() <= 2);
}
```
<sup><a href='/samples/TestsScheduling/SchedulingTests.cs#L58-L71' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-schedule-group' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`OrderDatabase.QueryAsync` returns how many queries were running with it, so each test fails if a third one ran beside it.

When one group name is declared with different limits, the smallest one applies.

## Keep tests apart with a resource

`[Resource(key)]` on a method or a class gives each of its tests that key. Two tests that hold the same key never run at the same time, whichever groups they are in. A test can hold several keys, and the keys of its class and its method add up:

<!-- snippet: tests-resource -->
<a id='snippet-tests-resource'></a>
```cs
public partial class PaymentTests
{
    [VerifyTest]
    [Resource("payments-sandbox")]
    public async Task Charges_a_card() => Verify(await PaymentsSandbox.UseAloneAsync());

    [VerifyTest]
    [Resource("payments-sandbox")]
    public async Task Refunds_a_card() => Verify(await PaymentsSandbox.UseAloneAsync());
}
```
<sup><a href='/samples/TestsScheduling/SchedulingTests.cs#L73-L84' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-resource' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The scheduler holds a test back while another running test holds one of its keys, and starts other tests of the group in the meantime.

Use a resource when the tests that conflict are in different groups, or when only a few tests of a group conflict. Use a group with `MaxParallel = 1` when the tests of a class must run one at a time.

## Wait for another group

`[RunAfter(group)]` on a class makes the group of its tests start only after every test of the named group has finished:

<!-- snippet: tests-run-after -->
<a id='snippet-tests-run-after'></a>
```cs
[ScheduleGroup("reports")]
[RunAfter("database")]
public partial class DailyReportTests
{
    [VerifyTest]
    public void Starts_after_the_database_tests() => Verify(OrderDatabase.Open == 0);
}
```
<sup><a href='/samples/TestsScheduling/SchedulingTests.cs#L86-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-run-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`[RunAfter]` applies to the whole group, so another class in `reports` waits too. A class can carry more than one `[RunAfter]`. A group that is named but has no selected tests, such as one that `--filter-uid` left out, does not hold anything back.

When the groups that `[RunAfter]` names lead back to the group that waits, the build fails with [VGT0003](../diagnostics/VGT0003.md).

## Scopes under parallel runs

A class scope is created when the first test of the class starts, in whichever group that test runs. Tests of the same class that start at the same time share it, and it is disposed after the last of them ends. A container with `Scope = ContainerScope.Class` or `Scope = ContainerScope.Assembly` is built once and shared by those tests in the same way, so it must be safe to use from several tests at once. [Test lifecycle](test-lifecycle.md) shows the order of the steps.

Output that a class or assembly scope writes goes to the test whose code is running when it writes, such as the test that created the scope.

## Replace the rules

`[assembly: Scheduler<T>]` replaces these rules with an `ITestScheduler` of your own, for example to split the tests between machines. [Extending VerificationGlue.Tests](extending.md#schedulers) shows one.

`samples/TestsScheduling` holds the code on this page, and `test/VerificationGlue.Tests.Specs/SchedulingSpecs.cs` checks the limit, the resources and `[RunAfter]` against `test/VerificationGlue.Tests.Fixture`.

## Next

- [Test lifecycle](test-lifecycle.md)
- [Extending VerificationGlue.Tests](extending.md)
- [VGT0003](../diagnostics/VGT0003.md)
