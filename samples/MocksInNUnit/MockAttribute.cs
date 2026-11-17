using System.Collections;
using System.Runtime.CompilerServices;
using NUnit.Framework.Interfaces;
using VerificationGlue.Mocks;

namespace MocksInNUnit;

#region mocks-hook-nunit
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
#endregion
