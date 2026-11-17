# Getting started with VerificationGlue.Tests

At the end of this page you can write `[VerifyTest]` methods, run them with `dotnet test` or `dotnet run`, select tests by ID and write a TRX report.

`VerificationGlue.Tests` is a test framework on Microsoft.Testing.Platform (MTP). It builds against the MTP 3 preview, so the package ships as a preview.

## Install

Add the `VerificationGlue.Tests` package to the test project:

<!-- snippet: reference-verificationglue-tests -->
<a id='snippet-reference-verificationglue-tests'></a>
```csproj
<ItemGroup>
  <PackageReference Include="VerificationGlue.Tests" Version="$(VerificationGlueVersion)" />
</ItemGroup>
```
<sup><a href='/samples/PackageConsumer/PackageConsumer.csproj#L20-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-reference-verificationglue-tests' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The sample sets `VerificationGlueVersion` to the version it tests. A project writes the version number there.

The package depends on the same version of `VerificationGlue` and `VerificationGlue.Mocks`, so `Verify`, `Check` and `Mock.Of<T>()` need no other reference. Its `build/VerificationGlue.Tests.props` makes the project an executable test application. The entry point is generated, so the project has no `Main`.

The MTP 3 previews are published on the `test-tools` feed, not on nuget.org. Add the feed to the `NuGet.config` next to the solution:

```xml
<packageSources>
  <add key="test-tools" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/test-tools/nuget/v3/index.json" />
</packageSources>
```

`dotnet test` needs one more line. Add the test runner to the `global.json` next to the solution:

```json
{
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

Without it, `dotnet test` fails with [VGT1001](../diagnostics/VGT1001.md). `dotnet build`, `dotnet run` and `dotnet publish` work without it.

## Write a test

A public method marked `[VerifyTest]` is a test. Check results with `Verify` from `VerificationGlue`:

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

A test method returns `void`, `Task` or `ValueTask`. It can be static. For an instance method, the runner creates the class with its parameterless constructor and disposes it after the test when it implements `IDisposable` or `IAsyncDisposable`.

A test can take a `CancellationToken`, which is cancelled when the run is cancelled, and a `TestContext`, which holds the test ID and the attributes of the test. A parameter of any other type fails the test with a message that names the parameter. Containers that supply services arrive in a later preview.

A method the runner cannot call, such as a non-public or generic method, reports [VGT0004](../diagnostics/VGT0004.md) and is not registered.

## Run the tests

`dotnet test` runs every test project in the solution:

```sh
dotnet test
```

The test project is an executable, so `dotnet run` runs its tests too, and so does the built executable:

```sh
dotnet run --project tests/Shop.Tests
```

Both take the MTP options. `--list-tests` lists the tests, and `--filter-uid` runs the tests with the given IDs. A test ID is the full name of the class, the method name and the parameter types:

```sh
dotnet run --project tests/Shop.Tests -- --filter-uid "Shop.CartTests.Checkout_empties_the_cart(System.Threading.CancellationToken)"
```

The ID does not change when the display name or the order of methods changes, so filters, retries and IDE history keep working. Two tests with the same ID report [VGT0001](../diagnostics/VGT0001.md).

## Write a TRX report

Add the MTP TRX extension to the test project:

<!-- snippet: reference-trx-report -->
<a id='snippet-reference-trx-report'></a>
```csproj
<ItemGroup>
  <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="3.0.0-preview.26566.9" />
</ItemGroup>
```
<sup><a href='/samples/PackageConsumer/PackageConsumer.csproj#L26-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-reference-trx-report' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Then pass `--report-trx`. The report goes to the `TestResults` folder, or to the folder given with `--results-directory`:

```sh
dotnet test --report-trx
```

## Messages with values

With tier 3 rewriting enabled, a failed `Verify` shows the value of each part of the condition. Set the property in the test project:

```xml
<PropertyGroup>
  <VerificationGlueRewrite>true</VerificationGlueRewrite>
</PropertyGroup>
```

The generator then writes a `{Name}__Verified` copy of each eligible test, and the runner calls the copy. An eligible test is in a `partial` class and is not virtual or generic. [Tiers](tiers.md) lists the rules, and [Generated test registrations](generated/test-registrations.md) shows the call.

## IDE setup

Rider 2026.2 discovers, runs, opens and debugs the tests on the MTP 3 preview. Turn on Testing Platform support under Settings > Build, Execution, Deployment > Unit Testing > Testing Platform. Rider ships with it turned off.

Visual Studio and VS Code with C# Dev Kit have not been checked on the MTP 3 preview yet. In both, `dotnet test` and the test executable run the tests. Test Explorer in Visual Studio and the Testing view in VS Code are not supported until they pass the checks in [the IDE report](../research/ide-report.md). To debug a test there, leave `VerificationGlueRewrite` unset, so the runner calls your method and not its copy.

## Next

- [Run tests under Native AOT](run-tests-under-native-aot.md)
- [Generated test registrations](generated/test-registrations.md)
