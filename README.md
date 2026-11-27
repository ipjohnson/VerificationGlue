<h1><img src="assets/logo/lockup-card.svg" alt="VerificationGlue" width="368" height="110"></h1>

[![CI](https://github.com/ipjohnson/VerificationGlue/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ipjohnson/VerificationGlue/actions/workflows/ci.yml)

## Verify your code

Write the C# condition you expect to be true. When it fails, see the values that made it fail.

Call `Verify(() => order.Total == 30)` on an order with a total of 40 and you get:

```text
Verify failed: order.Total == 30
  order.Total → 40
  order       → Order { Id = 7, Total = 40 }
  at OrderTests.cs:51
```

Use Verify in your existing test framework. The failure message also includes source information under Native AOT. [Write your first check](docs/guide/getting-started-verify.md) · [Read a failure message](docs/guide/messages.md)

## Mock a dependency. Control what happens.

An order needs a timestamp. Create a clock with `var clock = Mock.Of<IClock>();` and set the time it reports with `clock.Now.Returns(now);`. Every read of `clock.Now` returns the instant you chose.

[Set up a generated mock](docs/guide/getting-started-mocks.md)

## Test with your container

How do you give that clock to an order service? Mark the test class with `[Container<ShopBootstrap>]` and give the test two parameters, `OrderService orders` and `[Mock] IClock clock`. The runner registers the mock before resolving the service. Set the clock, create an order, and the order carries the time you chose.

[See the container and test together](docs/guide/containers-and-scopes.md#replace-a-service-with-a-mock)

## Launch your app in a test

Send the order through your ASP.NET Core endpoint. Each test starts its own app, and the endpoint receives the same clock the test controls.

<!-- snippet: first-app-test -->
<a id='snippet-first-app-test'></a>
```cs
[WebApp<ShopApiEntry>]
public partial class OrderTests
{
    [VerifyTest]
    public async Task Post_stamps_the_order_with_the_clock(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(2));
        var order = await response.Content.ReadFromJsonAsync<Order>();

        Verify(response.IsSuccessStatusCode);
        Verify(order!.Created == new DateTime(2026, 11, 17));
    }
}
```
<sup><a href='/samples/TestsAspNetCore/OrderTests.cs#L10-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-first-app-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

[Test your first endpoint](docs/guide/getting-started-aspnetcore.md)

## Choose your starting point

| Package | What it gives you | Get started |
| --- | --- | --- |
| `VerificationGlue` | Checks with `Verify`, `Throws`, and `ThrowsAsync` | [Write your first check](docs/guide/getting-started-verify.md) |
| `VerificationGlue.Mocks` | Mocks generated at build time, with typed setup and call checks | [Set up a mock](docs/guide/getting-started-mocks.md) |
| `VerificationGlue.Tests` | Tests registered at build time with `[VerifyTest]` | [Run your first test](docs/guide/getting-started-tests.md) |
| `VerificationGlue.Tests.AspNetCore` | An ASP.NET Core app started for each test, with an `HttpClient` and the test's mocks | [Test an app](docs/guide/getting-started-aspnetcore.md) |
| `VerificationGlue.Tests.DependencyInjection` | A Microsoft.Extensions.DependencyInjection container for each test, with the test's mocks | [Give tests a service collection](docs/guide/service-collection.md) |

Verify and Mocks work with xunit, NUnit, MSTest, and TUnit. The Tests package includes both and runs on Microsoft.Testing.Platform.

> **Preview packages:** all five packages are previews. VerificationGlue.Tests builds against the MTP 3 preview and needs the feed and runner settings in its [setup guide](docs/guide/getting-started-tests.md).

## Bring your existing tests

- [Migrate from xunit assertions](docs/guide/migrate-from-xunit-assert.md).
- [Migrate from NSubstitute](docs/guide/migrate-from-nsubstitute.md).
- [Review differences from NSubstitute](docs/guide/nsubstitute-differences.md).

## Explore the documentation site

The documentation site at `https://ipjohnson.github.io/VerificationGlue/` brings the guides and diagnostic pages together with search, package navigation, and runnable sample code. Its source is in [`spikes/docs-site`](spikes/docs-site/README.md). To run it locally:

```sh
cd spikes/docs-site
npm ci
npm run dev
```

Open `http://localhost:5173/VerificationGlue/`. The `docs-pages.yml` workflow publishes the site when a change to `docs/`, `spikes/docs-site/` or this README reaches `main`.

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

`eng/pack-and-consume.sh` packs the five packages into a local feed. It checks their layout, their shared version and their exact dependencies on each other, and publishes `samples/PackageConsumer`, `samples/VerifyAot`, `samples/MocksAot` and `samples/ModulesConsumer` with Native AOT. `samples/ModulesConsumer` uses `VerificationGlue.Tests.DependencyInjection` with DependencyModules modules, and the script runs its tests from the published binary. `samples/PackageConsumer` is a `VerificationGlue.Tests` project, and the script runs its tests from the published binary and records how long they take. The script also checks that a mismatched `VerificationGlue` version fails with VGT0007, and that `dotnet test` without the `global.json` runner setting fails with VGT1001. CI runs the same four commands.

`VerificationGlue.Tests` builds against the MTP 3 preview from the `test-tools` feed in `NuGet.config`. Every other project restores from nuget.org.

The samples under `samples/VerifyIn*` and `samples/MocksIn*` are test projects in xunit, NUnit, MSTest and TUnit. `samples/TestsGettingStarted` is a `VerificationGlue.Tests` project, and `samples/TestsAspNetCore` tests the app in `samples/ShopApi` with `VerificationGlue.Tests.AspNetCore`. `samples/TestsDependencyModules` tests the DependencyModules modules of `samples/ModulesShop` and its own modules with `VerificationGlue.Tests.DependencyInjection`. They are in the solution, so `dotnet test` runs them with the specs. `test/NSubstituteComparison.Specs` runs the NSubstitute comparison cases under both libraries. `test/VerificationGlue.Tests.Specs` builds `test/VerificationGlue.Tests.Fixture` and runs it as a process. The fixture is not in the solution, because some of its tests fail on purpose.

### Generator harnesses

`test/Generator.Harness` holds the code that generator specs and benchmarks share. `test/Generator.Harness.Sample` is a small generator that shows each harness in use.

- `GeneratorDriverHarness` runs a generator over in-memory sources and keeps its driver between runs. After an edit and a second run, `StepReasons` returns the `IncrementalStepRunReason` of each output of a tracked step.
- `GeneratorSnapshot.Match` compares the diagnostics and generated files of a run with `Snapshots/{Specs}.{Spec}.verified.txt` in the specs project. On a difference it writes a `.received.txt` file next to it and fails. Run the specs with `VERIFICATIONGLUE_ACCEPT_SNAPSHOTS=1` to accept the received output.
- `test/Generator.Benchmarks` uses BenchmarkDotNet to time a cold generator run and a run after an unrelated edit. For the entry generator it also times a run after an edit to the app. CI uploads the reports as the `generator-benchmarks` artifact.


</details>

`eng/check-docs.sh` checks sample snippets, links, and writing style. See [Writing docs](docs/writing-docs.md) to contribute a guide or diagnostic page.

## License

VerificationGlue is licensed under the [MIT license](LICENSE).
