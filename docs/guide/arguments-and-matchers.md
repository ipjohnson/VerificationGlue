# Arguments and matchers

After reading this page you can match any value or a value that passes a test, set `out` arguments, read the arguments of a call, and use matchers in checks.

## Any and Is

`Arg.Any<T>()` matches every value of `T`, including null. `Arg.Is<T>(predicate)` matches a value for which the predicate returns true, and `Arg.Is(value)` matches an equal value. Any other argument must be equal:

<!-- snippet: mocks-matchers-any-is -->
<a id='snippet-mocks-matchers-any-is'></a>
```cs
calc.Add(Arg.Any<int>(), Arg.Is<int>(x => x > 10)).Returns(-1);
calc.Add(Arg.Is(7), 7).Returns(49);

Verify(calc.Add(3, 11) == -1);
Verify(calc.Add(3, 10) == 0);
Verify(calc.Add(7, 7) == 49);
```
<sup><a href='/samples/MocksGuide/Matchers.cs#L14-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-matchers-any-is' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An array argument matches an array with equal elements.

## A matcher belongs to the argument it is written at

The generator reads the position of each `Arg` call from the source, so the matcher below applies to the second argument:

<!-- snippet: mocks-matchers-position -->
<a id='snippet-mocks-matchers-position'></a>
```cs
calc.Add(0, Arg.Any<int>()).Returns(5);

Verify(calc.Add(0, 9) == 5);
Verify(calc.Add(9, 0) == 0);
```
<sup><a href='/samples/MocksGuide/Matchers.cs#L29-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-matchers-position' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

NSubstitute cannot tell which `0` the matcher stands for, and throws `AmbiguousArgumentsException`. [Known differences from NSubstitute](nsubstitute-differences.md#argument-matching) has the cases.

A matcher stored in a local has no position. [VGM0002](../diagnostics/VGM0002.md) reports it, and at run time it falls back to NSubstitute's rule: it takes the first argument that holds the default value.

## Typed matchers

The typed surface takes `Arg<T>` for each parameter. Its matchers are `Arg<T>.Any()`, `Arg<T>.Is(predicate)` and `Arg<T>.Is(value)`:

<!-- snippet: mocks-matchers-typed -->
<a id='snippet-mocks-matchers-typed'></a>
```cs
calc.Setup().Add(Arg<int>.Any(), Arg<int>.Is(x => x > 10)).Returns(-1);
calc.Setup().Since(Arg<IClock>.Is(clock)).Returns(TimeSpan.FromMinutes(5));

Verify(calc.Add(3, 11) == -1);
Verify(calc.Since(clock) == TimeSpan.FromMinutes(5));
```
<sup><a href='/samples/MocksGuide/Matchers.cs#L43-L49' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-matchers-typed' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A plain value converts to `Arg<T>` as an exact match. C# applies no conversion from an interface type, so the `IClock` parameter needs `Arg<IClock>.Is(clock)`. Passing `Arg.Any<int>()` to a typed member is an error, [VGM0002](../diagnostics/VGM0002.md), because it would match only `0`.

## Out and ref arguments

`Arg.Any<T>()` can stand in for an `out` or `ref` argument. Set the value in a `Returns` factory through the argument's index:

<!-- snippet: mocks-matchers-out -->
<a id='snippet-mocks-matchers-out'></a>
```cs
calc.TryParse("7", out Arg.Any<int>()).Returns(call =>
{
    call[1] = 7;
    return true;
});

var parsed = calc.TryParse("7", out var value);

Verify(parsed);
Verify(value == 7);
```
<sup><a href='/samples/MocksGuide/Matchers.cs#L57-L68' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-matchers-out' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Read the arguments

A `Returns` factory receives a `CallInfo`. `ArgAt<T>(index)` reads an argument, and the indexer reads or sets one:

<!-- snippet: mocks-matchers-call-info -->
<a id='snippet-mocks-matchers-call-info'></a>
```cs
calc.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(call => call.ArgAt<int>(0) * call.ArgAt<int>(1));

Verify(calc.Add(3, 4) == 12);
```
<sup><a href='/samples/MocksGuide/Matchers.cs#L76-L80' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-matchers-call-info' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`When(...).Do(...)` and the typed `Does(...)` receive the same `CallInfo`.

## Matchers in checks

`Received`, `DidNotReceive` and `Calls()` take the same matchers:

<!-- snippet: mocks-matchers-received -->
<a id='snippet-mocks-matchers-received'></a>
```cs
calc.Received().Add(Arg.Is<int>(x => x > 0), 2);
Verify(calc.Calls().Add(Arg<int>.Is(x => x > 0), 2) == 1);
```
<sup><a href='/samples/MocksGuide/Matchers.cs#L89-L92' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-matchers-received' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
