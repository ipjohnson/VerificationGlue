# Compare two values with VerifyEquivalent

At the end of this page you can compare two records or objects with `VerifyEquivalent`, including their arrays and lists, and read the path of each difference in its message.

`VerifyEquivalent(actual, expected)` compares two values member by member. The generator writes a comparer for the type at each call site, with no reflection. The message lists the path of each difference, with the expected and the found value. It lists up to 10 differences and counts the rest.

## What each type compares

| Type | How it compares |
|---|---|
| A class, a struct or a record | Each public field and property, including those of base types |
| A one-dimensional array, or a class or interface that implements `IList<T>` or `IReadOnlyList<T>` | The number of items, then each item in order |
| A dictionary or a set | `Equals` |
| A primitive, a string or an enum | `Equals` |
| A type that implements `IEquatable<T>` and is not a record | `Equals` |
| Any other collection, an interface, or a type in a `System` or `Microsoft` namespace | `Equals` |
| A type with no public field or property, or a type the generated code cannot name, such as a private nested type | `Equals` |

Null on one side is a difference at that path. A pair of references compares once, so a graph with a cycle ends. The [messages page](messages.md#verifyequivalent) shows a failure for members of records.

These records hold the values in the examples below:

<!-- snippet: verify-equivalent-types -->
<a id='snippet-verify-equivalent-types'></a>
```cs
public sealed record InvoiceLine(string Sku, int Quantity);

public sealed record Invoice(int Id, List<InvoiceLine> Lines, string[] Tags);

public sealed record Stock(Dictionary<string, int> Levels);
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L45-L51' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-equivalent-types' title='Start of snippet'>anchor</a></sup>
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

## Dictionaries and sets

A dictionary or a set compares with `Equals`. `Dictionary<TKey, TValue>` and `HashSet<T>` do not override `Equals`, so two of them with the same entries differ, and the message shows the same value on both sides:

<!-- snippet: verify-equivalent-dictionary -->
<a id='snippet-verify-equivalent-dictionary'></a>
```cs
var parsed = new Stock(new Dictionary<string, int> { ["gift"] = 3 });
var expected = new Stock(new Dictionary<string, int> { ["gift"] = 3 });
VerifyEquivalent(parsed, expected);
```
<sup><a href='/samples/Messages/EquivalentMessages.cs#L37-L41' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-equivalent-dictionary' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- message: equivalent-dictionary -->
```text
VerifyEquivalent failed: parsed is not equivalent to expected
  Levels: expected ["gift": 3], found ["gift": 3]
  at EquivalentMessages.cs:40
```
<!-- endMessage -->

## Without the generator

Without the generator, `VerifyEquivalent` compares the two values with `Equals`. A record then still compares member by member, but a class compares by reference, and so does a list. The message gives both values and says that the generator did not run.

## Under Native AOT

The comparer is generated code, so `VerifyEquivalent` runs under Native AOT and gives the same message as on the JIT. `samples/VerifyAot` checks a `VerifyEquivalent` failure under Native AOT and on the JIT. [Run tests under Native AOT](run-tests-under-native-aot.md) shows how to publish a test project.
