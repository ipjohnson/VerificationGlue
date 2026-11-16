# VerificationGlue
C# based unit testing library that is built on AOT, source generation, and a lot of moxie!

## Building

The SDK version and the test runner are pinned in `global.json`.

```
dotnet build VerificationGlue.slnx -c Release
dotnet test --solution VerificationGlue.slnx -c Release --no-build
eng/pack-and-consume.sh --no-build
```

`eng/pack-and-consume.sh` packs the three packages into a local feed. It checks their layout and shared version, publishes `samples/PackageConsumer` with Native AOT, and checks that a mismatched `VerificationGlue` version fails with VGT0007. CI runs the same three commands.

## Docs

`eng/check-docs.sh` expands the snippets in the docs from `samples/` and fails when the committed docs differ. It also checks links and style. CI runs it too. [`docs/writing-docs.md`](docs/writing-docs.md) explains each check.
