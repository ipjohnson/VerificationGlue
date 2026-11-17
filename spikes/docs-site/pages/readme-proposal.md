# VerificationGlue

**C# checks, generated mocks, and tests built for Native AOT.**

Write expectations in C#. Get failure messages that name the condition, file, and line. Use Verify and Mocks with your existing test framework, or try the VerificationGlue.Tests runner.

> **Preview:** all three packages are previews. VerificationGlue.Tests builds against the MTP 3 preview.

[Start with Verify](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/getting-started-verify.md) · [Start with Mocks](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/getting-started-mocks.md) · [Start with Tests](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/getting-started-tests.md)

## A check you can read

```cs
var order = new Order(Id: 7, Total: 40);
Verify(order.Total == 30);
```

When the check fails:

```text
Verify failed: order.Total == 30
  expected order.Total to equal 30
  at OrderTests.cs:32
```

This example comes from the [xunit sample](https://github.com/ipjohnson/VerificationGlue/blob/main/samples/VerifyInXunit/OrderTests.cs). The [setup guide](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/getting-started-verify.md) covers the package reference, imports, and examples for each framework.

## Choose your starting point

| Package | Use it for | Framework |
| --- | --- | --- |
| `VerificationGlue` | Checks with `Verify`, `Throws`, and `ThrowsAsync` | xunit, NUnit, MSTest, TUnit |
| `VerificationGlue.Mocks` | Generated mocks, typed setup, and call checks | xunit, NUnit, MSTest, TUnit |
| `VerificationGlue.Tests` | Tests registered at build time with `[VerifyTest]` | Microsoft.Testing.Platform |

Verify and Mocks can be used with your current framework. The Tests package includes both.

## Move an existing suite

- [Migrate from xunit assertions](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/migrate-from-xunit-assert.md).
- [Migrate from NSubstitute](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/migrate-from-nsubstitute.md).
- [Review differences from NSubstitute](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/nsubstitute-differences.md).

## Go deeper

[Read failure messages](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/messages.md) · [Inspect generated code](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/generated/interceptors.md) · [Run tests with Native AOT](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/guide/run-tests-under-native-aot.md) · [Find a diagnostic](https://github.com/ipjohnson/VerificationGlue/tree/main/docs/diagnostics)

## Build and contribute

The SDK and test runner are pinned in `global.json`. The MTP 3 preview feed is configured in `NuGet.config`.

```sh
dotnet build VerificationGlue.slnx -c Release
dotnet test --solution VerificationGlue.slnx -c Release --no-build
```

See the [build and package checks](https://github.com/ipjohnson/VerificationGlue/blob/main/eng/pack-and-consume.sh) and the [documentation contribution guide](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/writing-docs.md).
