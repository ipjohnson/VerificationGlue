![VerificationGlue](https://raw.githubusercontent.com/ipjohnson/VerificationGlue/main/assets/logo/lockup.png)

# VerificationGlue.Tests

A test framework on Microsoft.Testing.Platform (MTP). A method marked `[VerifyTest]` is a test. The generator registers each test at build time, so discovery does no scanning and the test project publishes with Native AOT. The package builds against the MTP 3 preview and ships as a preview.

## Install

Reference the package. It brings `VerificationGlue` and `VerificationGlue.Mocks` at the same version, and makes the project a test application.

<!-- snippet: reference-verificationglue-tests -->
<a id='snippet-reference-verificationglue-tests'></a>
```csproj
<ItemGroup>
  <PackageReference Include="VerificationGlue.Tests" Version="$(VerificationGlueVersion)" />
</ItemGroup>
```
<sup><a href='/samples/PackageConsumer/PackageConsumer.csproj#L20-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-reference-verificationglue-tests' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`dotnet test` needs one line in the `global.json` next to the solution. `dotnet run` and `dotnet build` work without it.

```json
{
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

## Use

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

Run the tests with `dotnet test`, or with `dotnet run` on the test project. Select tests by ID with `--filter-uid`, and add `Microsoft.Testing.Extensions.TrxReport` for `--report-trx`.

## Docs

- [Getting started with VerificationGlue.Tests](../../docs/guide/getting-started-tests.md)
- [Migrate from xunit](../../docs/guide/migrate-from-xunit.md)
- [Containers and scopes](../../docs/guide/containers-and-scopes.md)
- [Parameters and where they come from](../../docs/guide/test-parameters.md)
- [Data rows](../../docs/guide/data-rows.md)
- [Test lifecycle](../../docs/guide/test-lifecycle.md)
- [Scheduling groups and resources](../../docs/guide/scheduling-groups-and-resources.md)
- [CI setup with TRX and retries](../../docs/guide/ci-setup-with-trx-and-retries.md)
- [Extending VerificationGlue.Tests](../../docs/guide/extending.md)
- [Run tests under Native AOT](../../docs/guide/run-tests-under-native-aot.md)
- [Generated test registrations](../../docs/guide/generated/test-registrations.md)
