# Data rows

At the end of this page you can run one test method with several sets of literal values, find each row's result, and run one row on its own.

## One test per row

Put a `[Row]` attribute on a `[VerifyTest]` method for each set of values. The generator registers one test per row:

<!-- snippet: tests-literal-rows -->
<a id='snippet-tests-literal-rows'></a>
```cs
[VerifyTest]
[Row(2, 10, 20)]
[Row(3, 5, 15)]
public void Totals(int qty, int price, int expected) => Verify(Pricing.Total(qty, price) == expected);
```
<sup><a href='/samples/TestsDataRows/PricingTests.cs#L20-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-literal-rows' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A test explorer and the TRX report show two results:

```text
Totals(qty: 2, price: 10, expected: 20)
Totals(qty: 3, price: 5, expected: 15)
```

Each row is its own test. It gets its own test scope, its own container and its own result.

## Rows and other parameters

The values of a row go to the method's first parameters, in order. The other parameters come from the usual sources in [Parameters and where they come from](test-parameters.md), such as the container or the built-in values:

<!-- snippet: tests-row-and-other-parameters -->
<a id='snippet-tests-row-and-other-parameters'></a>
```cs
[VerifyTest]
[Row("SAVE10", 90)]
[Row("HALF", 50)]
public void Discounts(string code, int expected, ITestOutput output)
{
    output.WriteLine($"Applying {code} to 100");
    Verify(Pricing.Discounted(100, code) == expected);
}
```
<sup><a href='/samples/TestsDataRows/PricingTests.cs#L27-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-row-and-other-parameters' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The display name lists only the values from the row: `Discounts(code: "SAVE10", expected: 90)`.

## Value types

A value must convert to its parameter type the way C# converts it without a cast. An `int` value fits a `long` or `double` parameter, and an `int` that is in range fits a `byte` or `short` parameter. `null` fits a reference type or a nullable value type. A value that does not fit, or a row with more values than the method has parameters, fails the build with [VGT0002](../diagnostics/VGT0002.md).

Attribute arguments cannot be `decimal`, but a whole number fits a `decimal` parameter: `[Row(2, 10)]` gives `price` the value `10m` in `Totals(int qty, decimal price)`. For a fraction, pass a `string` and parse it in the test. A `double` value does not fit a `decimal` parameter, because C# needs a cast for that conversion.

## Test IDs

A row's test ID is the method's ID followed by the row's values with their types:

```text
TestsDataRows.PricingTests.Totals(System.Int32,System.Int32,System.Int32)#row(System.Int32:2,System.Int32:10,System.Int32:20)
```

The ID comes from the values, not from the row's position. Adding a row, removing another, or moving rows keeps the ID of every row whose values did not change. A retry, a filter or a test explorer's history therefore finds the same row.

Two rows with the same values get the same ID plus an occurrence number. The second copy ends with `#2`, the third with `#3`.

## Run one row

Pass a row's test ID to `--filter-uid`:

```sh
dotnet run --project TestsDataRows -- --filter-uid "TestsDataRows.PricingTests.Totals(System.Int32,System.Int32,System.Int32)#row(System.Int32:3,System.Int32:5,System.Int32:15)"
```

Only that row runs. Its container is built and disposed, and the other rows build nothing.

## Display names

The default name is the method name followed by each row value with its parameter name. Strings and characters are in quotes, enums have their type name, and numbers use the invariant culture. A test without rows shows the method name.

To write other names, add an `ITestNameFormatter` with `[NameFormatter<T>]`. [Extending VerificationGlue.Tests](extending.md#name-formatters) shows one. The display name never changes the test ID.

## Next

- [Parameters and where they come from](test-parameters.md)
- [Containers and scopes](containers-and-scopes.md)
