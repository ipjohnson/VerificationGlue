# Parameters and where they come from

At the end of this page you can write tests and test classes that take services, mocks, settings and built-in values as parameters, and read the message of a parameter that has no value.

## The order

The runner supplies each parameter of a test method, and each parameter of its class's constructor, from the first source in this list that has a value:

1. The value from the test's [data row](data-rows.md), for a method parameter that a `[Row]`, a `[RowSource]` member or an `ITestDataSource` gives a value.
2. A parameter-level provider: `[Mock]`, which creates `Mock.Of<T>()`, or an attribute on the parameter that implements `ITestParameterProvider`.
3. Attributes that implement `ITestParameterProvider` on the method, then the class, then the assembly.
4. The test's [container](containers-and-scopes.md).
5. The built-in values: `CancellationToken`, `TestContext` and `ITestOutput`.
6. The parameter's default value.

This test takes a value from each source:

<!-- snippet: tests-parameter-sources -->
<a id='snippet-tests-parameter-sources'></a>
```cs
[Container<ShopBootstrap>]
[Setting("eu")]
public partial class ParameterSourceTests
{
    [VerifyTest]
    public void Takes_a_value_from_each_source(
        string region,
        [Setting("eur")] string currency,
        [Mock] IClock clock,
        OrderService sut,
        TestContext context,
        int quantity = 3)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var order = sut.Create(quantity, price: 4);

        Verify(region == "eu");
        Verify(currency == "eur");
        Verify(order.Created == clock.Now);
        Verify(order.Total == 12);
        Verify(context.Uid.StartsWith("TestsContainers.ParameterSourceTests.", StringComparison.Ordinal));
    }
}
```
<sup><a href='/samples/TestsContainers/ParameterTests.cs#L20-L45' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-parameter-sources' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `region` comes from `[Setting("eu")]` on the class.
- `currency` comes from `[Setting("eur")]` on the parameter, which comes before the class's provider.
- `clock` is a mock. The runner also registers it in the container, so `sut` receives it.
- `sut` comes from the container.
- `context` is a built-in value.
- `quantity` has no other source, so it gets its default value, 3.

## Write a provider

An attribute that implements `ITestParameterProvider` supplies parameters. It returns `false` for a parameter it does not supply, and the runner tries the next source:

<!-- snippet: tests-parameter-provider -->
<a id='snippet-tests-parameter-provider'></a>
```cs
/// <summary>Supplies the string parameters of the tests, or of the parameter, it is placed on.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter)]
public sealed class SettingAttribute(string value) : Attribute, ITestParameterProvider
{
    public bool TryProvide(TestContext context, TestParameter parameter, out object? provided)
    {
        provided = parameter.Type == typeof(string) ? value : null;
        return provided is not null;
    }
}
```
<sup><a href='/samples/TestsContainers/ParameterTests.cs#L7-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-parameter-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`TestParameter` gives the parameter's name, type, position, attributes and default value, and whether it is a `[Mock]` parameter or a constructor parameter.

## Constructor parameters

A test class can take parameters in its constructor. The runner uses the accessible constructor with the most parameters and supplies each one in the same order as test parameters:

<!-- snippet: tests-constructor-parameters -->
<a id='snippet-tests-constructor-parameters'></a>
```cs
[Container<ShopBootstrap>]
public sealed partial class CheckoutTests([Mock] IClock clock, OrderService sut)
{
    [VerifyTest]
    public void Constructor_and_method_share_one_mock([Mock] IClock sameClock)
    {
        sameClock.Now.Returns(new DateTime(2026, 11, 17));

        Verify(ReferenceEquals(clock, sameClock));
        Verify(sut.Create(quantity: 1, price: 1).Created == new DateTime(2026, 11, 17));
    }
}
```
<sup><a href='/samples/TestsContainers/ParameterTests.cs#L47-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-tests-constructor-parameters' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`[Mock]` parameters of the same type and key share one mock within a test, so the constructor, the method and the container all see the same instance.

When the container itself returns the test class, the runner uses that instance and leaves its disposal to the container.

## Built-in values

- `CancellationToken` is cancelled when the run is cancelled.
- `TestContext` holds the test ID, its attributes, its scope and its container.
- `ITestOutput` writes lines to the test's result. TRX reports show them as standard output.

## When no source has a value

The test fails, and the message names the parameter and every source the runner tried. A test without a container that takes an `IClock` fails with:

```text
Test parameter 'clock' of type Shop.IClock has no value. The providers tried were: the container (none, because no [Container<T>] applies), built-in values (CancellationToken, TestContext, ITestOutput). It has no default value.
```

`test/VerificationGlue.Tests.Specs/ContainerSpecs.cs` checks this message and the order above.

## Next

- [Containers and scopes](containers-and-scopes.md)
- [Extending VerificationGlue.Tests](extending.md)
