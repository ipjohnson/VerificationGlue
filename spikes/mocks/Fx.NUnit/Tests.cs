using System.Collections;
using System.Runtime.CompilerServices;
using Contracts;
using Glue.Mocks;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Glue.Mocks.NUnit
{
    // The whole NUnit integration. NUnit asks parameter attributes for their own data.
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class MockAttribute : Glue.Mocks.MockAttribute, IParameterDataSource
    {
        public IEnumerable GetData(IParameterInfo parameter)
        {
            // NUnit calls this during discovery, before any code in the test assembly has run.
            RuntimeHelpers.RunModuleConstructor(parameter.ParameterInfo.Member.Module.ModuleHandle);
            yield return MockRegistry.Create(parameter.ParameterType);
        }
    }
}

namespace Fx.NUnit
{
    public class Tests
    {
        [Test]
        public void SubstituteFor_needs_no_integration()
        {
            var calc = Substitute.For<ICalculator>();
            calc.Add(1, 2).Returns(3);
            Assert.That(calc.Add(1, 2), Is.EqualTo(3));
            calc.Received(1).Add(1, 2);
        }

        [Test]
        public void Mock_parameter([Glue.Mocks.NUnit.Mock] ICalculator calc, [Glue.Mocks.NUnit.Mock] IRepository<Order> orders)
        {
            orders.Find(7).Returns(new Order(7, "sku"));
            calc.Add(7, 100).Returns(107);
            Assert.That(new OrderService(calc, orders).Total(7), Is.EqualTo(107));
        }
    }
}
