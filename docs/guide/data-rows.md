# Data rows

At the end of this page you can run one test method with several sets of values, from literal rows or from a source that the runner reads at discovery. You can find each row's result, run one row on its own, and retry it in a fresh process.

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

## Rows from a member

`[RowSource(nameof(Member))]` takes the rows from a static member of the test class. The member is a method without parameters, a property or a field. It returns `IEnumerable<T>` or `IAsyncEnumerable<T>`, where `T` is a tuple of the values of the method's first parameters:

<!-- snippet: tests-row-source -->
<a id='snippet-tests-row-source'></a>
```cs
public static IEnumerable<(int Qty, int Price, int Expected)> Orders() =>
[
    (1, 4, 4),
    (6, 2, 12),
];

[VerifyTest]
[RowSource(nameof(Orders))]
public void Order_totals(int qty, int price, int expected) => Verify(Pricing.Total(qty, price) == expected);
```
<sup><a href='/samples/TestsDataRows/PricingTests.cs#L38-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-row-source' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The runner reads the member at discovery, and each row is its own test, as with `[Row]`. The generator writes a typed call to the member, so the call needs no reflection. A member with a single value per row can return `IEnumerable<int>` or any other element type instead of a tuple.

Each tuple value must convert to its parameter type without a cast. A member that is missing, is not static, is private, or returns rows that do not fit the method fails the build with [VGT0005](../diagnostics/VGT0005.md).

## Rows from a data source attribute

An attribute that implements `ITestDataSource` gives rows to the method it is placed on. `GetRowsAsync` returns a `TestRow` for each row. A `TestRow` holds the values of the method's first parameters and an optional `Key`:

<!-- snippet: tests-data-source -->
<a id='snippet-tests-data-source'></a>
```cs
/// <summary>Gives one row per stored customer, keyed by the customer's name.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CustomersAttribute : Attribute, ITestDataSource
{
    public async IAsyncEnumerable<TestRow> GetRowsAsync(TestDataContext context)
    {
        await Task.Yield();
        foreach (var customer in CustomerStore.All())
        {
            yield return new TestRow(customer) { Key = customer.Name };
        }
    }
}
```
<sup><a href='/samples/TestsDataRows/PricingTests.cs#L64-L78' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-data-source' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Put the attribute on a test method:

<!-- snippet: tests-data-source-usage -->
<a id='snippet-tests-data-source-usage'></a>
```cs
[VerifyTest]
[Customers]
public void Earns_points(Customer customer) => Verify(customer.Points >= 0);
```
<sup><a href='/samples/TestsDataRows/PricingTests.cs#L50-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-data-source-usage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`TestDataContext` names the test method and its parameters, and carries a cancellation token. [Extending VerificationGlue.Tests](extending.md#data-sources) describes the interface.

## Test IDs of runtime rows

The runner computes a runtime row's test ID at discovery, in every process that runs tests. A retry by `Microsoft.Testing.Extensions.Retry` starts a fresh process, which reads the source again. The ID must therefore name the same data in every process, whatever order the source gives its rows in. The runner picks the first of these rules that applies:

1. A row with a `TestRow.Key` takes its ID from the key, such as `#key("ann")`. Each key of a test must be unique.
2. A row whose values are all `null`, primitives, decimals, strings or enums takes its ID from its values, such as `#row(System.Int32:1,System.Int32:4,System.Int32:4)`. Two rows with the same values get `#2`, `#3`, as literal rows do.
3. A row from a source that declares deterministic replay takes its ID from its position, such as `#index(0)`. Set `DeterministicReplay = true` on `[RowSource]`, or return `true` from `ITestDataSource.DeterministicReplay`, only when the source gives the same rows in the same order in every process.

A row that matches none of these rules fails discovery. A row of records from a database, for example, needs a key, because its position can change between processes.

## A source that fails

The runner never selects different data in place of a row it cannot find. A source fails discovery in these cases:

- It throws while the runner reads it.
- It gives no rows.
- Two of its rows have the same key.
- A row has no key, its values are not all primitives, strings or enums, and the source does not declare deterministic replay.
- A row has more values than the method has parameters, or a value that its parameter cannot take.

The test method is then reported once, under its own test ID, as failed with the reason. `--list-tests` exits with code 2. A run selects this failed result when it filters on any of the method's row IDs, so a retry whose source fails in the fresh process reports the failure and runs no row.

## Display names

The default name is the method name followed by each row value with its parameter name. Strings and characters are in quotes, enums have their type name, and numbers use the invariant culture. A test without rows shows the method name.

To write other names, add an `ITestNameFormatter` with `[NameFormatter<T>]`. [Extending VerificationGlue.Tests](extending.md#name-formatters) shows one. The display name never changes the test ID.

## Next

- [Parameters and where they come from](test-parameters.md)
- [Extending VerificationGlue.Tests](extending.md)
- [Containers and scopes](containers-and-scopes.md)
- [CI setup with TRX and retries](ci-setup-with-trx-and-retries.md)
