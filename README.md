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

## Building

The SDK version and the test runner are pinned in `global.json`.

```
dotnet build VerificationGlue.slnx -c Release
dotnet test --solution VerificationGlue.slnx -c Release --no-build
dotnet run --project test/Generator.Benchmarks -c Release --no-build -- --filter '*' --job short --inProcess --artifacts artifacts/benchmarks
eng/pack-and-consume.sh --no-build
```

`eng/pack-and-consume.sh` packs the three packages into a local feed. It checks their layout and shared version, and publishes `samples/PackageConsumer` and `samples/VerifyAot` with Native AOT. `samples/VerifyAot` runs and checks its failure messages. The script also checks that a mismatched `VerificationGlue` version fails with VGT0007. CI runs the same four commands.

The samples under `samples/VerifyIn*` are test projects in xunit, NUnit, MSTest and TUnit. They are in the solution, so `dotnet test` runs them with the specs.

## Generator harnesses

`test/Generator.Harness` holds the code that generator specs and benchmarks share. `test/Generator.Harness.Sample` is a small generator that shows each harness in use.

- `GeneratorDriverHarness` runs a generator over in-memory sources and keeps its driver between runs. After an edit and a second run, `StepReasons` returns the `IncrementalStepRunReason` of each output of a tracked step.
- `GeneratorSnapshot.Match` compares the diagnostics and generated files of a run with `Snapshots/{Specs}.{Spec}.verified.txt` in the specs project. On a difference it writes a `.received.txt` file next to it and fails. Run the specs with `VERIFICATIONGLUE_ACCEPT_SNAPSHOTS=1` to accept the received output.
- `test/Generator.Benchmarks` uses BenchmarkDotNet to time a cold generator run and a run after an unrelated edit. CI uploads the reports as the `generator-benchmarks` artifact.

## Docs

`eng/check-docs.sh` expands the snippets in the docs from `samples/` and fails when the committed docs differ. It also checks links and style. CI runs it too. [`docs/writing-docs.md`](docs/writing-docs.md) explains each check.
