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

### VerifyEquivalent

`VerifyEquivalent(actual, expected)` compares two values member by member. The generator writes a comparer for the type at each call site, with no reflection. The message gives the path of each member that differs, with the expected and the found value. It lists up to 10 differences and counts the rest.

The comparer walks the public fields and properties of classes, structs and records, including those of base types. An array or a list compares its items in order, and the path gives the index of each item that differs. A dictionary compares its entries by key, and a set compares its items without order. Primitives, strings, enums, and types that implement `IEquatable<T>` and are not records compare with `Equals`. So do other collections, interfaces, types in `System` and `Microsoft` namespaces, types with no public field or property, and types the generated code cannot name, such as a private nested type. Null on one side is a difference at that path. The comparer does not follow a value that refers back to a value on its own path, and a cycle on one side only is a difference. [Compare two values with VerifyEquivalent](verify-equivalent.md) shows the messages for arrays, lists, dictionaries and sets.

<!-- snippet: message-tier2-equivalent -->
<a id='snippet-message-tier2-equivalent'></a>
```cs
var parsed = new Shipment(7, new Address("Ada", "London"), ShipmentStatus.Packed, null);
var expected = new Shipment(7, new Address("Ada", "Paris"), ShipmentStatus.Sent, new Address("Ada", "Paris"));
VerifyEquivalent(parsed, expected);
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L10-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-equivalent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-equivalent -->
```text
VerifyEquivalent failed: parsed is not equivalent to expected
  To.City: expected "Paris", found "London"
  Status: expected Sent, found Packed
  ReturnTo: expected Address { Name = Ada, City = Paris }, found null
  at EquivalentMessages.cs:13
```
<!-- endMessage -->

A dictionary names the key of each value that differs, and lists each entry whose key only one side has as missing or extra. A set lists each item that only one side has:

<!-- snippet: message-tier2-equivalent-maps -->
<a id='snippet-message-tier2-equivalent-maps'></a>
```cs
var parsed = new Stock(new() { ["gift"] = 3, ["wrap"] = 1 }, ["fragile", "gift"]);
var expected = new Stock(new() { ["gift"] = 5, ["card"] = 2 }, ["gift", "express"]);
VerifyEquivalent(parsed, expected);
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L37-L41' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier2-equivalent-maps' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier2-equivalent-maps -->
```text
VerifyEquivalent failed: parsed is not equivalent to expected
  Levels["gift"]: expected 5, found 3
  Levels: missing "card": 2
  Levels: extra "wrap": 1
  Tags: missing "express"
  Tags: extra "fragile"
  at EquivalentMessages.cs:40
```
<!-- endMessage -->

Without the generator, `VerifyEquivalent` compares the two values with `Equals`. A record then still compares member by member, but a class compares by reference. The message gives both values and says that the generator did not run.

## Tier 3: captured values

A `[Verify]` method in a `partial` type gets a tier 3 copy when the project sets `VerificationGlueRewrite`. The message lists the value of each part of the condition, as it was when the condition ran. Nothing is evaluated again to build the message, except the lambda of a failed `All`, as [Collections](#collections) says.

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

### Strings

A condition of the form `a == b` on two strings adds lines after the values that show where the strings first differ. Lines from `a` start with `-` and lines from `b` start with `+`. Once these lines are shown, each string value is cut to 40 characters.

When either string has more than one line, the message gives the line and column of the first difference and two lines on each side of it. Lines split at `\n`, so a `\r` stays in its line and shows as `\r`. A side with no line at that point shows `<end of text>`.

<!-- snippet: message-tier3-string-lines -->
<a id='snippet-message-tier3-string-lines'></a>
```cs
[Verify]
public static void VerifySvg(string written, string expected)
{
    Verify(written == expected);
}
```
<sup><a href='/samples/Messages/StringMessages.cs#L15-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-string-lines' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-string-lines -->
```text
Verify failed: written == expected
  written  → "<svg>\n  <g class=\"title\">\n    <text>Orde"… (112 characters)
  expected → "<svg>\n  <g class=\"title\">\n    <text>Orde"… (112 characters)
  first difference at line 6, column 19
    4     </g>
    5     <g class="node">
  - 6       <rect width="80">
  + 6       <rect width="82">
    7     </g>
    8   </svg>
  at StringMessages.cs:19
```
<!-- endMessage -->

When both strings are on one line and either is longer than 40 characters, the message gives the index of the first difference and the text around it.

<!-- snippet: message-tier3-string-index -->
<a id='snippet-message-tier3-string-index'></a>
```cs
[Verify]
public static void VerifyTitle(string title, string expected)
{
    Verify(title == expected);
}
```
<sup><a href='/samples/Messages/StringMessages.cs#L23-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-string-index' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-string-index -->
```text
Verify failed: title == expected
  title    → "Order 7 for Ada Lovelace, shipped standa"… (56 characters)
  expected → "Order 7 for Ada Lovelace, shipped expres"… (55 characters)
  first difference at index 34
  - "Order 7 for Ada Lovelace, shipped standard on 2026-11-18"
  + "Order 7 for Ada Lovelace, shipped express on 2026-11-18"
  at StringMessages.cs:27
```
<!-- endMessage -->

### Collections

A condition of the form `a.SequenceEqual(b)` adds lines before the values. They give the first index at which the items differ with the item each side has there, both lengths, and the items the longer side has past the end of the shorter. It works for the `Enumerable` and `MemoryExtensions` forms without a comparer.

<!-- snippet: message-tier3-sequence-equal -->
<a id='snippet-message-tier3-sequence-equal'></a>
```cs
[Verify]
public static void VerifyLines(int[] lines, int[] expected)
{
    Verify(lines.SequenceEqual(expected));
}
```
<sup><a href='/samples/Messages/CollectionMessages.cs#L24-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-sequence-equal' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-sequence-equal -->
```text
Verify failed: lines.SequenceEqual(expected)
  first difference at index 2: lines has 25, expected has 30
  lines has 3 items, expected has 4
  expected has 1 item past the end of lines: 40
  lines    → 3 items: 10, 20, 25
  expected → 4 items: 10, 20, 30, 40
  at CollectionMessages.cs:28
```
<!-- endMessage -->

A condition of the form `a.SetEquals(b)` adds a line for the items of `b` that `a` is missing and a line for the items of `a` that `b` does not have. Items match by the comparer of a `HashSet<T>`, and by the default comparer for any other set.

<!-- snippet: message-tier3-set-equals -->
<a id='snippet-message-tier3-set-equals'></a>
```cs
[Verify]
public static void VerifyTags(HashSet<string> tags, string[] expected)
{
    Verify(tags.SetEquals(expected));
}
```
<sup><a href='/samples/Messages/CollectionMessages.cs#L32-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-set-equals' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-set-equals -->
```text
Verify failed: tags.SetEquals(expected)
  tags is missing 1 item that expected has: "priority"
  tags has 2 items that expected does not: "fragile", "express"
  tags     → 3 items: "gift", "fragile", "express"
  expected → 2 items: "gift", "priority"
  at CollectionMessages.cs:36
```
<!-- endMessage -->

A comparison with `a.Count`, `a.Length` or `a.Count()` on either side lists the items of `a`. The row of `a` lists them, as [Collection values](#collection-values) says. When that row cannot list them, a line before the values does, and an empty `a` shows `a is empty`. The `Length` of a string gets no line.

<!-- snippet: message-tier3-count -->
<a id='snippet-message-tier3-count'></a>
```cs
[Verify]
public static void VerifyLineCount(List<int> lines)
{
    Verify(lines.Count == 3);
}
```
<sup><a href='/samples/Messages/CollectionMessages.cs#L40-L46' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-count' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-count -->
```text
Verify failed: lines.Count == 3
  lines.Count → 2
  lines       → 2 items: 10, 20
  at CollectionMessages.cs:44
```
<!-- endMessage -->

A condition of the form `a.All(x => ...)` adds a line with the index and value of the first item that fails. To find that item, the message runs the lambda again on the items up to it. The lambda runs again only when the check fails, and only to build the message. When it throws on that run, the message has no line for it.

<!-- snippet: message-tier3-all -->
<a id='snippet-message-tier3-all'></a>
```cs
[Verify]
public static void VerifyQuantities(int[] quantities)
{
    Verify(quantities.All(quantity => quantity > 0));
}
```
<sup><a href='/samples/Messages/CollectionMessages.cs#L48-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-all' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-all -->
```text
Verify failed: quantities.All(quantity => quantity > 0)
  first item that fails at index 2: 0
  quantities → 4 items: 2, 5, 0, 3
  at CollectionMessages.cs:52
```
<!-- endMessage -->

A condition of the form `a.Any()`, `a.Any(x => ...)` or `a.Contains(x)` lists the items of `a` in the same way. The lambda in `Any` does not run again, because the list does not need it.

<!-- snippet: message-tier3-any -->
<a id='snippet-message-tier3-any'></a>
```cs
[Verify]
public static void VerifyBackorder(int[] quantities)
{
    Verify(quantities.Any(quantity => quantity > 10));
}
```
<sup><a href='/samples/Messages/CollectionMessages.cs#L56-L62' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-any' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-any -->
```text
Verify failed: quantities.Any(quantity => quantity > 10)
  quantities → 3 items: 2, 5, 3
  at CollectionMessages.cs:60
```
<!-- endMessage -->

<!-- snippet: message-tier3-contains -->
<a id='snippet-message-tier3-contains'></a>
```cs
[Verify]
public static void VerifyExpress(string[] tags)
{
    Verify(tags.Contains("express"));
}
```
<sup><a href='/samples/Messages/CollectionMessages.cs#L64-L70' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-contains' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: tier3-contains -->
```text
Verify failed: tags.Contains("express")
  tags → 2 items: "gift", "fragile"
  at CollectionMessages.cs:68
```
<!-- endMessage -->

Items print the way values do, and a list of items stops after 10 with `…`. Each sequence the lines read must be a collection, such as an array, a list or a set. A lazy sequence, such as the result of `Select`, would run its code again when read, so it gets no lines. The lines come only when the condition is one of these calls or comparisons as a whole, so `a.Count == 3 && ok` gets none.

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

### Collection values

A value that is a collection, such as an array, a list, a set, a queue or a dictionary, shows its count and its items. This holds for `Verify(() => ...)`, `Check(() => ...)` and tier 3. The items go on the same line when they fit, and one per line when they do not. The list stops after 10 items with `…`. A dictionary shows each key and its value. A collection inside a collection shows its items in brackets. A string shows as a string. The message reads the items when it is built, so a collection that the condition changed shows its items after the change.

<!-- snippet: message-lambda-collection -->
<a id='snippet-message-lambda-collection'></a>
```cs
var problems = new List<string> { "flowchart/a: box differs", "pie/b: missing label" };
Verify(() => problems.Count == 0);
```
<sup><a href='/samples/Messages/LambdaMessages.cs#L25-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-lambda-collection' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: lambda-collection -->
```text
Verify failed: problems.Count == 0
  problems.Count → 2
  problems       → 2 items
                     "flowchart/a: box differs"
                     "pie/b: missing label"
  at LambdaMessages.cs:27
```
<!-- endMessage -->

A sequence that is not a collection, such as the result of `Select` or a method that uses `yield`, is not read again, because reading it could run its code. It shows its type with a note. The runtime picks the type of a LINQ result, so the name can differ under Native AOT.

<!-- snippet: message-lambda-lazy -->
<a id='snippet-message-lambda-lazy'></a>
```cs
var totals = new[] { 30, 45 }.Select(total => total * 2);
Verify(() => totals.Count() == 0);
```
<sup><a href='/samples/Messages/LambdaMessages.cs#L33-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-lambda-lazy' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: lambda-lazy -->
```text
Verify failed: totals.Count() == 0
  totals.Count() → 2
  totals         → ArraySelectIterator<Int32, Int32> (not read again, because reading it could run its code)
  at LambdaMessages.cs:35
```
<!-- endMessage -->

### Recognised shapes

A failed `SequenceEqual`, `SetEquals`, `==` on two strings or `Math.Abs(a - b) <= t` adds the same lines it adds at tier 3, as [Collections](#collections), [Strings](#strings) and [Tolerances](#tolerances) show. So do `Count`, `Any`, `Contains` and `All`. `Check(() => ...)` adds the same lines.

<!-- snippet: message-lambda-sequence -->
<a id='snippet-message-lambda-sequence'></a>
```cs
string[] a = ["x", "y", "z"];
string[] b = ["x", "q", "z"];
Verify(() => a.SequenceEqual(b));
```
<sup><a href='/samples/Messages/LambdaMessages.cs#L41-L45' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-lambda-sequence' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: lambda-sequence -->
```text
Verify failed: a.SequenceEqual(b)
  first difference at index 1: a has "y", b has "q"
  a has 3 items, b has 3
  a → 3 items: "x", "y", "z"
  b → 3 items: "x", "q", "z"
  at LambdaMessages.cs:44
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
<sup><a href='/samples/Messages/LambdaMessages.cs#L50-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-lambda-members' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: lambda-members -->
```text
Verify failed: _order.Total == expected
  _order.Total → 30
  _order       → order 7
  expected     → 40
  at LambdaMessages.cs:53
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
<sup><a href='/samples/Messages/LambdaMessages.cs#L59-L64' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-lambda-fallback' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: lambda-fallback -->
```text
Verify failed: IsEven(total + 1)
  expected IsEven(total + 1) to return true
  at LambdaMessages.cs:61
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
