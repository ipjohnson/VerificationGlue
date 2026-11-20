# Recursive mocks

After reading this page you can set up a member of a member, such as `calc.History.Last`, without creating the mock in between, and set how deep the generator goes.

## Set up a member of a member

An unconfigured member whose return type is an interface returns a mock of that interface. The mock is created on the first call and returned again on later calls:

<!-- snippet: mocks-recursive -->
<a id='snippet-mocks-recursive'></a>
```cs
var calc = Mock.Of<ICalculator>();

calc.History.Last.Returns(5);

Verify(calc.History.Last == 5);
Verify(ReferenceEquals(calc.History, calc.History));
```
<sup><a href='/samples/MocksGuide/RecursiveMocks.cs#L12-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-recursive' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A member with parameters keeps one mock for each set of arguments:

<!-- snippet: mocks-recursive-arguments -->
<a id='snippet-mocks-recursive-arguments'></a>
```cs
var first = calc.History.Find(1);

Verify(ReferenceEquals(first, calc.History.Find(1)));
Verify(!ReferenceEquals(first, calc.History.Find(2)));
```
<sup><a href='/samples/MocksGuide/RecursiveMocks.cs#L27-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-recursive-arguments' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Arguments decide the mock as they do for an exact match. Equal strings, and arrays with equal elements, get the same mock even when they are different objects. A setup on the member, such as `calc.History.Returns(history)`, replaces the recursive mock, even after the test has read it.

A member that returns `Task<T>` or `ValueTask<T>` returns a completed task with `default`, not a task holding a mock. An interface in a `System` namespace, such as `IServiceProvider`, gets no recursive mock. `Received()` and `DidNotReceive()` checks create no mocks.

## The depth

The generator writes the mocks that recursive members return, and stops a number of steps from each type that a test mocks. A member of a mock at that depth returns `default`, and [VGM0007](../diagnostics/VGM0007.md) reports it as information.

In this sample, `ICalculator` is the mocked type, `IHistory` is one step away and `IEntry` is two. At the default depth of 2, `IEntry.Next` returns null:

<!-- snippet: mocks-recursive-depth -->
<a id='snippet-mocks-recursive-depth'></a>
```cs
var entry = calc.History.Find(1);

Verify(entry is not null);
Verify(entry.Next is null);
```
<sup><a href='/samples/MocksGuide/RecursiveMocks.cs#L40-L45' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-recursive-depth' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Set `VerificationGlueMocksRecursionDepth` in the project to change the depth. The value 0 turns recursive mocks off, so every member that would return one returns null:

<!-- snippet: mocks-recursion-depth -->
<a id='snippet-mocks-recursion-depth'></a>
```csproj
<PropertyGroup>
  <VerificationGlueMocksRecursionDepth>2</VerificationGlueMocksRecursionDepth>
</PropertyGroup>
```
<sup><a href='/samples/MocksGuide/MocksGuide.csproj#L7-L11' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-recursion-depth' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The default comes from the shape sweep over the BCL and the 100 most downloaded NuGet packages. [`docs/design/mocks-shape-sweep.md`](../design/mocks-shape-sweep.md#recursion-depth) has the figures.

A type that a test names with `Mock.Of<T>()` starts its own walk from step 0. Naming `IEntry` in a test therefore gives `IEntry.Next` a mock too.
