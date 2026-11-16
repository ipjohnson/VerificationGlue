using Contracts;
using Glue.Mocks;

// The whole TUnit integration.
public sealed class MockDataAttribute : UntypedDataSourceGeneratorAttribute
{
    protected override IEnumerable<Func<object?[]?>> GenerateDataSources(DataGeneratorMetadata dataGeneratorMetadata)
    {
        yield return () => dataGeneratorMetadata.MembersToGenerate.OfType<ParameterMetadata>().Select(p => (object?)MockRegistry.Create(p.Type)).ToArray();
    }
}

public class Tests
{
    [Test]
    public async Task SubstituteFor_needs_no_integration()
    {
        var calc = Substitute.For<ICalculator>();
        calc.Add(1, 2).Returns(3);
        await Assert.That(calc.Add(1, 2)).IsEqualTo(3);
        calc.Received(1).Add(1, 2);
    }

    [Test, MockData]
    public async Task Mock_parameter([Mock] ICalculator calc, [Mock] IRepository<Order> orders)
    {
        orders.Find(7).Returns(new Order(7, "sku"));
        calc.Add(7, 100).Returns(107);
        await Assert.That(new OrderService(calc, orders).Total(7)).IsEqualTo(107);
    }
}
