# Getting started with mocks

At the end of this page you can create a mock with `Mock.Of<T>()`, set up its members and check its calls in xunit, NUnit, MSTest or TUnit. You can also pass mocks to test parameters marked `[Mock]` in each of those frameworks.

The mocks are generated at build time, so they work under Native AOT and trimming. The package is a preview, released as 0.x.

## Install

Add the `VerificationGlue.Mocks` package to the test project:

<!-- snippet: install-verificationglue-mocks -->
<a id='snippet-install-verificationglue-mocks'></a>
```csproj
<ItemGroup>
  <PackageReference Include="VerificationGlue.Mocks" Version="$(VerificationGlueVersion)" />
</ItemGroup>
```
<sup><a href='/samples/MocksAot/MocksAot.csproj#L16-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-install-verificationglue-mocks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The sample sets `VerificationGlueVersion` to the version it tests. A project writes the version number there.

The package adds a source generator and needs no other setup. Its `buildTransitive/VerificationGlue.Mocks.props` adds `VerificationGlue.Mocks.Generated` to `InterceptorsNamespaces`, which the generated code needs.

## Create a mock and set it up

`Mock.Of<T>()` returns a mock of the interface `T`. The generator writes the mock class when it sees the call, so there is nothing to register. Set up a member by calling it and then `Returns`, and check a call with `Received`:

<!-- snippet: mocks-in-xunit -->
<a id='snippet-mocks-in-xunit'></a>
```cs
[Fact]
public void Quote_adds_the_prices()
{
    var prices = Mock.Of<IPriceList>();
    prices.PriceOf("apple").Returns(3);
    prices.PriceOf("pear").Returns(4);

    var total = new Quote(prices).Total("apple", "pear");

    Assert.Equal(7, total);
    prices.Received(2).PriceOf(Arg.Any<string>());
}
```
<sup><a href='/samples/MocksInXunit/InvoiceTests.cs#L9-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-in-xunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An unconfigured member returns an empty string for `string`, a completed task for `Task` and `ValueTask`, and a mock for an interface outside the `System` namespaces, up to the [recursion depth](recursive-mocks.md). It returns `default` for the rest, including arrays and collections.

The setup above is the NSubstitute style. The package also generates a typed `Setup()` and `Calls()` surface for each mocked interface. [Choose a setup style](choose-a-setup-style.md) compares the two.

## Host frameworks

`Mock.Of<T>()` works the same in every test framework. A test parameter marked `[Mock]` needs a short hook that asks `MockRegistry.Create(Type)` for each mock. The package does not ship the hooks. Copy the one for your framework into the test project.

Each hook calls `RuntimeHelpers.RunModuleConstructor` first. The generated mocks register themselves in a module initializer, and a framework can ask for data before any code in the test assembly has run.

The hooks read the test method's parameters through reflection. Under Native AOT, use `Mock.Of<T>()` instead.

### xunit

The test in [Create a mock and set it up](#create-a-mock-and-set-it-up) is an xunit v3 test. The hook is a `DataAttribute`:

<!-- snippet: mocks-hook-xunit -->
<a id='snippet-mocks-hook-xunit'></a>
```cs
/// <summary>Passes a generated mock to each parameter marked [Mock].</summary>
public sealed class MockDataAttribute : DataAttribute
{
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        RuntimeHelpers.RunModuleConstructor(testMethod.Module.ModuleHandle);
        var arguments = testMethod.GetParameters()
            .Select(parameter => parameter.IsDefined(typeof(MockAttribute)) ? MockRegistry.Create(parameter.ParameterType) : null)
            .ToArray();
        return new([new TheoryDataRow(arguments)]);
    }

    public override bool SupportsDiscoveryEnumeration() => false;
}
```
<sup><a href='/samples/MocksInXunit/MockDataAttribute.cs#L10-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-hook-xunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: mocks-parameter-in-xunit -->
<a id='snippet-mocks-parameter-in-xunit'></a>
```cs
[Theory, MockData]
public void Bill_with_mock_parameters([Mock] IPriceList prices, [Mock] IReceipts receipts)
{
    prices.Setup().PriceOf(Arg<string>.Any()).Returns(5);

    var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

    Assert.Equal(10, total);
    Assert.True(receipts.Calls().Send("ana", 10) == 1);
}
```
<sup><a href='/samples/MocksInXunit/InvoiceTests.cs#L24-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-parameter-in-xunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### NUnit

<!-- snippet: mocks-in-nunit -->
<a id='snippet-mocks-in-nunit'></a>
```cs
[Test]
public void Quote_adds_the_prices()
{
    var prices = Mock.Of<IPriceList>();
    prices.PriceOf("apple").Returns(3);
    prices.PriceOf("pear").Returns(4);

    var total = new Quote(prices).Total("apple", "pear");

    Assert.That(total, Is.EqualTo(7));
    prices.Received(2).PriceOf(Arg.Any<string>());
}
```
<sup><a href='/samples/MocksInNUnit/InvoiceTests.cs#L9-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-in-nunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

NUnit asks each parameter attribute for its values, so the hook is the project's own `[Mock]` attribute:

<!-- snippet: mocks-hook-nunit -->
<a id='snippet-mocks-hook-nunit'></a>
```cs
/// <summary>Passes a generated mock to the parameter it marks.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class MockAttribute : Attribute, IParameterDataSource
{
    public IEnumerable GetData(IParameterInfo parameter)
    {
        RuntimeHelpers.RunModuleConstructor(parameter.ParameterInfo.Member.Module.ModuleHandle);
        yield return MockRegistry.Create(parameter.ParameterType);
    }
}
```
<sup><a href='/samples/MocksInNUnit/MockAttribute.cs#L8-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-hook-nunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The generator makes mocks for the parameters that `VerificationGlue.Mocks.MockAttribute` marks. Add the full name of the hook's attribute to the `VerificationGlueMockAttribute` item, so the generator makes mocks for its parameters too:

<!-- snippet: mocks-hook-nunit-item -->
<a id='snippet-mocks-hook-nunit-item'></a>
```csproj
<ItemGroup>
  <VerificationGlueMockAttribute Include="MocksInNUnit.MockAttribute" />
</ItemGroup>
```
<sup><a href='/samples/MocksInNUnit/MocksInNUnit.csproj#L12-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-hook-nunit-item' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: mocks-parameter-in-nunit -->
<a id='snippet-mocks-parameter-in-nunit'></a>
```cs
[Test]
public void Bill_with_mock_parameters([Mock] IPriceList prices, [Mock] IReceipts receipts)
{
    prices.Setup().PriceOf(Arg<string>.Any()).Returns(5);

    var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

    Assert.That(total, Is.EqualTo(10));
    Assert.That(receipts.Calls().Send("ana", 10) == 1);
}
```
<sup><a href='/samples/MocksInNUnit/InvoiceTests.cs#L24-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-parameter-in-nunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### MSTest

<!-- snippet: mocks-in-mstest -->
<a id='snippet-mocks-in-mstest'></a>
```cs
[TestMethod]
public void Quote_adds_the_prices()
{
    var prices = Mock.Of<IPriceList>();
    prices.PriceOf("apple").Returns(3);
    prices.PriceOf("pear").Returns(4);

    var total = new Quote(prices).Total("apple", "pear");

    Assert.AreEqual(7, total);
    prices.Received(2).PriceOf(Arg.Any<string>());
}
```
<sup><a href='/samples/MocksInMSTest/InvoiceTests.cs#L10-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-in-mstest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The hook is an `ITestDataSource`:

<!-- snippet: mocks-hook-mstest -->
<a id='snippet-mocks-hook-mstest'></a>
```cs
/// <summary>Passes a generated mock to each parameter marked [Mock].</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MockDataAttribute : Attribute, ITestDataSource
{
    public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
    {
        RuntimeHelpers.RunModuleConstructor(methodInfo.Module.ModuleHandle);
        yield return methodInfo.GetParameters()
            .Select(parameter => parameter.IsDefined(typeof(MockAttribute)) ? MockRegistry.Create(parameter.ParameterType) : null)
            .ToArray();
    }

    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data) => methodInfo.Name;
}
```
<sup><a href='/samples/MocksInMSTest/MockDataAttribute.cs#L8-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-hook-mstest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: mocks-parameter-in-mstest -->
<a id='snippet-mocks-parameter-in-mstest'></a>
```cs
[TestMethod, MockData]
public void Bill_with_mock_parameters([Mock] IPriceList prices, [Mock] IReceipts receipts)
{
    prices.Setup().PriceOf(Arg<string>.Any()).Returns(5);

    var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

    Assert.AreEqual(10, total);
    Assert.IsTrue(receipts.Calls().Send("ana", 10) == 1);
}
```
<sup><a href='/samples/MocksInMSTest/InvoiceTests.cs#L25-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-parameter-in-mstest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### TUnit

<!-- snippet: mocks-in-tunit -->
<a id='snippet-mocks-in-tunit'></a>
```cs
[Test]
public async Task Quote_adds_the_prices()
{
    var prices = Mock.Of<IPriceList>();
    prices.PriceOf("apple").Returns(3);
    prices.PriceOf("pear").Returns(4);

    var total = new Quote(prices).Total("apple", "pear");

    await Assert.That(total).IsEqualTo(7);
    prices.Received(2).PriceOf(Arg.Any<string>());
}
```
<sup><a href='/samples/MocksInTUnit/InvoiceTests.cs#L8-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-in-tunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The hook is an `UntypedDataSourceGeneratorAttribute`. It passes a mock to every parameter of the test:

<!-- snippet: mocks-hook-tunit -->
<a id='snippet-mocks-hook-tunit'></a>
```cs
/// <summary>Passes a generated mock to each parameter of the test.</summary>
public sealed class MockDataAttribute : UntypedDataSourceGeneratorAttribute
{
    protected override IEnumerable<Func<object?[]?>> GenerateDataSources(DataGeneratorMetadata dataGeneratorMetadata)
    {
        yield return () => dataGeneratorMetadata.MembersToGenerate
            .OfType<ParameterMetadata>()
            .Select(parameter => (object?)MockRegistry.Create(parameter.Type))
            .ToArray();
    }
}
```
<sup><a href='/samples/MocksInTUnit/MockDataAttribute.cs#L5-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-hook-tunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: mocks-parameter-in-tunit -->
<a id='snippet-mocks-parameter-in-tunit'></a>
```cs
[Test, MockData]
public async Task Bill_with_mock_parameters([Mock] IPriceList prices, [Mock] IReceipts receipts)
{
    prices.Setup().PriceOf(Arg<string>.Any()).Returns(5);

    var total = new Invoice(prices, receipts).Bill("ana", "apple", "pear");

    await Assert.That(total).IsEqualTo(10);
    await Assert.That(receipts.Calls().Send("ana", 10) == 1).IsTrue();
}
```
<sup><a href='/samples/MocksInTUnit/InvoiceTests.cs#L23-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-parameter-in-tunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### VerificationGlue.Tests

`Mock.Of<T>()` works in `VerificationGlue.Tests` too. Built-in `[Mock]` parameters arrive with its Phase 2, described in [`docs/design/tests.md`](../design/tests.md).

## Native AOT

`samples/MocksAot` uses each part of the NSubstitute-style setup in a console program. `eng/pack-and-consume.sh` publishes it with Native AOT and warnings as errors against the packed package, and runs it.

## Next

- [Choose a setup style](choose-a-setup-style.md)
- [Arguments and matchers](arguments-and-matchers.md)
- [Mock async members](mock-async-members.md)
- [Recursive mocks](recursive-mocks.md)
- [Mock a type from a generic helper](mock-a-type-from-a-generic-helper.md)
- [Migrate from NSubstitute](migrate-from-nsubstitute.md) and [Known differences from NSubstitute](nsubstitute-differences.md)
- [Generated mocks](generated/mocks.md)
- Diagnostics: [VGM0001](../diagnostics/VGM0001.md), [VGM0002](../diagnostics/VGM0002.md), [VGM0003](../diagnostics/VGM0003.md), [VGM0004](../diagnostics/VGM0004.md), [VGM0005](../diagnostics/VGM0005.md) and [VGM0007](../diagnostics/VGM0007.md)
