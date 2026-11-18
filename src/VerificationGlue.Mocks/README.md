![VerificationGlue](https://raw.githubusercontent.com/ipjohnson/VerificationGlue/main/assets/logo/lockup.png)

# VerificationGlue.Mocks

Source-generated mocks for .NET tests, with NSubstitute-style setup and a typed setup surface. The generator writes each mock at build time, so mocks work under Native AOT and trimming. They work in xunit, NUnit, MSTest, TUnit and plain application code. The package is a preview, released as 0.x.

## Install

<!-- snippet: install-verificationglue-mocks -->
<a id='snippet-install-verificationglue-mocks'></a>
```csproj
<ItemGroup>
  <PackageReference Include="VerificationGlue.Mocks" Version="$(VerificationGlueVersion)" />
</ItemGroup>
```
<sup><a href='/samples/MocksAot/MocksAot.csproj#L16-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-install-verificationglue-mocks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Use

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

The typed surface checks each setup at compile time:

<!-- snippet: mocks-style-typed -->
<a id='snippet-mocks-style-typed'></a>
```cs
var calc = Mock.Of<ICalculator>();
calc.Setup().Add(1, Arg<int>.Any()).Returns(3);
calc.Setup().Divide(Arg<int>.Any(), 0).Throws(new DivideByZeroException());
calc.Setup().Mode.Returns(Mode.Scientific);

var sum = calc.Add(1, 2);

Verify(calc.Calls().Add(1, 2) == 1);
Verify(calc.Calls().Reset() == 0);
```
<sup><a href='/samples/MocksGuide/SetupStyles.cs#L31-L41' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-style-typed' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Mocks as test parameters

`Mock.Of<T>()` needs no hook. A test parameter marked `[Mock]` needs a short hook for its framework. Copy the one you need into the test project. CI runs each hook in a test project under `samples/`.

### xunit v3

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

Name the hook's attribute in the project file, so the generator makes mocks for the parameters it marks:

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

## Docs

- [Getting started with mocks](../../docs/guide/getting-started-mocks.md)
- [Choose a setup style](../../docs/guide/choose-a-setup-style.md)
- [Migrate from NSubstitute](../../docs/guide/migrate-from-nsubstitute.md)
- [Known differences from NSubstitute](../../docs/guide/nsubstitute-differences.md)
