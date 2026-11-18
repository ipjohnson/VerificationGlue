# VerificationGlue

**Clear expectations. Readable failures.**

C# checks, generated mocks, and a test runner built for Native AOT. Use Verify and Mocks with your existing test framework, or run tests with VerificationGlue.Tests on Microsoft.Testing.Platform.

[Documentation site preview](spikes/docs-site/README.md) · [Getting started](docs/guide/getting-started-verify.md) · [Samples](samples) · [Diagnostics](docs/diagnostics)

> **Preview packages:** all four packages are previews. VerificationGlue.Tests builds against the MTP 3 preview and needs the feed and runner settings in its [setup guide](docs/guide/getting-started-tests.md).

## See what failed

When `Verify(order.Total == 30)` fails, the message names the condition, its expected shape, and the source location:

```text
Verify failed: order.Total == 30
  expected order.Total to equal 30
  at OrderTests.cs:32
```

The [xunit sample](samples/VerifyInXunit/OrderTests.cs) checks this message. The same source information is available under Native AOT. [Read the failure message guide](docs/guide/getting-started-verify.md#read-a-failure).

## Choose your starting point

| Package | What it gives you | Get started |
| --- | --- | --- |
| `VerificationGlue` | Checks with `Verify`, `Throws`, and `ThrowsAsync` | [Write your first check](docs/guide/getting-started-verify.md) |
| `VerificationGlue.Mocks` | Mocks generated at build time, with typed setup and call checks | [Set up a mock](docs/guide/getting-started-mocks.md) |
| `VerificationGlue.Tests` | Tests registered at build time with `[VerifyTest]` | [Run your first test](docs/guide/getting-started-tests.md) |
| `VerificationGlue.Tests.AspNetCore` | An ASP.NET Core app started for each test, with an `HttpClient` and the test's mocks | [Test an app](docs/guide/getting-started-aspnetcore.md) |

Verify and Mocks work with xunit, NUnit, MSTest, and TUnit. The Tests package includes both and runs on Microsoft.Testing.Platform.

`VerificationGlue.Tests.AspNetCore` starts each app on TestServer at test scope. Its other phases are in [its design](docs/design/aspnet.md).

## Write expectations in C#

Import `VerificationGlue.VerifySyntax` with `using static`, then write a condition:

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

[Install and use Verify](docs/guide/getting-started-verify.md) · [Collect failures with Check](docs/guide/check.md) · [Understand failure messages](docs/guide/messages.md)

<details>
<summary><strong>Set up dependencies with generated mocks</strong></summary>

Use the typed `Setup()` surface to configure behavior and `Calls()` to check how a mock was used:

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

[Get started with Mocks](docs/guide/getting-started-mocks.md) · [Choose a setup style](docs/guide/choose-a-setup-style.md)

</details>

<details>
<summary><strong>Run tests registered at build time</strong></summary>

Mark methods with `[VerifyTest]` in a partial class. Run them with `dotnet test` or `dotnet run`:

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

[Get started with Tests](docs/guide/getting-started-tests.md) · [Run tests under Native AOT](docs/guide/run-tests-under-native-aot.md) · [Set up CI with TRX and retries](docs/guide/ci-setup-with-trx-and-retries.md)

</details>

## Bring your existing tests

- [Migrate from xunit assertions](docs/guide/migrate-from-xunit-assert.md).
- [Migrate from NSubstitute](docs/guide/migrate-from-nsubstitute.md).
- [Review differences from NSubstitute](docs/guide/nsubstitute-differences.md).

## Explore the documentation site

The [VitePress site preview](spikes/docs-site/README.md) brings the guides and diagnostic pages together with search, package navigation, and runnable sample code. [View the homepage mockup](spikes/docs-site/mockups/home-desktop.png) or run it locally:

```sh
cd spikes/docs-site
npm ci
npm run dev
```

Open `http://localhost:5173/VerificationGlue/`. The planned public address is `https://ipjohnson.github.io/VerificationGlue/`; publication is pending. The preview includes the GitHub Pages workflow example and setup instructions.

## Build and contribute

The SDK and test runner are pinned in `global.json`. `NuGet.config` includes the MTP preview feed.

```sh
dotnet build VerificationGlue.slnx -c Release
dotnet test --solution VerificationGlue.slnx -c Release --no-build
```

<details>
<summary><strong>Build checks and generator harnesses</strong></summary>

### Build, package, and benchmark

The SDK version and the test runner are pinned in `global.json`.

```
dotnet build VerificationGlue.slnx -c Release
dotnet test --solution VerificationGlue.slnx -c Release --no-build
dotnet run --project test/Generator.Benchmarks -c Release --no-build -- --filter '*' --job short --inProcess --artifacts artifacts/benchmarks
eng/pack-and-consume.sh --no-build
```

`eng/pack-and-consume.sh` packs the four packages into a local feed. It checks their layout, their shared version and their exact dependencies on each other, and publishes `samples/PackageConsumer`, `samples/VerifyAot` and `samples/MocksAot` with Native AOT. `samples/PackageConsumer` is a `VerificationGlue.Tests` project, and the script runs its tests from the published binary and records how long they take. The script also checks that a mismatched `VerificationGlue` version fails with VGT0007, and that `dotnet test` without the `global.json` runner setting fails with VGT1001. CI runs the same four commands.

`VerificationGlue.Tests` builds against the MTP 3 preview from the `test-tools` feed in `NuGet.config`. Every other project restores from nuget.org.

The samples under `samples/VerifyIn*` and `samples/MocksIn*` are test projects in xunit, NUnit, MSTest and TUnit. `samples/TestsGettingStarted` is a `VerificationGlue.Tests` project, and `samples/TestsAspNetCore` tests the app in `samples/ShopApi` with `VerificationGlue.Tests.AspNetCore`. They are in the solution, so `dotnet test` runs them with the specs. `test/NSubstituteComparison.Specs` runs the NSubstitute comparison cases under both libraries. `test/VerificationGlue.Tests.Specs` builds `test/VerificationGlue.Tests.Fixture` and runs it as a process. The fixture is not in the solution, because some of its tests fail on purpose.

### Generator harnesses

`test/Generator.Harness` holds the code that generator specs and benchmarks share. `test/Generator.Harness.Sample` is a small generator that shows each harness in use.

- `GeneratorDriverHarness` runs a generator over in-memory sources and keeps its driver between runs. After an edit and a second run, `StepReasons` returns the `IncrementalStepRunReason` of each output of a tracked step.
- `GeneratorSnapshot.Match` compares the diagnostics and generated files of a run with `Snapshots/{Specs}.{Spec}.verified.txt` in the specs project. On a difference it writes a `.received.txt` file next to it and fails. Run the specs with `VERIFICATIONGLUE_ACCEPT_SNAPSHOTS=1` to accept the received output.
- `test/Generator.Benchmarks` uses BenchmarkDotNet to time a cold generator run and a run after an unrelated edit. For the entry generator it also times a run after an edit to the app. CI uploads the reports as the `generator-benchmarks` artifact.


</details>

`eng/check-docs.sh` checks sample snippets, links, and writing style. See [Writing docs](docs/writing-docs.md) to contribute a guide or diagnostic page.
