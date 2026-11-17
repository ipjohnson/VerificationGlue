# Start with the piece you need

Choose a package, follow its setup guide, and run an example in your test project.

::: info Preview packages
All three packages are previews. VerificationGlue.Tests uses the MTP 3 preview and needs the feed and runner settings in its getting started guide.
:::

## Keep your test framework

Use **Verify** for checks that explain a failure. Use **Mocks** to set up dependencies and check their calls. Both work with xunit, NUnit, MSTest, and TUnit.

| I want to… | Start here |
| --- | --- |
| Write checks and read failures | [Get started with Verify](/docs/guide/getting-started-verify.md) |
| Set up a dependency in a test | [Get started with Mocks](/docs/guide/getting-started-mocks.md) |
| Collect failures before stopping | [Soft checks with Check](/docs/guide/check.md) |

## Try the test runner

**VerificationGlue.Tests** registers `[VerifyTest]` methods at build time. It includes Verify and Mocks and runs on Microsoft.Testing.Platform.

[Set up your first test project →](/docs/guide/getting-started-tests.md)

## Bring your existing tests

- [Move assertions from xunit](/docs/guide/migrate-from-xunit-assert.md).
- [Move mocks from NSubstitute](/docs/guide/migrate-from-nsubstitute.md).
- [Review differences from NSubstitute](/docs/guide/nsubstitute-differences.md).

## Understand what runs

Read the [generated interceptors](/docs/guide/generated/interceptors.md), [generated mocks](/docs/guide/generated/mocks.md), and [test registrations](/docs/guide/generated/test-registrations.md). Then [publish tests with Native AOT](/docs/guide/run-tests-under-native-aot.md).
