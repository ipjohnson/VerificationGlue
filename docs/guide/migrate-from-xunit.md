# Migrate from xunit

At the end of this page you can move an xunit v3 test project to `VerificationGlue.Tests`. Each test keeps its name, its data and its checks, and runs with `dotnet test` as before.

Each pair comes from two test projects that CI builds and runs: [`samples/MigrateFromXunit/Before`](../../samples/MigrateFromXunit/Before), which uses xunit v3, and [`samples/MigrateFromXunit/After`](../../samples/MigrateFromXunit/After), which uses `VerificationGlue.Tests`. This repository's own `.Specs` projects moved the same way. [Moving a large project](#moving-a-large-project) describes that move.

## Change the project

Remove the `xunit.v3` reference and the `OutputType`:

<!-- snippet: migrate-from-xunit-project-before -->
<a id='snippet-migrate-from-xunit-project-before'></a>
```csproj
<PropertyGroup>
  <OutputType>Exe</OutputType>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="xunit.v3" />
</ItemGroup>
```
<sup><a href='/samples/MigrateFromXunit/Before/MigrateFromXunit.Before.csproj#L3-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-project-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Reference `VerificationGlue.Tests` instead:

<!-- snippet: reference-verificationglue-tests -->
<a id='snippet-reference-verificationglue-tests'></a>
```csproj
<ItemGroup>
  <PackageReference Include="VerificationGlue.Tests" Version="$(VerificationGlueVersion)" />
</ItemGroup>
```
<sup><a href='/samples/PackageConsumer/PackageConsumer.csproj#L20-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-reference-verificationglue-tests' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The package makes the project a test application, so the project needs no `OutputType`. `xunit.v3` and `VerificationGlue.Tests` run on different versions of Microsoft.Testing.Platform, so one project cannot reference both.

`dotnet test` needs this line in the `global.json` next to the solution, as it does for xunit v3 on Microsoft.Testing.Platform:

```json
{
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

## What changes

| xunit v3 | VerificationGlue.Tests |
|---|---|
| `[Fact]` | `[VerifyTest]` |
| `[Theory]` with `[InlineData]` | `[VerifyTest]` with `[Row]` |
| `[MemberData]` returning `TheoryData` | `[RowSource]` returning tuples |
| `TestContext.Current.CancellationToken` | a `CancellationToken` parameter |
| `ITestOutputHelper` in the constructor | an `ITestOutput` parameter |
| `[Collection]` | `[Resource]` or `[ScheduleGroup]` |
| `[Trait]` | `[Trait]` |
| `IClassFixture<T>` | `[Container<T>]` with class scope |

Put `using VerificationGlue.Tests;` in each test file, or add it as a global using.

## Tests

<!-- snippet: migrate-from-xunit-fact-before -->
<a id='snippet-migrate-from-xunit-fact-before'></a>
```cs
[Fact]
public void Total_multiplies_quantity_and_price()
{
    Assert.Equal(20, Pricing.Total(2, 10));
}
```
<sup><a href='/samples/MigrateFromXunit/Before/CheckoutTests.cs#L8-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-fact-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: migrate-from-xunit-fact-after -->
<a id='snippet-migrate-from-xunit-fact-after'></a>
```cs
[VerifyTest]
public void Total_multiplies_quantity_and_price()
{
    Verify(Pricing.Total(2, 10) == 20);
}
```
<sup><a href='/samples/MigrateFromXunit/After/CheckoutTests.cs#L9-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-fact-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The checks in the samples also move from `Assert` to `Verify`. That step is optional. [Keep xunit's Assert](#keep-xunits-assert) shows how to leave the checks as they are.

## Rows

<!-- snippet: migrate-from-xunit-inline-data-before -->
<a id='snippet-migrate-from-xunit-inline-data-before'></a>
```cs
[Theory]
[InlineData(2, 10, 20)]
[InlineData(3, 5, 15)]
public void Totals(int qty, int price, int expected)
{
    Assert.Equal(expected, Pricing.Total(qty, price));
}
```
<sup><a href='/samples/MigrateFromXunit/Before/CheckoutTests.cs#L16-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-inline-data-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: migrate-from-xunit-inline-data-after -->
<a id='snippet-migrate-from-xunit-inline-data-after'></a>
```cs
[VerifyTest]
[Row(2, 10, 20)]
[Row(3, 5, 15)]
public void Totals(int qty, int price, int expected)
{
    Verify(Pricing.Total(qty, price) == expected);
}
```
<sup><a href='/samples/MigrateFromXunit/After/CheckoutTests.cs#L17-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-inline-data-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each row is its own test, with its own ID and result. [Data rows](data-rows.md) describes the IDs and the value types a row takes.

## Rows from a member

<!-- snippet: migrate-from-xunit-member-data-before -->
<a id='snippet-migrate-from-xunit-member-data-before'></a>
```cs
public static TheoryData<string, int> Codes => new()
{
    { "SAVE10", 90 },
    { "HALF", 50 },
};

[Theory]
[MemberData(nameof(Codes))]
public void Discounts(string code, int expected)
{
    Assert.Equal(expected, Pricing.Discounted(100, code));
}
```
<sup><a href='/samples/MigrateFromXunit/Before/CheckoutTests.cs#L26-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-member-data-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: migrate-from-xunit-member-data-after -->
<a id='snippet-migrate-from-xunit-member-data-after'></a>
```cs
public static IEnumerable<(string Code, int Expected)> Codes =>
[
    ("SAVE10", 90),
    ("HALF", 50),
];

[VerifyTest]
[RowSource(nameof(Codes))]
public void Discounts(string code, int expected)
{
    Verify(Pricing.Discounted(100, code) == expected);
}
```
<sup><a href='/samples/MigrateFromXunit/After/CheckoutTests.cs#L27-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-member-data-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`[RowSource]` takes a static method, property or field. It returns `IEnumerable<T>` where `T` is a tuple of the row's values, or a single value when the method takes one. `TheoryData` does not fit, because its elements are `TheoryDataRow` objects.

## Cancellation and output

<!-- snippet: migrate-from-xunit-context-before -->
<a id='snippet-migrate-from-xunit-context-before'></a>
```cs
public class ReceiptTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Prints_the_total()
    {
        var receipt = await new Receipts().PrintAsync(20, TestContext.Current.CancellationToken);

        output.WriteLine(receipt);
        Assert.Equal("Total: 20", receipt);
    }
}
```
<sup><a href='/samples/MigrateFromXunit/Before/CheckoutTests.cs#L51-L63' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-context-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: migrate-from-xunit-context-after -->
<a id='snippet-migrate-from-xunit-context-after'></a>
```cs
public partial class ReceiptTests
{
    [VerifyTest]
    public async Task Prints_the_total(ITestOutput output, CancellationToken cancellationToken)
    {
        var receipt = await new Receipts().PrintAsync(20, cancellationToken);

        output.WriteLine(receipt);
        Verify(receipt == "Total: 20");
    }
}
```
<sup><a href='/samples/MigrateFromXunit/After/CheckoutTests.cs#L52-L64' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-context-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`VerificationGlue.Tests` has no `TestContext.Current`. A test takes the built-in values it needs as parameters after its row values. [Parameters and where they come from](test-parameters.md#built-in-values) lists them. A helper method that needs the token takes it as a parameter too.

## Tests that must not overlap

xunit runs the tests of one class one at a time, and runs the classes of one collection one at a time. `VerificationGlue.Tests` runs every test in parallel, up to the processor count, so the tests of one class can run at the same time.

Give tests that share state the same `[Resource]` key. Two tests that hold the same key never run at the same time:

<!-- snippet: migrate-from-xunit-collection-before -->
<a id='snippet-migrate-from-xunit-collection-before'></a>
```cs
[Collection("stock")]
public class TakeTests
{
    [Fact]
    public async Task Taking_two_leaves_eight()
    {
        Stock.Reset(10);
        Assert.Equal(8, await Stock.TakeAsync(2));
    }
}

[Collection("stock")]
public class RestockTests
{
    [Fact]
    public async Task Taking_five_leaves_five()
    {
        Stock.Reset(10);
        Assert.Equal(5, await Stock.TakeAsync(5));
    }
}
```
<sup><a href='/samples/MigrateFromXunit/Before/CheckoutTests.cs#L65-L87' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-collection-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: migrate-from-xunit-collection-after -->
<a id='snippet-migrate-from-xunit-collection-after'></a>
```cs
[Resource("stock")]
public partial class TakeTests
{
    [VerifyTest]
    public async Task Taking_two_leaves_eight()
    {
        Stock.Reset(10);
        Verify(await Stock.TakeAsync(2) == 8);
    }
}

[Resource("stock")]
public partial class RestockTests
{
    [VerifyTest]
    public async Task Taking_five_leaves_five()
    {
        Stock.Reset(10);
        Verify(await Stock.TakeAsync(5) == 5);
    }
}
```
<sup><a href='/samples/MigrateFromXunit/After/CheckoutTests.cs#L66-L88' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-collection-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

To run all the tests of a class one at a time, put the class in a schedule group with `MaxParallel = 1`. [Scheduling groups and resources](scheduling-groups-and-resources.md) describes both.

## Traits

<!-- snippet: migrate-from-xunit-trait-before -->
<a id='snippet-migrate-from-xunit-trait-before'></a>
```cs
[Fact]
[Trait("Speed", "Fast")]
public void Unknown_code_keeps_the_total()
{
    Assert.Equal(100, Pricing.Discounted(100, "NONE"));
}
```
<sup><a href='/samples/MigrateFromXunit/Before/CheckoutTests.cs#L41-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-trait-before' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: migrate-from-xunit-trait-after -->
<a id='snippet-migrate-from-xunit-trait-after'></a>
```cs
[VerifyTest]
[Trait("Speed", "Fast")]
public void Unknown_code_keeps_the_total()
{
    Verify(Pricing.Discounted(100, "NONE") == 100);
}
```
<sup><a href='/samples/MigrateFromXunit/After/CheckoutTests.cs#L42-L49' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-trait-after' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Select tests by trait with `--vg-filter "trait:Speed=Fast"`. [CI setup with TRX and retries](ci-setup-with-trx-and-retries.md) shows the filter syntax.

## Fixtures

A class fixture becomes a container that the class shares. [Containers and scopes](containers-and-scopes.md#share-a-container-across-a-class-or-the-assembly) shows `[Container<T>(Scope = ContainerScope.Class)]`. The runner disposes a test class instance that it created, as xunit does. [Test lifecycle](test-lifecycle.md#who-disposes-what) lists who disposes what.

## Skipped tests

`VerificationGlue.Tests` has no `Skip`. Delete a test that should not run. A test that must not run in some environments returns early, as `test/Generator.Harness.Specs/SnapshotSpecs.cs` does when snapshots are being accepted.

## Keep xunit's Assert

The `xunit.v3.assert` package holds `Assert` without the xunit runner. Reference it at the version of `xunit.v3` you used, to keep the checks unchanged. This repository's `test/Directory.Build.props` does so, with the version in `Directory.Packages.props`:

<!-- snippet: migrate-from-xunit-keep-assert -->
<a id='snippet-migrate-from-xunit-keep-assert'></a>
```props
<PackageReference Include="xunit.v3.assert" />
```
<sup><a href='/test/Directory.Build.props#L59-L61' title='Snippet source file'>snippet source</a> | <a href='#snippet-migrate-from-xunit-keep-assert' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Assert` is in the `Xunit` namespace, so keep `using Xunit;`. A failed `Assert` fails the test as it did on xunit. To move the checks to `Verify` later, follow [Migrate from xunit Assert](migrate-from-xunit-assert.md).

## Moving a large project

This repository moved its `.Specs` projects to `VerificationGlue.Tests` one project at a time, and kept `Assert` through `xunit.v3.assert`. `test/Directory.Build.props` adds the package and the references to every `.Specs` project. `test/VerificationGlue.Tests.Specs` stays on xunit v3, because a runner cannot be the only check of itself.

The conformance suites of `VerificationGlue` and `VerificationGlue.Mocks` also run on xunit v3 in CI, so a defect in `VerificationGlue.Tests` cannot hide a defect in those packages. `dotnet test -p:SpecsRunner=xunit` builds such a project against `test/XunitShim.cs`, which gives `[VerifyTest]`, `[Row]` and `[RowSource]` to xunit.

## Next

- [Getting started with VerificationGlue.Tests](getting-started-tests.md)
- [Data rows](data-rows.md)
- [Scheduling groups and resources](scheduling-groups-and-resources.md)
- [Migrate from xunit Assert](migrate-from-xunit-assert.md)
