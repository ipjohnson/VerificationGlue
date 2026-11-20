using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>Helpers that generated test registrations call.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestRuntime
{
    private const string BuiltIns = "built-in values (CancellationToken, TestContext, ITestOutput)";

    /// <summary>The value of the test method's parameter at <paramref name="position"/>.</summary>
    /// <param name="context">The running test.</param>
    /// <param name="position">The zero-based position.</param>
    public static object? Argument(TestContext context, int position) => Resolve(context, context.Parameters[position]);

    /// <summary>The value of the test class constructor's parameter at <paramref name="position"/>.</summary>
    /// <param name="context">The running test.</param>
    /// <param name="position">The zero-based position.</param>
    public static object? ConstructorArgument(TestContext context, int position) => Resolve(context, context.ConstructorParameters[position]);

    /// <summary>The failure for a test class the runner cannot create.</summary>
    /// <param name="type">The test class.</param>
    public static InvalidOperationException NoConstructor(string type) =>
        new($"The runner cannot create {type}. A test class needs a public constructor without ref, out or in parameters, or the test method must be static.");

    /// <summary>
    /// Supplies a parameter from, in order: the data row, the <c>[Mock]</c> factory or a provider
    /// attribute on the parameter, provider attributes of the method, the class and the assembly, the
    /// container, the built-in values, and the default value.
    /// </summary>
    internal static object? Resolve(TestContext context, TestParameter parameter)
    {
        var row = context.Test.Arguments;
        if (!parameter.IsConstructorParameter && parameter.Position < row.Count)
        {
            return row[parameter.Position];
        }

        var tried = new List<string>();
        if (context.TryGetMock(parameter, out var mock))
        {
            return mock;
        }

        foreach (var attribute in parameter.Attributes)
        {
            if (attribute is ITestParameterProvider provider)
            {
                tried.Add(provider.GetType().Name + " on the parameter");
                if (TryProvide(provider, context, parameter, tried[^1], out var value))
                {
                    return value;
                }
            }
        }

        var attributes = context.Attributes;
        for (var i = attributes.Count - 1; i >= 0; i--)
        {
            if (attributes[i] is ITestParameterProvider provider)
            {
                tried.Add(provider.GetType().Name);
                if (TryProvide(provider, context, parameter, tried[^1], out var value))
                {
                    return value;
                }
            }
        }

        if (context.Container is { } container)
        {
            tried.Add("the container (" + container.GetType().Name + ")");
            bool resolved;
            object? value;
            try
            {
                resolved = container.TryResolve(parameter, out value);
            }
            catch (Exception failure)
            {
                throw SourceThrew(parameter, tried[^1], failure);
            }

            if (resolved)
            {
                return value;
            }
        }
        else
        {
            tried.Add("the container (none, because no [Container<T>] applies)");
        }

        tried.Add(BuiltIns);
        if (parameter.Type == typeof(CancellationToken))
        {
            return context.CancellationToken;
        }

        if (parameter.Type == typeof(TestContext))
        {
            return context;
        }

        if (parameter.Type == typeof(ITestOutput))
        {
            return context.Output;
        }

        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue;
        }

        throw NoValue(parameter, tried);
    }

    internal static InvalidOperationException NoValue(TestParameter parameter, IReadOnlyList<string> tried) =>
        new($"{Describe(parameter)} has no value. " +
            $"The providers tried were: {string.Join(", ", tried)}. It has no default value.");

    private static bool TryProvide(ITestParameterProvider provider, TestContext context, TestParameter parameter, string source, out object? value)
    {
        try
        {
            return provider.TryProvide(context, parameter, out value);
        }
        catch (Exception failure)
        {
            throw SourceThrew(parameter, source, failure);
        }
    }

    private static InvalidOperationException SourceThrew(TestParameter parameter, string source, Exception failure) =>
        new($"{Describe(parameter)} has no value, because {source} threw {failure.GetType().Name}: {failure.Message}", failure);

    private static string Describe(TestParameter parameter) =>
        $"{(parameter.IsConstructorParameter ? "Constructor" : "Test")} parameter '{parameter.Name}' of type {parameter.TypeName}";

    /// <summary>Disposes an instance that the runner created.</summary>
    internal static ValueTask DisposeAsync(object instance)
    {
        if (instance is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        (instance as IDisposable)?.Dispose();
        return default;
    }
}
