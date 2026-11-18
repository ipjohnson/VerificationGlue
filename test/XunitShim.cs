using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit.Sdk;
using Xunit.v3;

// A .Specs project built with -p:SpecsRunner=xunit compiles this file in place of the VerificationGlue.Tests
// attributes, so the conformance suites also run on xunit v3. Every [VerifyTest] method is an xunit theory. A
// method without [Row] or [RowSource] gets its one row from [VerifyTest]. A parameter after the row's values
// must be a CancellationToken, and it gets TestContext.Current.CancellationToken, as VerificationGlue.Tests
// passes the test's token. The rows are read when the test runs, because a token cannot be serialized.
namespace VerificationGlue.Tests;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class VerifyTestAttribute(
    [CallerFilePath] string? sourceFilePath = null,
    [CallerLineNumber] int sourceLineNumber = -1) : TheoryAttribute(sourceFilePath, sourceLineNumber), IDataAttribute
{
    ValueTask<IReadOnlyCollection<ITheoryDataRow>> IDataAttribute.GetData(MethodInfo testMethod, DisposalTracker disposalTracker) =>
        new(testMethod.IsDefined(typeof(RowAttribute)) || testMethod.IsDefined(typeof(RowSourceAttribute))
            ? []
            : [XunitShim.Row(testMethod, [])]);

    bool IDataAttribute.SupportsDiscoveryEnumeration() => false;

    bool IDataAttribute.DisableParallelization => false;

    bool? IDataAttribute.Explicit => null;

    string? IDataAttribute.Label => null;

    string? IDataAttribute.Skip => null;

    Type? IDataAttribute.SkipType => null;

    string? IDataAttribute.SkipUnless => null;

    string? IDataAttribute.SkipWhen => null;

    string? IDataAttribute.TestDisplayName => null;

    int? IDataAttribute.Timeout => null;

    string[]? IDataAttribute.Traits => null;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RowAttribute(params object?[]? values) : DataAttribute
{
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker) =>
        new([XunitShim.Row(testMethod, values ?? [null])]);

    public override bool SupportsDiscoveryEnumeration() => false;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RowSourceAttribute(string memberName) : DataAttribute
{
    public bool DeterministicReplay { get; set; }

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        var type = testMethod.ReflectedType!;
        var rows = type.GetMethod(memberName, Static, Type.EmptyTypes)?.Invoke(null, null)
            ?? type.GetProperty(memberName, Static)?.GetValue(null)
            ?? type.GetField(memberName, Static)?.GetValue(null)
            ?? throw new InvalidOperationException($"{type.Name}.{memberName} is not a static member with rows.");

        var result = new List<ITheoryDataRow>();
        foreach (var row in (IEnumerable)rows)
        {
            object?[] values = row is ITuple tuple
                ? [.. Enumerable.Range(0, tuple.Length).Select(index => tuple[index])]
                : [row];
            result.Add(XunitShim.Row(testMethod, values));
        }

        return new(result);
    }

    public override bool SupportsDiscoveryEnumeration() => false;
}

internal static class XunitShim
{
    public static ITheoryDataRow Row(MethodInfo testMethod, object?[] values)
    {
        var parameters = testMethod.GetParameters();
        var arguments = new object?[Math.Max(parameters.Length, values.Length)];
        values.CopyTo(arguments, 0);
        for (var index = values.Length; index < parameters.Length; index++)
        {
            arguments[index] = parameters[index].ParameterType == typeof(CancellationToken)
                ? TestContext.Current.CancellationToken
                : throw new InvalidOperationException(
                    $"{testMethod.Name} takes {parameters[index].Name}, which the xunit shim cannot give. Only a CancellationToken may follow the row's values.");
        }

        return new TheoryDataRow(arguments);
    }
}
