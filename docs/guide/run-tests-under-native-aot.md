# Run tests under Native AOT

At the end of this page you can publish a `VerificationGlue.Tests` project with Native AOT and run its tests from the published binary.

The runner uses no reflection. The generator registers each test from a module initializer, creates the attributes of a test with `new`, and calls the test method directly. `Verify` messages and `Mock.Of<T>()` mocks are generated too. So the published binary runs the same tests as `dotnet test`.

## Publish

Set `PublishAot` in the test project, and set `OutputType` next to it:

<!-- snippet: publish-tests-with-native-aot -->
<a id='snippet-publish-tests-with-native-aot'></a>
```csproj
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```
<sup><a href='/samples/PackageConsumer/PackageConsumer.csproj#L13-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-publish-tests-with-native-aot' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The package sets `OutputType` to `Exe` too, but `dotnet publish` restores the project before the package's props are imported. Without `OutputType` in the project file, that restore has no runtime identifier, and the publish fails with NETSDK1047.

Then publish:

```sh
dotnet publish tests/Shop.Tests -c Release -o artifacts/tests
```

## Run

The published binary is the test application. It takes the same options as `dotnet run`:

```sh
artifacts/tests/Shop.Tests
artifacts/tests/Shop.Tests --list-tests
artifacts/tests/Shop.Tests --filter-uid "Shop.CartTests.Total_adds_the_line_prices()"
artifacts/tests/Shop.Tests --report-trx --results-directory artifacts/results
```

The exit code is 0 when every test passes and 2 when a test fails.

## What CI checks

`eng/pack-and-consume.sh` publishes `samples/PackageConsumer` with Native AOT and warnings as errors, against the packed packages. Its tests cover an awaited test, a `CancellationToken` and a `TestContext` parameter, attributes created for the test, a mock and a `Verify` message. The script runs the binary, lists the tests, runs one by ID and checks the TRX report. CI adds the time of a full run to the job summary, next to the time of the same tests on the JIT.

<!-- snippet: tests-under-native-aot -->
<a id='snippet-tests-under-native-aot'></a>
```cs
[Area("orders")]
public partial class AotTests
{
    [VerifyTest]
    public void Runs_a_test()
    {
        var lines = new[] { 3, 4 };
        Verify(lines.Sum() == 7);
    }

    [VerifyTest]
    public async Task Awaits_a_test(CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        Verify(!cancellationToken.IsCancellationRequested);
    }

    [VerifyTest]
    [Area("checkout")]
    public void Creates_attributes(TestContext context) =>
        Verify(string.Join(",", context.Attributes.OfType<AreaAttribute>().Select(area => area.Name)) == "orders,checkout");

    [VerifyTest]
    public void Uses_a_mock()
    {
        var clock = Mock.Of<IClock>();
        clock.Now.Returns(new DateTime(2026, 11, 17));
        Verify(clock.Now.Day == 17);
    }

    [VerifyTest]
    public void Reports_a_message()
    {
        var total = 40;
        var failure = Throws<VerifyException>(() => Verify(total == 30));
        Verify(failure.Message.StartsWith("Verify failed: total == 30\n  expected total to equal 30\n", StringComparison.Ordinal));
    }
}
```
<sup><a href='/samples/PackageConsumer/AotTests.cs#L21-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-under-native-aot' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
