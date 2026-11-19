# Compare two values with VerifyEquivalent

At the end of this page you can compare two records or objects with `VerifyEquivalent`, including their arrays, lists, dictionaries and sets, compare a list without order or skip a member, and read the path of each difference in its message.

`VerifyEquivalent(actual, expected)` compares two values member by member. The generator writes a comparer for the type at each call site, with no reflection. The message lists the path of each difference, with the expected and the found value. It lists up to 10 differences and counts the rest.

## What each type compares

| Type | How it compares |
|---|---|
| A class, a struct or a record | Each public field and property, including those of base types |
| A one-dimensional array, or a class or interface that implements `IList<T>` or `IReadOnlyList<T>` | The number of items, then each item in order, or each item without order when `Unordered` names it |
| A class or interface that implements `IDictionary<TKey, TValue>` or `IReadOnlyDictionary<TKey, TValue>` | The value at each key, then the keys that only one side has |
| A class or interface that implements `ISet<T>` or `IReadOnlySet<T>` | The items, without order |
| A primitive, a string or an enum | `Equals` |
| A type that implements `IEquatable<T>` and is not a record | `Equals` |
| Any other collection, an interface, or a type in a `System` or `Microsoft` namespace | `Equals` |
| A type with no public field or property, or a type the generated code cannot name, such as a private nested type | `Equals` |
| An open generic type, such as `T` in a generic helper | `Equals` |

A type in the last two rows that has no `Equals` of its own compares by reference, so the build warns with [VG0011](../diagnostics/VG0011.md) at the call. So does an open generic type.

Null on one side is a difference at that path. The [messages page](messages.md#verifyequivalent) shows a failure for members of records.

## Cycles

A value that refers back to a value on its own path, such as a child that points to its parent, is a cycle. The comparer does not follow it. When both sides refer back to the same depth of the path, the cycle is not a difference. When only one side has a cycle there, or the two refer back to different depths, the message names the value that the cycle goes back to:

```text
  Next: expected no cycle, found a cycle back to the root
  Child.Parent: expected a cycle back to Child, found a cycle back to the root
```

A value that two members share is not a cycle. Each other pair of references compares once, at the first path that reaches it.

These records hold the values in the examples below:

<!-- snippet: verify-equivalent-types -->
<a id='snippet-verify-equivalent-types'></a>
```cs
public sealed record InvoiceLine(string Sku, int Quantity);

public sealed record Invoice(int Id, List<InvoiceLine> Lines, string[] Tags);

public sealed record Stock(Dictionary<string, int> Levels, HashSet<string> Tags);
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L72-L78' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-equivalent-types' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Arrays and lists

An array or a list compares its items in order. The path of an item gives its index, and an item that is a record compares member by member:

<!-- snippet: verify-equivalent-items -->
<a id='snippet-verify-equivalent-items'></a>
```cs
var parsed = new Invoice(7, [new InvoiceLine("A-1", 2), new InvoiceLine("B-2", 1)], ["gift"]);
var expected = new Invoice(7, [new InvoiceLine("A-1", 2), new InvoiceLine("B-2", 3)], ["gift"]);
VerifyEquivalent(parsed, expected);
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L19-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-equivalent-items' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: equivalent-items -->
```text
VerifyEquivalent failed: parsed is not equivalent to expected
  Lines[1].Quantity: expected 3, found 1
  at EquivalentMessages.cs:22
```
<!-- endMessage -->

When the two have a different number of items, the message gives both counts. The items at the indexes both have still compare, so the first item that differs is listed too:

<!-- snippet: verify-equivalent-counts -->
<a id='snippet-verify-equivalent-counts'></a>
```cs
var parsed = new Invoice(7, [new InvoiceLine("A-1", 1)], ["gift", "fragile"]);
var expected = new Invoice(7, [new InvoiceLine("A-1", 2), new InvoiceLine("B-2", 3)], ["gift"]);
VerifyEquivalent(parsed, expected);
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L28-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-equivalent-counts' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: equivalent-counts -->
```text
VerifyEquivalent failed: parsed is not equivalent to expected
  Lines: expected 2 items, found 1 item
  Lines[0].Quantity: expected 2, found 1
  Tags: expected 1 item, found 2 items
  at EquivalentMessages.cs:31
```
<!-- endMessage -->

## Unordered and Ignore

A third argument changes the comparison for the members it names. `Unordered` compares the items of an array or a list without order. `Ignore` skips a member. Each takes a lambda that names the member by a chain of member accesses, such as `x => x.Customer.Address`, and `x => x` names the value itself for `Unordered`. The generator reads the lambdas from the call and writes them into the comparer, so they never run:

<!-- snippet: verify-equivalent-options -->
<a id='snippet-verify-equivalent-options'></a>
```cs
var parsed = new Invoice(8, [new InvoiceLine("B-2", 3), new InvoiceLine("A-1", 2)], ["gift", "gift"]);
var expected = new Invoice(7, [new InvoiceLine("A-1", 2), new InvoiceLine("B-2", 3)], ["fragile", "gift"]);
VerifyEquivalent(parsed, expected, options => options.Unordered(x => x.Lines).Unordered(x => x.Tags).Ignore(x => x.Id));
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L64-L68' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-equivalent-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An unordered list matches each expected item with an actual item that is equivalent to it, each item once, so duplicates count. The message lists each expected item with no match as missing, and each actual item with no match as extra:

<!-- message: equivalent-options -->
```text
VerifyEquivalent failed: parsed is not equivalent to expected
  Tags: missing "fragile"
  Tags: extra "gift"
  at EquivalentMessages.cs:67
```
<!-- endMessage -->

An option applies only to the call that has it. Other calls that compare the same types still compare in order and read every member. A lambda that the generator cannot read, or that names a member the comparer does not read, is build error [VG0010](../diagnostics/VG0010.md). An option cannot name a member of the items of an array, a list, a dictionary or a set yet. A set already compares without order, so `Unordered` names only an array or a list.

## Dictionaries and sets

A dictionary compares by key. For each key that both have, the values compare, and the path names the key. A value that is a record compares member by member. Each entry whose key only the expected dictionary has is missing, and each entry whose key only the actual dictionary has is extra. Each dictionary looks up keys with its own key comparer, so a dictionary built with `StringComparer.OrdinalIgnoreCase` matches keys without case:

<!-- snippet: verify-equivalent-dictionary -->
<a id='snippet-verify-equivalent-dictionary'></a>
```cs
var parsed = new Stock(new() { ["gift"] = 3, ["wrap"] = 1 }, ["gift"]);
var expected = new Stock(new() { ["gift"] = 5, ["card"] = 2 }, ["gift"]);
VerifyEquivalent(parsed, expected);
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L46-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-equivalent-dictionary' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: equivalent-dictionary -->
```text
VerifyEquivalent failed: parsed is not equivalent to expected
  Levels["gift"]: expected 5, found 3
  Levels: missing "card": 2
  Levels: extra "wrap": 1
  at EquivalentMessages.cs:49
```
<!-- endMessage -->

A set compares its items without order. An item that the comparer does not walk, such as a string, is looked up with the set's own `Contains`. An item that it walks, such as a record, matches an item of the other set that is equivalent to it, as in an unordered list. The message lists each expected item with no match as missing, and each actual item with no match as extra:

<!-- snippet: verify-equivalent-set -->
<a id='snippet-verify-equivalent-set'></a>
```cs
var parsed = new Stock(new() { ["gift"] = 3 }, ["fragile", "gift"]);
var expected = new Stock(new() { ["gift"] = 3 }, ["gift", "express"]);
VerifyEquivalent(parsed, expected);
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L55-L59' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-equivalent-set' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: equivalent-set -->
```text
VerifyEquivalent failed: parsed is not equivalent to expected
  Tags: missing "express"
  Tags: extra "fragile"
  at EquivalentMessages.cs:58
```
<!-- endMessage -->

## Without the generator

Without the generator, `VerifyEquivalent` compares the two values with `Equals`. A record then still compares member by member, but a class compares by reference, and so does a list. The message gives both values and says that the generator did not run.

## Under Native AOT

The comparer is generated code, so `VerifyEquivalent` runs under Native AOT and gives the same message as on the JIT. `samples/VerifyAot` checks a `VerifyEquivalent` failure under Native AOT and on the JIT. [Run tests under Native AOT](run-tests-under-native-aot.md) shows how to publish a test project.
