using System.Reflection;
using System.Runtime.CompilerServices;
using VerificationGlue.Mocks;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace MocksInXunit;

#region mocks-hook-xunit
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
#endregion
