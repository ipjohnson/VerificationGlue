# Migrate from xunit Assert

This guide shows the `Verify` form of each common xunit `Assert` call. After reading it you can move a test class from `Assert` to `Verify`, `Check` and `Throws`, and keep it running on xunit.

Each pair comes from two test projects that CI builds and runs: [`samples/MigrateFromXunitAssert/Before`](../../samples/MigrateFromXunitAssert/Before), which uses only xunit, and [`samples/MigrateFromXunitAssert/After`](../../samples/MigrateFromXunitAssert/After), which uses VerificationGlue.

## Set up

Add the package and import the methods in each test file:

```csharp
using VerificationGlue;
using static VerificationGlue.VerifySyntax;
```

The tests stay xunit tests. `[Fact]` and `[Theory]` do not change. The [getting started page](getting-started-verify.md#xunit) has the project file.

## The rule

`Assert` has a method per kind of comparison. `Verify` takes one `bool` condition written in C#. The generator reads the condition and writes the message, so `Verify(order.Total == 40)` fails with `expected order.Total to equal 40` and the file and line. The [messages page](messages.md) shows each message.

## Equality

<!-- snippet: xunit-assert-equal-before -->
<a id='snippet-xunit-assert-equal-before'></a>
```cs
Assert.Equal(40, _order.Total);
Assert.NotEqual(0, _order.Id);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L13-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-equal-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-equal-after -->
<a id='snippet-xunit-assert-equal-after'></a>
```cs
Verify(_order.Total == 40);
Verify(_order.Id != 0);
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L15-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-equal-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Assert.Equal(expected, actual)` takes the expected value first. In `Verify` the order does not matter to the message, which quotes the condition as written.

## Booleans

<!-- snippet: xunit-assert-bool-before -->
<a id='snippet-xunit-assert-bool-before'></a>
```cs
Assert.True(_order.IsPaid);
Assert.False(_order.Lines.Count == 0);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L22-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-bool-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-bool-after -->
<a id='snippet-xunit-assert-bool-after'></a>
```cs
Verify(_order.IsPaid);
Verify(_order.Lines.Count != 0);
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L24-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-bool-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Null

<!-- snippet: xunit-assert-null-before -->
<a id='snippet-xunit-assert-null-before'></a>
```cs
Assert.Null(_order.Coupon);
Assert.NotNull(_order.Shipping);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L31-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-null-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-null-after -->
<a id='snippet-xunit-assert-null-after'></a>
```cs
Verify(_order.Coupon is null);
Verify(_order.Shipping is not null);
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L33-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-null-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Collections

<!-- snippet: xunit-assert-collections-before -->
<a id='snippet-xunit-assert-collections-before'></a>
```cs
Assert.Contains(10, _order.Lines);
Assert.DoesNotContain(99, _order.Lines);
Assert.Empty(cart.Items);
Assert.Equal(2, _order.Lines.Count);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L42-L47' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-collections-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-collections-after -->
<a id='snippet-xunit-assert-collections-after'></a>
```cs
Verify(_order.Lines.Contains(10));
Verify(!_order.Lines.Contains(99));
Verify(cart.Items.Count == 0);
Verify(_order.Lines.Count == 2);
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L44-L49' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-collections-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Assert.Single(items)` returns the element. With `Verify`, check `items.Count == 1` and then read `items[0]`.

## Strings

<!-- snippet: xunit-assert-strings-before -->
<a id='snippet-xunit-assert-strings-before'></a>
```cs
Assert.StartsWith("exp", _order.Shipping);
Assert.Contains("press", _order.Shipping);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L53-L56' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-strings-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-strings-after -->
<a id='snippet-xunit-assert-strings-after'></a>
```cs
Verify(_order.Shipping.StartsWith("exp"));
Verify(_order.Shipping.Contains("press"));
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L55-L58' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-strings-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Types

<!-- snippet: xunit-assert-types-before -->
<a id='snippet-xunit-assert-types-before'></a>
```cs
Assert.IsAssignableFrom<Order>(order);
Assert.IsType<PaidOrder>(order);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L64-L67' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-types-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-types-after -->
<a id='snippet-xunit-assert-types-after'></a>
```cs
Verify(order is Order);
Verify(order.GetType() == typeof(PaidOrder));
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L66-L69' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-types-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Assert.IsType<T>` passes only for exactly `T`, and `is T` also passes for a type derived from `T`. Compare `GetType()` with `typeof(T)` when the exact type matters.

## Ranges

<!-- snippet: xunit-assert-range-before -->
<a id='snippet-xunit-assert-range-before'></a>
```cs
Assert.InRange(_order.Total, 1, 100);
Assert.True(_order.Total > 10);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L73-L76' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-range-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-range-after -->
<a id='snippet-xunit-assert-range-after'></a>
```cs
Verify(_order.Total is >= 1 and <= 100);
Verify(_order.Total > 10);
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L75-L78' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-range-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Floating-point values

<!-- snippet: xunit-assert-precision-before -->
<a id='snippet-xunit-assert-precision-before'></a>
```cs
Assert.Equal(4.0, _order.Tax, 6);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L82-L84' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-precision-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-precision-after -->
<a id='snippet-xunit-assert-precision-after'></a>
```cs
Verify(Math.Abs(_order.Tax - 4.0) < 0.000001);
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L84-L86' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-precision-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Compare the difference with a tolerance. Analyzer [VG0004](../diagnostics/VG0004.md) warns about `==` on `double` and `float`.

## Messages

<!-- snippet: xunit-assert-message-before -->
<a id='snippet-xunit-assert-message-before'></a>
```cs
Assert.True(_order.Total > 10, $"order {_order.Id} is too small");
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L90-L92' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-message-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-message-after -->
<a id='snippet-xunit-assert-message-after'></a>
```cs
Verify(_order.Total > 10, $"order {_order.Id} is too small");
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L92-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-message-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The holes of the interpolated message run only when the check fails.

## Exceptions

<!-- snippet: xunit-assert-throws-before -->
<a id='snippet-xunit-assert-throws-before'></a>
```cs
var error = Assert.Throws<InvalidOperationException>(() => cart.Checkout());
Assert.Equal("The cart is empty.", error.Message);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L100-L103' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-throws-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-throws-after -->
<a id='snippet-xunit-assert-throws-after'></a>
```cs
var error = Throws<InvalidOperationException>(() => cart.Checkout());
Verify(error.Message == "The cart is empty.");
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L102-L105' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-throws-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Assert.Throws<T>` passes only for exactly `T`, and `ThrowsExactly<T>` does the same. `Throws<T>` also passes for an exception derived from `T`, as `Assert.ThrowsAny<T>` does. `Assert.ThrowsAsync<T>` maps to `ThrowsExactlyAsync<T>` in the same way.

## Async exceptions

<!-- snippet: xunit-assert-throws-async-before -->
<a id='snippet-xunit-assert-throws-async-before'></a>
```cs
var timeout = await Assert.ThrowsAsync<TimeoutException>(() => client.GetAsync("/orders/7"));
Assert.Contains("/orders/7", timeout.Message);
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L111-L114' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-throws-async-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-throws-async-after -->
<a id='snippet-xunit-assert-throws-async-after'></a>
```cs
var timeout = await ThrowsAsync<TimeoutException>(() => client.GetAsync("/orders/7"));
Verify(timeout.Message.Contains("/orders/7"));
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L113-L116' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-throws-async-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Several checks at once

<!-- snippet: xunit-assert-multiple-before -->
<a id='snippet-xunit-assert-multiple-before'></a>
```cs
Assert.Multiple(
    () => Assert.Equal(40, _order.Total),
    () => Assert.Equal("express", _order.Shipping),
    () => Assert.Equal(2, _order.Lines.Count));
```
<sup><a href='/samples/MigrateFromXunitAssert/Before/OrderTests.cs#L120-L125' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-multiple-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: xunit-assert-multiple-after -->
<a id='snippet-xunit-assert-multiple-after'></a>
```cs
using (VerifyScope.Begin())
{
    Check(_order.Total == 40);
    Check(_order.Shipping == "express");
    Check(_order.Lines.Count == 2);
}
```
<sup><a href='/samples/MigrateFromXunitAssert/After/OrderTests.cs#L122-L129' title='Snippet source file'>snippet source</a> | <a href='#snippet-xunit-assert-multiple-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Check` records each failure, and the scope reports them all when it ends. The [Check guide](check.md) explains scopes.

## Get captured values

An xunit test method runs through reflection, so its checks give tier 2 messages. To see the value of each part of a condition, move the checks into a `[Verify]` method in a `partial` class, turn tier 3 on, and call the method from the test. The [tiers guide](tiers.md) explains how.
