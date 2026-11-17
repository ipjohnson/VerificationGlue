# Mock async members

After reading this page you can set up members that return `Task`, `Task<T>`, `ValueTask` and `ValueTask<T>`, return a faulted task, and await inside a setup.

## Return a value

`Returns` on a `Task<T>` or `ValueTask<T>` member takes a `T` and wraps it in a completed task:

<!-- snippet: mocks-async-returns -->
<a id='snippet-mocks-async-returns'></a>
```cs
calc.ComputeAsync(5).Returns(25);
calc.NameAsync().Returns("casio");

Verify(await calc.ComputeAsync(5) == 25);
Verify(await calc.NameAsync() == "casio");
```
<sup><a href='/samples/MocksGuide/AsyncMembers.cs#L14-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-async-returns' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The typed surface does the same:

<!-- snippet: mocks-async-typed -->
<a id='snippet-mocks-async-typed'></a>
```cs
calc.Setup().ComputeAsync(5).Returns(25);

Verify(await calc.ComputeAsync(5) == 25);
```
<sup><a href='/samples/MocksGuide/AsyncMembers.cs#L28-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-async-typed' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Returns` also takes a task, such as `Task.FromResult(25)`, when the test needs to control it.

## Unconfigured members

An unconfigured async member returns a completed task. A `Task<T>` or `ValueTask<T>` holds the auto-value of `T`, which is an empty string for `string`:

<!-- snippet: mocks-async-auto-values -->
<a id='snippet-mocks-async-auto-values'></a>
```cs
var calc = Mock.Of<ICalculator>();

Verify(calc.SaveAsync().IsCompletedSuccessfully);
Verify(await calc.ComputeAsync(1) == 0);
Verify(await calc.NameAsync() == "");
```
<sup><a href='/samples/MocksGuide/AsyncMembers.cs#L38-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-async-auto-values' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Faults

Return a faulted task to fail the `await`. `Throws` makes the call itself throw, before there is a task to await:

<!-- snippet: mocks-async-faulted -->
<a id='snippet-mocks-async-faulted'></a>
```cs
calc.ComputeAsync(0).Returns(Task.FromException<int>(new TimeoutException()));
calc.ComputeAsync(-1).Throws(new ArgumentOutOfRangeException());

await ThrowsAsync<TimeoutException>(() => calc.ComputeAsync(0));
Throws<ArgumentOutOfRangeException>(() => { _ = calc.ComputeAsync(-1); });
```
<sup><a href='/samples/MocksGuide/AsyncMembers.cs#L52-L58' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-async-faulted' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Await inside a setup

The link between a mock call and its `Returns` is kept in an `AsyncLocal`, so it survives an `await` in the argument:

<!-- snippet: mocks-async-await-in-setup -->
<a id='snippet-mocks-async-await-in-setup'></a>
```cs
calc.Add(1, 2).Returns(await LoadExpectedSumAsync());

Verify(calc.Add(1, 2) == 3);
```
<sup><a href='/samples/MocksGuide/AsyncMembers.cs#L66-L70' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-async-await-in-setup' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

NSubstitute keeps that link per thread. When the `await` resumes on another thread, its setup fails. [Known differences from NSubstitute](nsubstitute-differences.md#async-results) shows the case.
