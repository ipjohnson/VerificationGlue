using System.Reflection;
using Contracts;
using Glue.Mocks;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

// The whole xunit v3 integration.
public sealed class MockDataAttribute : DataAttribute
{
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var arguments = testMethod
            .GetParameters()
            .Select(p => p.IsDefined(typeof(MockAttribute)) ? MockRegistry.Create(p.ParameterType) : null)
            .ToArray();

        return new([new TheoryDataRow(arguments)]);
    }

    public override bool SupportsDiscoveryEnumeration() => false;
}

public class Tests
{
    [Fact]
    public void SubstituteFor_needs_no_integration()
    {
        var calc = Substitute.For<ICalculator>();
        calc.Add(1, 2).Returns(3);
        Assert.Equal(3, calc.Add(1, 2));
        calc.Received(1).Add(1, 2);
    }

    [Theory, MockData]
    public void Mock_parameter([Mock] ICalculator calc, [Mock] IRepository<Order> orders)
    {
        orders.Find(7).Returns(new Order(7, "sku"));
        calc.Add(7, 100).Returns(107);
        Assert.Equal(107, new OrderService(calc, orders).Total(7));
    }
}
