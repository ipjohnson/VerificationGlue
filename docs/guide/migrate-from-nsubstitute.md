# Migrate from NSubstitute

This guide shows the VerificationGlue.Mocks form of each common NSubstitute call. After reading it you can move a test project from NSubstitute to VerificationGlue.Mocks, and run it under Native AOT.

Each pair comes from two test projects that CI builds and runs: [`samples/MigrateFromNSubstitute/Before`](../../samples/MigrateFromNSubstitute/Before/InvoiceTests.cs), which uses NSubstitute, and [`samples/MigrateFromNSubstitute/After`](../../samples/MigrateFromNSubstitute/After/InvoiceTests.cs), which uses VerificationGlue.Mocks. The tests and their assertions do not change.

## Set up

Replace the `NSubstitute` package with `VerificationGlue.Mocks`, as [getting started](getting-started-mocks.md#install) shows. Then replace the `using` directives:

<!-- snippet: nsubstitute-usings-before -->
<a id='snippet-nsubstitute-usings-before'></a>
```cs
using NSubstitute;
using NSubstitute.ExceptionExtensions;
```
<sup><a href='/samples/MigrateFromNSubstitute/Before/InvoiceTests.cs#L1-L4' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-usings-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: nsubstitute-usings-after -->
<a id='snippet-nsubstitute-usings-after'></a>
```cs
using VerificationGlue.Mocks;
```
<sup><a href='/samples/MigrateFromNSubstitute/After/InvoiceTests.cs#L1-L3' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-usings-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Throws` is in the `VerificationGlue.Mocks` namespace, so `NSubstitute.ExceptionExtensions` has no counterpart.

## Create a mock

`Substitute.For<T>()` becomes `Mock.Of<T>()`:

<!-- snippet: nsubstitute-create-before -->
<a id='snippet-nsubstitute-create-before'></a>
```cs
var prices = Substitute.For<IPriceList>();
var receipts = Substitute.For<IReceipts>();
```
<sup><a href='/samples/MigrateFromNSubstitute/Before/InvoiceTests.cs#L15-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-create-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: nsubstitute-create-after -->
<a id='snippet-nsubstitute-create-after'></a>
```cs
var prices = Mock.Of<IPriceList>();
var receipts = Mock.Of<IReceipts>();
```
<sup><a href='/samples/MigrateFromNSubstitute/After/InvoiceTests.cs#L14-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-create-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Mock.Of<T>()` takes an interface. Class mocks come in a later phase, and the generator reports [VGM0001](../diagnostics/VGM0001.md) for a class today. Inside a generic helper, name the type in `[assembly: GenerateMock(typeof(T))]`. [Mock a type from a generic helper](mock-a-type-from-a-generic-helper.md) shows how.

## Returns and matchers

`Returns`, `Arg.Any` and `Arg.Is` keep their form:

<!-- snippet: nsubstitute-returns-before -->
<a id='snippet-nsubstitute-returns-before'></a>
```cs
prices.PriceOf("apple").Returns(3);
prices.PriceOf(Arg.Is<string>(sku => sku.StartsWith('p'))).Returns(4);
```
<sup><a href='/samples/MigrateFromNSubstitute/Before/InvoiceTests.cs#L20-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-returns-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: nsubstitute-returns-after -->
<a id='snippet-nsubstitute-returns-after'></a>
```cs
prices.PriceOf("apple").Returns(3);
prices.PriceOf(Arg.Is<string>(sku => sku.StartsWith('p'))).Returns(4);
```
<sup><a href='/samples/MigrateFromNSubstitute/After/InvoiceTests.cs#L19-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-returns-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A matcher applies to the argument it is written at. NSubstitute gives it to the first argument that holds the default value instead, so a setup such as `calc.Add(0, Arg.Any<int>())` behaves differently. [Arguments and matchers](arguments-and-matchers.md#a-matcher-belongs-to-the-argument-it-is-written-at) explains it.

## Received and DidNotReceive

<!-- snippet: nsubstitute-received-before -->
<a id='snippet-nsubstitute-received-before'></a>
```cs
receipts.Received(1).Send("ana", 7);
receipts.DidNotReceive().Send("bob", Arg.Any<int>());
```
<sup><a href='/samples/MigrateFromNSubstitute/Before/InvoiceTests.cs#L28-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-received-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: nsubstitute-received-after -->
<a id='snippet-nsubstitute-received-after'></a>
```cs
receipts.Received(1).Send("ana", 7);
receipts.DidNotReceive().Send("bob", Arg.Any<int>());
```
<sup><a href='/samples/MigrateFromNSubstitute/After/InvoiceTests.cs#L27-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-received-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Async members

<!-- snippet: nsubstitute-async-before -->
<a id='snippet-nsubstitute-async-before'></a>
```cs
prices.PriceOfAsync("apple").Returns(3);
prices.PriceOfAsync("pear").Returns(Task.FromResult(4));
```
<sup><a href='/samples/MigrateFromNSubstitute/Before/InvoiceTests.cs#L40-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-async-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: nsubstitute-async-after -->
<a id='snippet-nsubstitute-async-after'></a>
```cs
prices.PriceOfAsync("apple").Returns(3);
prices.PriceOfAsync("pear").Returns(Task.FromResult(4));
```
<sup><a href='/samples/MigrateFromNSubstitute/After/InvoiceTests.cs#L39-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-async-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A setup whose value comes from an `await`, such as `calc.Add(1, 2).Returns(await LoadAsync())`, works when the `await` resumes on another thread. Under NSubstitute it fails. [Mock async members](mock-async-members.md) has more.

## Throws

<!-- snippet: nsubstitute-throws-before -->
<a id='snippet-nsubstitute-throws-before'></a>
```cs
prices.PriceOf("plum").Throws(new KeyNotFoundException());
```
<sup><a href='/samples/MigrateFromNSubstitute/Before/InvoiceTests.cs#L54-L56' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-throws-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: nsubstitute-throws-after -->
<a id='snippet-nsubstitute-throws-after'></a>
```cs
prices.PriceOf("plum").Throws(new KeyNotFoundException());
```
<sup><a href='/samples/MigrateFromNSubstitute/After/InvoiceTests.cs#L53-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-throws-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## When and Do

<!-- snippet: nsubstitute-when-do-before -->
<a id='snippet-nsubstitute-when-do-before'></a>
```cs
var sent = new List<int>();
receipts.When(r => r.Send("ana", Arg.Any<int>())).Do(call => sent.Add(call.ArgAt<int>(1)));
```
<sup><a href='/samples/MigrateFromNSubstitute/Before/InvoiceTests.cs#L69-L72' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-when-do-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: nsubstitute-when-do-after -->
<a id='snippet-nsubstitute-when-do-after'></a>
```cs
var sent = new List<int>();
receipts.When(r => r.Send("ana", Arg.Any<int>())).Do(call => sent.Add(call.ArgAt<int>(1)));
```
<sup><a href='/samples/MigrateFromNSubstitute/After/InvoiceTests.cs#L68-L71' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-when-do-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## The typed surface

After the move, new tests can use the generated `Setup()` and `Calls()` members, which the compiler checks:

<!-- snippet: nsubstitute-typed-after -->
<a id='snippet-nsubstitute-typed-after'></a>
```cs
var prices = Mock.Of<IPriceList>();
var receipts = Mock.Of<IReceipts>();
prices.Setup().PriceOf(Arg<string>.Any()).Returns(2);

new Invoice(prices, receipts).Bill("ana", "apple", "pear");

Assert.True(receipts.Calls().Send("ana", 4) == 1);
```
<sup><a href='/samples/MigrateFromNSubstitute/After/InvoiceTests.cs#L81-L89' title='Snippet source file'>snippet source</a> | <a href='#snippet-nsubstitute-typed-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

[Choose a setup style](choose-a-setup-style.md) compares the two styles.

## Build-time checks

The generator reports two mistakes that NSubstitute finds only at run time, or not at all:

- [VGM0005](../diagnostics/VGM0005.md): `Returns` or `Throws` on a value that is not a mock call, such as `"abc".Length.Returns(3)`. Under NSubstitute that line rebinds the mock call before it without an error.
- [VGM0002](../diagnostics/VGM0002.md): an `Arg` matcher that is not an argument of a mock call, such as one stored in a local.

## Not supported yet

These NSubstitute features have no counterpart in the first release:

- Class mocks, `ReturnsForAnyArgs`, `Received.InOrder` and partial mocks with `ForPartsOf`.
- Several `Returns` factories in one call, and `ThrowsAsync`. Return a faulted task with `Returns(Task.FromException<T>(...))` instead.
- `Arg.Do` and `Arg.Invoke`. Use `When(...).Do(...)` and read the arguments from `CallInfo`.
- Auto-values of empty arrays and collections. An unconfigured member returns `default` for them.

## Behaviour that differs

[Known differences from NSubstitute](nsubstitute-differences.md) lists every case in the comparison suite where the two libraries behave differently, and why.
