# Failure messages

This page shows the message each kind of failure gives. After reading it you can tell from a message which tier produced it, and read the values a tier 3 message lists.

Every message here is the real output of a check in [`samples/Messages`](../../samples/Messages) that fails on purpose. `test/Docs.Specs/MessagePageSpecs.cs` runs them and fails when this page differs from a fresh run. The [tiers guide](tiers.md) explains when each tier applies.

## Tier 2: the shape of the condition

With the generator running, every call gets a message for the shape of its condition, plus the file and line. Paths are relative to the project.

### Equality

<!-- snippet: message-tier2-equal -->
<a id='snippet-message-tier2-equal'></a>
```cs
Verify(order.Total == 40);
```
<sup><a href='/samples/Messages/Tier2Messages.cs#L14-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-equal' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-equal -->
```text
Verify failed: order.Total == 40
  expected order.Total to equal 40
  at Tier2Messages.cs:15
```
<!-- endMessage -->

### Null checks

<!-- snippet: message-tier2-not-null -->
<a id='snippet-message-tier2-not-null'></a>
```cs
Verify(order is not null);
```
<sup><a href='/samples/Messages/Tier2Messages.cs#L22-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-not-null' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-not-null -->
```text
Verify failed: order is not null
  order can not be null
  at Tier2Messages.cs:23
```
<!-- endMessage -->

### Comparisons

<!-- snippet: message-tier2-relational -->
<a id='snippet-message-tier2-relational'></a>
```cs
Verify(order.Total >= 50);
```
<sup><a href='/samples/Messages/Tier2Messages.cs#L30-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-relational' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-relational -->
```text
Verify failed: order.Total >= 50
  expected order.Total to be at least 50
  at Tier2Messages.cs:31
```
<!-- endMessage -->

### A call that returns bool

<!-- snippet: message-tier2-bool-call -->
<a id='snippet-message-tier2-bool-call'></a>
```cs
Verify(order.Shipping.StartsWith("express"));
```
<sup><a href='/samples/Messages/Tier2Messages.cs#L38-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-bool-call' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-bool-call -->
```text
Verify failed: order.Shipping.StartsWith("express")
  expected order.Shipping.StartsWith("express") to return true
  at Tier2Messages.cs:39
```
<!-- endMessage -->

### Patterns

<!-- snippet: message-tier2-pattern -->
<a id='snippet-message-tier2-pattern'></a>
```cs
Verify(order is Order { Total: > 50 });
```
<sup><a href='/samples/Messages/Tier2Messages.cs#L46-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-pattern' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-pattern -->
```text
Verify failed: order is Order { Total: > 50 }
  expected order to match Order { Total: > 50 }
  at Tier2Messages.cs:47
```
<!-- endMessage -->

### An interpolated message

The holes of the message run only when the check fails.

<!-- snippet: message-tier2-with-message -->
<a id='snippet-message-tier2-with-message'></a>
```cs
Verify(order.Total == 40, $"order {order.Id} has the wrong total");
```
<sup><a href='/samples/Messages/Tier2Messages.cs#L54-L56' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-with-message' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-with-message -->
```text
Verify failed: order.Total == 40
  expected order.Total to equal 40
  order 7 has the wrong total
  at Tier2Messages.cs:55
```
<!-- endMessage -->

### Throws

<!-- snippet: message-tier2-throws -->
<a id='snippet-message-tier2-throws'></a>
```cs
Throws<InvalidOperationException>(() => new Cart().Add(1));
```
<sup><a href='/samples/Messages/Tier2Messages.cs#L61-L63' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-throws' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-throws -->
```text
Throws failed: () => new Cart().Add(1)
  expected new Cart().Add(1) to throw InvalidOperationException
  no exception was thrown
  at Tier2Messages.cs:62
```
<!-- endMessage -->

### ThrowsExactly

`ThrowsExactly<T>` fails for an exception derived from `T`. The message names the type that was thrown.

<!-- snippet: message-tier2-throws-exactly -->
<a id='snippet-message-tier2-throws-exactly'></a>
```cs
ThrowsExactly<ArgumentException>(() => throw new ArgumentNullException("item"));
```
<sup><a href='/samples/Messages/Tier2Messages.cs#L68-L70' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-throws-exactly' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-throws-exactly -->
```text
ThrowsExactly failed: () => throw new ArgumentNullException("item")
  expected exactly ArgumentException, but ArgumentNullException was thrown
  at Tier2Messages.cs:69
```
<!-- endMessage -->

### Single

`Single` returns the only item. It fails when there are no items or more than one. The message gives the count, and lists up to 10 of the items when there is more than one.

<!-- snippet: message-tier2-single -->
<a id='snippet-message-tier2-single'></a>
```cs
var line = Single(order.Lines);
```
<sup><a href='/samples/Messages/ValueMessages.cs#L13-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-single' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-single -->
```text
Single failed: order.Lines
  expected one item, but found 2: [10, 20]
  at ValueMessages.cs:14
```
<!-- endMessage -->

With a predicate, `Single` returns the only item that matches. The message gives the count of matches, and lists up to 10 of them when more than one matches.

<!-- snippet: message-tier2-single-match -->
<a id='snippet-message-tier2-single-match'></a>
```cs
var order = Single(orders, o => o.Total > 50);
```
<sup><a href='/samples/Messages/ValueMessages.cs#L21-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-single-match' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-single-match -->
```text
Single failed: orders, o => o.Total > 50
  expected one match, but found 0
  at ValueMessages.cs:22
```
<!-- endMessage -->

### Is

`Is<T>` returns the value as a `T`. It fails when the value is null or not a `T`, and the message names the actual type.

<!-- snippet: message-tier2-is -->
<a id='snippet-message-tier2-is'></a>
```cs
var order = Is<Order>(item);
```
<sup><a href='/samples/Messages/ValueMessages.cs#L29-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-is' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-is -->
```text
Is failed: item
  expected Order, but it was Cart
  at ValueMessages.cs:30
```
<!-- endMessage -->

### NotNull

`NotNull` returns the value when it is not null. Its message has no line beyond the expression and the location.

<!-- snippet: message-tier2-not-null-value -->
<a id='snippet-message-tier2-not-null-value'></a>
```cs
var found = NotNull(order);
```
<sup><a href='/samples/Messages/ValueMessages.cs#L37-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-not-null-value' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-not-null-value -->
```text
NotNull failed: order
  at ValueMessages.cs:38
```
<!-- endMessage -->

## Tier 3: captured values

A `[Verify]` method in a `partial` type gets a tier 3 copy when the project sets `VerificationGlueRewrite`. The message lists the value of each part of the condition, as it was when the condition ran. Nothing is evaluated again to build the message.

### Values

<!-- snippet: message-tier3-values -->
<a id='snippet-message-tier3-values'></a>
```cs
[Verify]
public static void VerifyShipping(Order order)
{
    Verify(order.Shipping.StartsWith("express") && order.Lines.Count < 3);
}
```
<sup><a href='/samples/Messages/Tier3Messages.cs#L21-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-values' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-values -->
```text
Verify failed: order.Shipping.StartsWith("express") && order.Lines.Count < 3
  order.Shipping.StartsWith("express") → false
  order.Shipping                       → "standard"
  order                                → order 7
  order.Lines.Count < 3                → <not evaluated>
  order.Lines.Count                    → <not evaluated>
  order.Lines                          → <not evaluated>
  at Tier3Messages.cs:25
```
<!-- endMessage -->

### The skipped side of `&&` and `||`

A part that `&&`, `||`, `?:` or `??` skipped shows `<not evaluated>`.

<!-- snippet: message-tier3-skipped -->
<a id='snippet-message-tier3-skipped'></a>
```cs
[Verify]
public static void VerifyDiscount(Order order, string? code)
{
    Verify(code != null && code.Length == 6 || order.Total > 100);
}
```
<sup><a href='/samples/Messages/Tier3Messages.cs#L29-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-skipped' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-skipped -->
```text
Verify failed: code != null && code.Length == 6 || order.Total > 100
  code != null && code.Length == 6 → false
  code != null                     → false
  code                             → null
  code.Length == 6                 → <not evaluated>
  code.Length                      → <not evaluated>
  order.Total > 100                → false
  order.Total                      → 30
  order                            → order 7
  at Tier3Messages.cs:33
```
<!-- endMessage -->

### Because

`.Because(...)` adds a reason. It works only in a tier 3 copy. Analyzer [VG0001](../diagnostics/VG0001.md) reports it anywhere else.

<!-- snippet: message-tier3-because -->
<a id='snippet-message-tier3-because'></a>
```cs
[Verify]
public static void VerifyTotal(Order order)
{
    Verify(order.Total - Discount(order) == 25).Because("orders over 20 get 10 off");
}
```
<sup><a href='/samples/Messages/Tier3Messages.cs#L37-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-because' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-because -->
```text
Verify failed: order.Total - Discount(order) == 25
  order.Total - Discount(order) → 30
  order.Total                   → 30
  order                         → order 7
  Discount(order)               → 0
  because orders over 20 get 10 off
  at Tier3Messages.cs:41
```
<!-- endMessage -->

### Property patterns

The members a property pattern tests show `<unavailable>`. Their getters run once, in the pattern, and are not read again for the message.

<!-- snippet: message-tier3-pattern -->
<a id='snippet-message-tier3-pattern'></a>
```cs
[Verify]
public static void VerifyShape(Order order)
{
    Verify(order is { Total: > 50, Shipping: "express" });
}
```
<sup><a href='/samples/Messages/Tier3Messages.cs#L45-L51' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-pattern' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-pattern -->
```text
Verify failed: order is { Total: > 50, Shipping: "express" }
  order          → order 7
  order.Total    → <unavailable>
  order.Shipping → <unavailable>
  at Tier3Messages.cs:49
```
<!-- endMessage -->

### Tolerances

A condition of the form `Math.Abs(a - b) <= t` or `Math.Abs(a - b) < t` adds a line that says how far apart the values were and what the tolerance was. The line also says when either value is NaN. It works for `double`, `float` and `decimal`, and for the comparison written either way round.

<!-- snippet: message-tier3-tolerance -->
<a id='snippet-message-tier3-tolerance'></a>
```cs
[Verify]
public static void VerifyLeft(Box box)
{
    Verify(Math.Abs(box.X - 120.5) <= 0.01);
}
```
<sup><a href='/samples/Messages/ToleranceMessages.cs#L13-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-tolerance' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-tolerance -->
```text
Verify failed: Math.Abs(box.X - 120.5) <= 0.01
  box.X differs from 120.5 by 0.0625, tolerance 0.01
  Math.Abs(box.X - 120.5) → 0.0625
  box.X - 120.5           → 0.0625
  box.X                   → 120.5625
  box                     → Box { X = 120.5625, Y = 40 }
  at ToleranceMessages.cs:17
```
<!-- endMessage -->

## A lambda: values in any method

`Verify(() => ...)` and `Check(() => ...)` take the condition as a lambda. The generator writes a check for each call that reads the variables the lambda captured and runs the lambda's body once, keeping the value of each part. It works in any method, including a test body in xunit, NUnit, MSTest or TUnit. The method needs no `[Verify]`, its type needs no `partial`, and the project does not need `VerificationGlueRewrite`.

### Values

<!-- snippet: message-lambda-values -->
<a id='snippet-message-lambda-values'></a>
```cs
var order = new Order(7, 30, "standard");
Verify(() => order.Shipping.StartsWith("express") && order.Lines.Count < 3);
```
<sup><a href='/samples/Messages/LambdaMessages.cs#L17-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-lambda-values' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: lambda-values -->
```text
Verify failed: order.Shipping.StartsWith("express") && order.Lines.Count < 3
  order.Shipping.StartsWith("express") → false
  order.Shipping                       → "standard"
  order                                → order 7
  order.Lines.Count < 3                → <not evaluated>
  order.Lines.Count                    → <not evaluated>
  order.Lines                          → <not evaluated>
  at LambdaMessages.cs:19
```
<!-- endMessage -->

### Members of the type

The body can use the fields, properties and methods of the type it is in, including private ones.

<!-- snippet: message-lambda-members -->
<a id='snippet-message-lambda-members'></a>
```cs
private void VerifyTotal(int expected)
{
    Verify(() => _order.Total == expected);
}
```
<sup><a href='/samples/Messages/LambdaMessages.cs#L25-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-lambda-members' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: lambda-members -->
```text
Verify failed: _order.Total == expected
  _order.Total → 30
  _order       → order 7
  expected     → 40
  at LambdaMessages.cs:28
```
<!-- endMessage -->

### When values are not captured

When the body uses something the generated check cannot reach, the call gives its tier 2 message. The body still runs once. This happens for a local function, a private nested type, a variable whose type is a type parameter or an anonymous type, a write to a captured variable, `base`, and a lambda with a block body.

<!-- snippet: message-lambda-fallback -->
<a id='snippet-message-lambda-fallback'></a>
```cs
var total = 30;
Verify(() => IsEven(total + 1));

static bool IsEven(int value) => value % 2 == 0;
```
<sup><a href='/samples/Messages/LambdaMessages.cs#L34-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-lambda-fallback' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: lambda-fallback -->
```text
Verify failed: IsEven(total + 1)
  expected IsEven(total + 1) to return true
  at LambdaMessages.cs:36
```
<!-- endMessage -->

## Check

`Check` records each failure, and the scope lists them all when it ends. The [Check guide](check.md) shows how to open a scope.

### Tier 2

<!-- snippet: check-scope -->
<a id='snippet-check-scope'></a>
```cs
var order = new Order(7, 30, "standard");

using (VerifyScope.Begin())
{
    Check(order.Total == 40);
    Check(order.Shipping == "express");
    Check(order.Lines.Count == 2);
}
```
<sup><a href='/samples/Messages/CheckMessages.cs#L11-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-check-scope' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: check-tier2 -->
```text
2 checks failed

Check failed: order.Total == 40
  expected order.Total to equal 40
  at CheckMessages.cs:16

Check failed: order.Shipping == "express"
  expected order.Shipping to equal "express"
  at CheckMessages.cs:17
```
<!-- endMessage -->

### Tier 3

<!-- snippet: check-tier3 -->
<a id='snippet-check-tier3'></a>
```cs
[Verify]
public static void CheckOrder(Order order)
{
    Check(order.Total == 40);
    Check(order.Shipping.Length > 8);
}
```
<sup><a href='/samples/Messages/CheckMessages.cs#L31-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-check-tier3' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: check-tier3 -->
```text
2 checks failed

Check failed: order.Total == 40
  order.Total → 30
  order       → order 7
  at CheckMessages.cs:35

Check failed: order.Shipping.Length > 8
  order.Shipping.Length → 8
  order.Shipping        → "standard"
  order                 → order 7
  at CheckMessages.cs:36
```
<!-- endMessage -->
