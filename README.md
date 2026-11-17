# VerificationGlue
C# based unit testing library that is built on AOT, source generation, and a lot of moxie!

## Verify

The `VerificationGlue` package gives checks whose failure messages name the file, the line and the shape of the condition, under any test framework and under Native AOT. It is a preview.

<!-- snippet: verify-in-xunit -->
<a id='snippet-verify-in-xunit'></a>
```cs
[Fact]
public async Task Checkout_reports_an_empty_cart()
{
    var order = new Order(Id: 7, Total: 40);
    Verify(order.Total == 40);
    Verify(order.Id > 0, $"order {order.Id} has no id");

    var error = Throws<InvalidOperationException>(() => new Cart().Checkout());
    Verify(error.Message == "The cart is empty.");

    var timeout = await ThrowsAsync<TimeoutException>(() => new Client().GetAsync("/orders/7"));
    Verify(timeout.Message.Contains("/orders/7"));
}
```
<sup><a href='/samples/VerifyInXunit/OrderTests.cs#L10-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-in-xunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

When `Verify(order.Total == 30)` fails, the message is:

```text
Verify failed: order.Total == 30
  expected order.Total to equal 30
  at OrderTests.cs:32
```

[Getting started with Verify](docs/guide/getting-started-verify.md) shows the setup for xunit, NUnit, MSTest and TUnit, and every message shape. The package README is [`src/VerificationGlue/README.md`](src/VerificationGlue/README.md).

## Mocks

The `VerificationGlue.Mocks` package generates mocks at build time, with NSubstitute-style setup and a typed setup surface. They work under any test framework and under Native AOT. It is a preview.

<!-- snippet: mocks-style-typed -->
<a id='snippet-mocks-style-typed'></a>
```cs
var calc = Mock.Of<ICalculator>();
calc.Setup().Add(1, Arg<int>.Any()).Returns(3);
calc.Setup().Divide(Arg<int>.Any(), 0).Throws(new DivideByZeroException());
calc.Setup().Mode.Returns(Mode.Scientific);

var sum = calc.Add(1, 2);

Verify(calc.Calls().Add(1, 2) == 1);
Verify(calc.Calls().Reset() == 0);
```
<sup><a href='/samples/MocksGuide/SetupStyles.cs#L31-L41' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-style-typed' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

[Getting started with mocks](docs/guide/getting-started-mocks.md) shows the setup for xunit, NUnit, MSTest and TUnit, with the hook each one needs for `[Mock]` test parameters. The package README is [`src/VerificationGlue.Mocks/README.md`](src/VerificationGlue.Mocks/README.md). [Migrate from NSubstitute](docs/guide/migrate-from-nsubstitute.md) and [Known differences from NSubstitute](docs/guide/nsubstitute-differences.md) cover a move from NSubstitute.

## Tests

The `VerificationGlue.Tests` package is a test framework on Microsoft.Testing.Platform. A method marked `[VerifyTest]` is a test, registered at build time, so the test project publishes with Native AOT. `dotnet test` and `dotnet run` run the tests, with `--filter-uid` and TRX reports. It builds against the MTP 3 preview and is a preview.

<!-- snippet: first-verify-tests -->
<a id='snippet-first-verify-tests'></a>
```cs
public partial class CartTests
{
    [VerifyTest]
    public void Total_adds_the_line_prices()
    {
        var prices = Mock.Of<IPrices>();
        prices.PriceOf("apple").Returns(3);
        prices.PriceOf("pear").Returns(4);
        var cart = new Cart(prices);

        cart.Add("apple", 2);
        cart.Add("pear", 1);

        Verify(cart.Total == 10);
    }

    [VerifyTest]
    public async Task Checkout_empties_the_cart(CancellationToken cancellationToken)
    {
        var cart = new Cart(Mock.Of<IPrices>());
        cart.Add("apple", 1);

        await cart.CheckoutAsync(cancellationToken);

        Verify(cart.Total == 0);
    }
}
```
<sup><a href='/samples/TestsGettingStarted/CartTests.cs#L27-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-first-verify-tests' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

[Getting started with VerificationGlue.Tests](docs/guide/getting-started-tests.md) shows the setup, including the `global.json` line that `dotnet test` needs. The package README is [`src/VerificationGlue.Tests/README.md`](src/VerificationGlue.Tests/README.md).

## Building

The SDK version and the test runner are pinned in `global.json`.

```
dotnet build VerificationGlue.slnx -c Release
dotnet test --solution VerificationGlue.slnx -c Release --no-build
dotnet run --project test/Generator.Benchmarks -c Release --no-build -- --filter '*' --job short --inProcess --artifacts artifacts/benchmarks
eng/pack-and-consume.sh --no-build
```

`eng/pack-and-consume.sh` packs the three packages into a local feed. It checks their layout and shared version, and publishes `samples/PackageConsumer`, `samples/VerifyAot` and `samples/MocksAot` with Native AOT. `samples/PackageConsumer` is a `VerificationGlue.Tests` project, and the script runs its tests from the published binary and records how long they take. The script also checks that a mismatched `VerificationGlue` version fails with VGT0007, and that `dotnet test` without the `global.json` runner setting fails with VGT1001. CI runs the same four commands.

`VerificationGlue.Tests` builds against the MTP 3 preview from the `test-tools` feed in `NuGet.config`. Every other project restores from nuget.org.

The samples under `samples/VerifyIn*` and `samples/MocksIn*` are test projects in xunit, NUnit, MSTest and TUnit, and `samples/TestsGettingStarted` is a `VerificationGlue.Tests` project. They are in the solution, so `dotnet test` runs them with the specs. `test/NSubstituteComparison.Specs` runs the NSubstitute comparison cases under both libraries. `test/VerificationGlue.Tests.Specs` builds `test/VerificationGlue.Tests.Fixture` and runs it as a process. The fixture is not in the solution, because some of its tests fail on purpose.

## Generator harnesses

`test/Generator.Harness` holds the code that generator specs and benchmarks share. `test/Generator.Harness.Sample` is a small generator that shows each harness in use.

- `GeneratorDriverHarness` runs a generator over in-memory sources and keeps its driver between runs. After an edit and a second run, `StepReasons` returns the `IncrementalStepRunReason` of each output of a tracked step.
- `GeneratorSnapshot.Match` compares the diagnostics and generated files of a run with `Snapshots/{Specs}.{Spec}.verified.txt` in the specs project. On a difference it writes a `.received.txt` file next to it and fails. Run the specs with `VERIFICATIONGLUE_ACCEPT_SNAPSHOTS=1` to accept the received output.
- `test/Generator.Benchmarks` uses BenchmarkDotNet to time a cold generator run and a run after an unrelated edit. CI uploads the reports as the `generator-benchmarks` artifact.

## Docs

`eng/check-docs.sh` expands the snippets in the docs from `samples/` and fails when the committed docs differ. It also checks links and style. CI runs it too. [`docs/writing-docs.md`](docs/writing-docs.md) explains each check.
