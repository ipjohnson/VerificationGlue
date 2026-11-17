using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerificationGlue.Mocks;

namespace MocksInMSTest;

#region mocks-hook-mstest
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
#endregion
