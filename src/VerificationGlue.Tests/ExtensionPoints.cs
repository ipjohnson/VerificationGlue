using System;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>
/// Supplies test parameters. An attribute that implements it supplies the parameter it is placed on,
/// or, on a method, class or assembly, every parameter of those tests.
/// </summary>
public interface ITestParameterProvider
{
    /// <summary>Supplies a value for <paramref name="parameter"/>, or returns false to pass it to the next provider.</summary>
    /// <param name="context">The running test.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="value">The value.</param>
    bool TryProvide(TestContext context, TestParameter parameter, out object? value);
}

/// <summary>Runs around each test, after its container is built and before its class is created.</summary>
public interface ITestMiddleware
{
    /// <summary>Runs the test by calling <paramref name="next"/>.</summary>
    /// <param name="context">The running test.</param>
    /// <param name="next">Creates the class, resolves the parameters, runs the test and disposes an instance the runner created.</param>
    ValueTask InvokeAsync(TestContext context, Func<ValueTask> next);
}

/// <summary>
/// Is told when a scope is created and when it is about to be disposed. An attribute that implements it
/// is told about the scopes of the tests it applies to: an assembly attribute about the assembly scope,
/// each class scope and each test scope.
/// </summary>
public interface ITestScopeListener
{
    /// <summary>Called after the scope is created. A test scope's overrides can still be added here.</summary>
    /// <param name="scope">The scope.</param>
    ValueTask OnCreatedAsync(TestScope scope);

    /// <summary>Called before the scope's containers are disposed.</summary>
    /// <param name="scope">The scope.</param>
    ValueTask OnDisposingAsync(TestScope scope);
}

/// <summary>Writes to a test's output, which is reported with the test's result.</summary>
public interface ITestOutput
{
    /// <summary>Writes a line.</summary>
    /// <param name="message">The line.</param>
    void WriteLine(string message);
}

/// <summary>The base of <see cref="UseAttribute{T}"/>.</summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public abstract class UseAttribute : Attribute
{
    private protected UseAttribute()
    {
    }

    internal abstract ITestMiddleware Create();
}

/// <summary>
/// Runs <typeparamref name="T"/> around each test. A new instance runs for each test. Middleware
/// from the assembly runs outside middleware from the class, which runs outside middleware from the method.
/// </summary>
/// <typeparam name="T">The middleware.</typeparam>
public sealed class UseAttribute<T> : UseAttribute
    where T : ITestMiddleware, new()
{
    internal override ITestMiddleware Create() => new T();
}
