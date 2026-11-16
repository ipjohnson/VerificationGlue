# VerificationGlue
C# based unit testing library that is built on AOT, source generation, and a lot of moxie!

## Building

The SDK version and the test runner are pinned in `global.json`.

```
dotnet build VerificationGlue.slnx -c Release
dotnet test --solution VerificationGlue.slnx -c Release --no-build
dotnet run --project test/Generator.Benchmarks -c Release --no-build -- --filter '*' --job short --inProcess --artifacts artifacts/benchmarks
eng/pack-and-consume.sh --no-build
```

`eng/pack-and-consume.sh` packs the three packages into a local feed. It checks their layout and shared version, publishes `samples/PackageConsumer` with Native AOT, and checks that a mismatched `VerificationGlue` version fails with VGT0007. CI runs the same four commands.

## Generator harnesses

`test/Generator.Harness` holds the code that generator specs and benchmarks share. `test/Generator.Harness.Sample` is a small generator that shows each harness in use.

- `GeneratorDriverHarness` runs a generator over in-memory sources and keeps its driver between runs. After an edit and a second run, `StepReasons` returns the `IncrementalStepRunReason` of each output of a tracked step.
- `GeneratorSnapshot.Match` compares the diagnostics and generated files of a run with `Snapshots/{Specs}.{Spec}.verified.txt` in the specs project. On a difference it writes a `.received.txt` file next to it and fails. Run the specs with `VERIFICATIONGLUE_ACCEPT_SNAPSHOTS=1` to accept the received output.
- `test/Generator.Benchmarks` uses BenchmarkDotNet to time a cold generator run and a run after an unrelated edit. CI uploads the reports as the `generator-benchmarks` artifact.

## Docs

`eng/check-docs.sh` expands the snippets in the docs from `samples/` and fails when the committed docs differ. It also checks links and style. CI runs it too. [`docs/writing-docs.md`](docs/writing-docs.md) explains each check.
