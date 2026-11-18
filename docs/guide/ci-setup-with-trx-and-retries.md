# CI setup with TRX and retries

At the end of this page your CI job writes a TRX report, runs a failed test again in a fresh process, and selects tests by name, trait or schedule group with `--vg-filter`.

## Add the extensions

TRX reports and retries come from two Microsoft.Testing.Platform extensions. Add them to the test project:

<!-- snippet: tests-ci-packages -->
<a id='snippet-tests-ci-packages'></a>
```csproj
<ItemGroup>
  <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="3.0.0-preview.26566.9" />
  <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="3.0.0-preview.26566.9" />
</ItemGroup>
```
<sup><a href='/samples/TestsCi/TestsCi.csproj#L19-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-ci-packages' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each package registers itself with the entry point that the build generates, so the project needs no code for them.

## The workflow

This GitHub Actions workflow runs the tests of `tests/Shop.Tests`, retries each failed test up to two times, leaves out the tests with the trait `Category=Manual`, and uploads the TRX reports:

<!-- snippet: tests-ci-workflow -->
<a id='snippet-tests-ci-workflow'></a>
```yml
name: tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Test
        run: dotnet test --project tests/Shop.Tests -c Release --report-trx --results-directory TestResults --retry-failed-tests 2 --vg-filter "!trait:Category=Manual"
      - name: Upload the TRX reports
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: TestResults/**/*.trx
```
<sup><a href='/samples/TestsCi/ci.yml#L4-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-ci-workflow' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`--report-trx` writes a TRX report to the folder that `--results-directory` names. `--retry-failed-tests 2` runs the tests that failed again, at most two more times. `--vg-filter` selects the tests, as [Select tests with --vg-filter](#select-tests-with---vg-filter) shows. The repository's `global.json` must set the test runner to Microsoft.Testing.Platform, as [Getting started with VerificationGlue.Tests](getting-started-tests.md#install) shows.

The job passes when every test passes in its last attempt. The run summary counts the tests that ran again, such as `total: 3 (+1 retried)`.

## Where the TRX reports go

The report in the results directory holds the results of the last attempt. When a run has retries, the report of each earlier attempt is in `Retries/<id>/<attempt>/` under the results directory. Upload `TestResults/**/*.trx`, as the workflow does, to keep every attempt.

In a run where `Converts(code: "GBP")` fails once and then passes, the first attempt's report has three results with the GBP row failed. The report in the results directory has one result, the GBP row passed.

## How a retry selects tests

Each retry starts a fresh process with the original command line and `--filter-uid` set to the IDs of the tests that failed. VerificationGlue.Tests runs a test only when it matches both `--filter-uid` and `--vg-filter`, so a retry stays inside the filtered selection.

A test ID does not change between processes. The ID of a test without rows is the full name of its class, the method name and the parameter types. These rows have IDs that come from their values:

<!-- snippet: tests-ci-rows -->
<a id='snippet-tests-ci-rows'></a>
```cs
public partial class CurrencyTests
{
    [VerifyTest]
    [Row("EUR")]
    [Row("GBP")]
    public async Task Converts(string code) => Verify(await RateFeed.RateAsync(code) > 0m);
}
```
<sup><a href='/samples/TestsCi/CheckoutTests.cs#L19-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-ci-rows' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

When the GBP row fails, the retry runs `TestsCi.CurrencyTests.Converts(System.String)#row(System.String:"GBP")` and not the EUR row. Adding, removing or moving rows between attempts does not change which row the retry runs.

A row from `[RowSource]` or an `ITestDataSource` is read again in the retry's process. Its ID comes from its key, its values or its position, as [Test IDs of runtime rows](data-rows.md#test-ids-of-runtime-rows) describes. When the source fails in the retry's process, the test fails with the reason and no row runs, as [A source that fails](data-rows.md#a-source-that-fails) describes.

## Select tests with --vg-filter

`--vg-filter` takes one expression. Each term starts with what it matches:

| Term | Selects a test when |
| --- | --- |
| `name:<pattern>` | The method name, the full name `Namespace.Class.Method` or the display name matches. |
| `trait:<name>=<pattern>` | The test has a `[Trait]` with that name and a value that matches. |
| `trait:<name>` | The test has a `[Trait]` with that name. |
| `group:<pattern>` | The name of the test's [schedule group](scheduling-groups-and-resources.md) matches. |

In a pattern, `*` matches any run of characters. Matching is case-sensitive. `!` in front of a term selects the tests that the term does not match. `&` joins terms that must all match, and `|` joins alternatives. `&` applies before `|`, and the expression has no parentheses.

`[Trait(name, value)]` goes on a class or a method. A test has the traits of its class and of its method:

<!-- snippet: tests-ci-traits -->
<a id='snippet-tests-ci-traits'></a>
```cs
[Trait("Category", "Checkout")]
public partial class CheckoutTests
{
    [VerifyTest]
    public void Adds_tax() => Verify(Prices.WithTax(100m) == 120m);

    [VerifyTest]
    [Trait("Category", "Manual")]
    public void Prints_a_receipt() => Verify(ReceiptPrinter.Print("order 1"));
}
```
<sup><a href='/samples/TestsCi/CheckoutTests.cs#L6-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-ci-traits' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

With these tests, each expression below selects:

| Expression | Tests |
| --- | --- |
| `trait:Category=Checkout` | `Adds_tax`, `Prints_a_receipt` |
| `!trait:Category=Manual` | Every test but `Prints_a_receipt` |
| `name:Converts(code: "GBP")` | The GBP row |
| `name:TestsCi.CheckoutTests.* & !trait:Category=Manual` | `Adds_tax` |
| `name:Adds_* \| name:Converts*` | `Adds_tax` and both rows of `Converts` |
| `group:default & trait:Category` | `Adds_tax`, `Prints_a_receipt` |

Each test node also carries its traits as a `TestMetadataProperty`.

An expression that matches no test runs nothing, and the run fails with exit code 8 and `Zero tests ran`. An expression that does not parse fails the run with exit code 5 before any test runs, and the message says which term is wrong:

```text
Option '--vg-filter' has invalid arguments: The --vg-filter value 'colour:red' is not valid: the term 'colour:red' does not start with name:, trait: or group:.
```

`--vg-filter` is an option of VerificationGlue.Tests. The experimental MTP filter `--treenode-filter` is not supported.

## Next

- [Data rows](data-rows.md)
- [Scheduling groups and resources](scheduling-groups-and-resources.md)
- [Run tests under Native AOT](run-tests-under-native-aot.md)
