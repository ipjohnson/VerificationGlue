using System.Reflection;
using Contracts;
using Glue.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// The whole MSTest integration.
[AttributeUsage(AttributeTargets.Method)]
public sealed class MockDataAttribute : Attribute, ITestDataSource
{
    public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
    {
        yield return methodInfo
            .GetParameters()
            .Select(p => p.IsDefined(typeof(MockAttribute)) ? MockRegistry.Create(p.ParameterType) : null)
            .ToArray();
    }

    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data) => methodInfo.Name;
}

[TestClass]
public class Tests
{
    [TestMethod]
    public void SubstituteFor_needs_no_integration()
    {
        var calc = Substitute.For<ICalculator>();
        calc.Add(1, 2).Returns(3);
        Assert.AreEqual(3, calc.Add(1, 2));
        calc.Received(1).Add(1, 2);
    }

    [TestMethod, MockData]
    public void Mock_parameter([Mock] ICalculator calc, [Mock] IRepository<Order> orders)
    {
        orders.Find(7).Returns(new Order(7, "sku"));
        calc.Add(7, 100).Returns(107);
        Assert.AreEqual(107, new OrderService(calc, orders).Total(7));
    }
}
