using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>
/// Runs the selected tests of one request through their assembly, class and test scopes, in the order
/// docs/design/tests.md gives under "Scopes".
/// </summary>
internal sealed class TestRunner
{
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<Type, int> _remaining = [];
    private readonly Dictionary<Type, TestScope> _classes = [];
    private readonly List<TestScope> _openClasses = [];
    private readonly ForwardingOutput _sharedOutput = new();
    private TestScope? _assembly;

    public TestRunner(IEnumerable<TestDescriptor> selected, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        foreach (var test in selected)
        {
            _remaining[test.TestClass] = _remaining.GetValueOrDefault(test.TestClass) + 1;
        }
    }

    /// <summary>Runs one test and returns every failure in the order it happened, with the test's output.</summary>
    public async Task<TestOutcome> RunAsync(TestDescriptor test)
    {
        var output = new TestOutput();
        _sharedOutput.Target = output;
        var failures = new List<Exception>();
        var checks = VerifyScope.Begin();
        TestScope? scope = null;
        ITestContainer? container = null;
        try
        {
            var classScope = await ClassScopeAsync(test);
            scope = new TestScope(
                TestScopeKind.Test,
                test.Uid,
                classScope,
                [.. TestRegistry.CreateAssemblyAttributes(), .. test.CreateClassAttributes(), .. test.CreateMethodAttributes()],
                output,
                _cancellationToken);
            var context = new TestContext(test, scope, _cancellationToken);
            await NotifyCreatedAsync(scope);
            AddMocks(context);
            scope.CloseOverrides();
            _cancellationToken.ThrowIfCancellationRequested();
            container = await BuildContainerAsync(context, classScope);
            context.Container = container;
            await RunMiddlewareAsync(context, failures);
        }
        catch (Exception failure)
        {
            if (!failures.Contains(failure))
            {
                failures.Add(failure);
            }
        }

        if (scope is not null)
        {
            await NotifyDisposingAsync(scope, failures);
        }

        if (container is not null)
        {
            await DisposeAsync(container, failures);
        }

        try
        {
            checks.Dispose();
        }
        catch (VerifyException failure)
        {
            failures.Add(failure);
        }

        _sharedOutput.Target = null;
        return new TestOutcome(failures, output.ToString());
    }

    /// <summary>Disposes the test's class scope when <paramref name="test"/> was its last selected test.</summary>
    public async Task<ScopeFailure?> CompleteAsync(TestDescriptor test)
    {
        _remaining[test.TestClass]--;
        if (_remaining[test.TestClass] > 0 || !_classes.Remove(test.TestClass, out var scope))
        {
            return null;
        }

        _openClasses.Remove(scope);
        return await DisposeScopeAsync(scope);
    }

    /// <summary>Disposes the class scopes still open, newest first, and then the assembly scope.</summary>
    public async Task<IReadOnlyList<ScopeFailure>> DisposeAsync()
    {
        var failures = new List<ScopeFailure>();
        for (var i = _openClasses.Count - 1; i >= 0; i--)
        {
            if (await DisposeScopeAsync(_openClasses[i]) is { } failure)
            {
                failures.Add(failure);
            }
        }

        _openClasses.Clear();
        _classes.Clear();
        if (_assembly is not null && await DisposeScopeAsync(_assembly) is { } assemblyFailure)
        {
            failures.Add(assemblyFailure);
        }

        _assembly = null;
        return failures;
    }

    private async ValueTask<TestScope> ClassScopeAsync(TestDescriptor test)
    {
        _assembly ??= await CreateSharedScopeAsync(
            TestScopeKind.Assembly,
            test.TestClass.Assembly.GetName().Name ?? "",
            null,
            static _ => TestRegistry.CreateAssemblyAttributes(),
            test);
        if (_assembly.Failure is { } assemblyFailure)
        {
            throw ScopeFailed(_assembly, "could not be created", assemblyFailure);
        }

        if (!_classes.TryGetValue(test.TestClass, out var scope))
        {
            scope = await CreateSharedScopeAsync(
                TestScopeKind.Class,
                test.FullTypeName,
                _assembly,
                static test => [.. TestRegistry.CreateAssemblyAttributes(), .. test.CreateClassAttributes()],
                test);
            _classes.Add(test.TestClass, scope);
            _openClasses.Add(scope);
        }

        if (scope.Failure is { } classFailure)
        {
            throw ScopeFailed(scope, "could not be created", classFailure);
        }

        return scope;
    }

    private async ValueTask<TestScope> CreateSharedScopeAsync(
        TestScopeKind kind,
        string uid,
        TestScope? parent,
        Func<TestDescriptor, IReadOnlyList<Attribute>> createAttributes,
        TestDescriptor test)
    {
        IReadOnlyList<Attribute> attributes;
        try
        {
            attributes = createAttributes(test);
        }
        catch (Exception failure)
        {
            return new TestScope(kind, uid, parent, [], _sharedOutput, _cancellationToken) { Failure = failure };
        }

        var scope = new TestScope(kind, uid, parent, attributes, _sharedOutput, _cancellationToken);
        try
        {
            await NotifyCreatedAsync(scope);
        }
        catch (Exception failure)
        {
            scope.Failure = failure;
        }

        return scope;
    }

    private static async ValueTask NotifyCreatedAsync(TestScope scope)
    {
        foreach (var attribute in scope.Attributes)
        {
            if (attribute is ITestScopeListener listener)
            {
                await listener.OnCreatedAsync(scope);
                scope.Listeners.Add(listener);
            }
        }
    }

    private static async ValueTask NotifyDisposingAsync(TestScope scope, List<Exception> failures)
    {
        for (var i = scope.Listeners.Count - 1; i >= 0; i--)
        {
            try
            {
                await scope.Listeners[i].OnDisposingAsync(scope);
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }
    }

    private static async ValueTask DisposeAsync(IAsyncDisposable disposable, List<Exception> failures)
    {
        try
        {
            await disposable.DisposeAsync();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
    }

    private static async Task<ScopeFailure?> DisposeScopeAsync(TestScope scope)
    {
        var failures = new List<Exception>();
        await NotifyDisposingAsync(scope, failures);
        for (var i = scope.Containers.Count - 1; i >= 0; i--)
        {
            if (scope.Containers[i].Container is { } container)
            {
                await DisposeAsync(container, failures);
            }
        }

        return failures.Count == 0 ? null : new ScopeFailure(scope, failures);
    }

    /// <summary>
    /// Creates a mock for each <c>[Mock]</c> parameter and registers it as an override. Parameters of
    /// the same type and key share one mock, so the constructor, the method and the container agree.
    /// </summary>
    private static void AddMocks(TestContext context)
    {
        var created = new Dictionary<(Type, object?), object>();
        foreach (var parameter in context.ConstructorParameters.Concat(context.Parameters))
        {
            if (parameter.CreateMock is null)
            {
                continue;
            }

            var key = (parameter.Type, parameter.ServiceKey);
            if (!created.TryGetValue(key, out var mock))
            {
                mock = parameter.CreateMock();
                created.Add(key, mock);
                context.Scope.AddOverride(parameter.Type, mock, parameter.ServiceKey);
            }

            context.SetMock(parameter, mock);
        }
    }

    private async ValueTask<ITestContainer?> BuildContainerAsync(TestContext context, TestScope classScope)
    {
        ContainerAttribute? binding = null;
        foreach (var attribute in context.Attributes)
        {
            if (attribute is ContainerAttribute candidate)
            {
                binding = candidate;
            }
        }

        if (binding is null)
        {
            return null;
        }

        var scope = context.Scope;
        TestParameter[] parameters = [.. context.ConstructorParameters, .. context.Parameters];
        if (binding.Scope == ContainerScope.Test)
        {
            return await binding.BuildAsync(new TestContainerContext(scope, parameters, scope.Overrides, null, scope.Output, _cancellationToken));
        }

        if (scope.Overrides.Count > 0 && !binding.SupportsChildOverrides)
        {
            throw new InvalidOperationException(
                $"VGT0008: The test has overrides for {string.Join(", ", scope.Overrides.Select(entry => entry.ServiceType.Name))}, " +
                $"but its container {binding.Bootstrap.Name} is shared with Scope = {binding.Scope} and its bootstrap does not implement " +
                "ISupportsChildOverrides, so the child container would not return them. Use Scope = Test, or a bootstrap that implements ISupportsChildOverrides.");
        }

        var owner = binding.Scope == ContainerScope.Class ? classScope : classScope.Parent!;
        var parent = await SharedContainerAsync(owner, binding);
        return await parent.CreateChildAsync(new TestContainerContext(scope, parameters, scope.Overrides, parent, scope.Output, _cancellationToken));
    }

    private async ValueTask<ITestContainer> SharedContainerAsync(TestScope scope, ContainerAttribute binding)
    {
        var entry = scope.Containers.Find(candidate => candidate.Bootstrap == binding.Bootstrap);
        if (entry is null)
        {
            entry = new SharedContainer(binding.Bootstrap);
            scope.Containers.Add(entry);
            try
            {
                entry.Container = await binding.BuildAsync(new TestContainerContext(scope, [], [], null, scope.Output, _cancellationToken));
            }
            catch (Exception failure)
            {
                entry.Failure = failure;
            }
        }

        return entry.Container ?? throw ScopeFailed(scope, $"could not build its {binding.Bootstrap.Name} container", entry.Failure!);
    }

    private static async ValueTask RunMiddlewareAsync(TestContext context, List<Exception> failures)
    {
        var middleware = new List<ITestMiddleware>();
        foreach (var attribute in context.Attributes)
        {
            if (attribute is UseAttribute use)
            {
                middleware.Add(use.Create());
            }
        }

        Func<ValueTask> next = () => InvokeAsync(context, failures);
        for (var i = middleware.Count - 1; i >= 0; i--)
        {
            var current = middleware[i];
            var inner = next;
            next = () => current.InvokeAsync(context, inner);
        }

        await next();
    }

    /// <summary>
    /// Creates the class, runs the test and disposes an instance the runner created. Records each
    /// failure, then throws the first so middleware sees it.
    /// </summary>
    private static async ValueTask InvokeAsync(TestContext context, List<Exception> failures)
    {
        var test = context.Test;
        object? instance = null;
        var owned = false;
        Exception? first = null;
        try
        {
            if (test.CreateInstance is not null)
            {
                instance = context.Services?.GetService(test.TestClass);
                if (instance is null)
                {
                    instance = test.CreateInstance(context);
                    owned = true;
                }
            }

            await test.Invoke(context, instance);
        }
        catch (Exception failure)
        {
            failures.Add(failure);
            first = failure;
        }

        if (owned)
        {
            try
            {
                await TestRuntime.DisposeAsync(instance!);
            }
            catch (Exception failure)
            {
                failures.Add(failure);
                first ??= failure;
            }
        }

        if (first is not null)
        {
            ExceptionDispatchInfo.Throw(first);
        }
    }

    private static TestScopeException ScopeFailed(TestScope scope, string what, Exception failure) =>
        new($"The {scope.Kind.ToString().ToLowerInvariant()} scope {scope.Uid} {what}: {failure.Message}", failure);

    private sealed class ForwardingOutput : ITestOutput
    {
        public TestOutput? Target { get; set; }

        public void WriteLine(string message) => Target?.WriteLine(message);
    }
}

/// <summary>The failures of one test, in the order they happened, and its output.</summary>
internal sealed record TestOutcome(IReadOnlyList<Exception> Failures, string Output);

/// <summary>The failures of disposing a class or assembly scope.</summary>
internal sealed record ScopeFailure(TestScope Scope, IReadOnlyList<Exception> Failures);

/// <summary>A test failed because its class or assembly scope failed.</summary>
internal sealed class TestScopeException(string message, Exception inner) : Exception(message, inner);

/// <summary>Collects a test's output.</summary>
internal sealed class TestOutput : ITestOutput
{
    private readonly StringBuilder _text = new();

    public void WriteLine(string message)
    {
        lock (_text)
        {
            _text.Append(message).Append('\n');
        }
    }

    public override string ToString()
    {
        lock (_text)
        {
            return _text.ToString();
        }
    }
}

/// <summary>
/// More than one failure of one test. The message starts with the first failure and lists the rest in
/// the order they happened.
/// </summary>
internal sealed class TestFailuresException : Exception
{
    public TestFailuresException(IReadOnlyList<Exception> failures)
        : base(Compose(failures), failures[0])
    {
        Failures = failures;
    }

    public IReadOnlyList<Exception> Failures { get; }

    public override string? StackTrace =>
        string.Join("\n\n", Failures.Select(failure => failure.GetType().Name + ":\n" + failure.StackTrace));

    private static string Compose(IReadOnlyList<Exception> failures)
    {
        var text = new StringBuilder(failures[0].Message);
        text.Append("\n\n").Append(failures.Count - 1).Append(failures.Count == 2 ? " more failure after it:" : " more failures after it:");
        foreach (var failure in failures.Skip(1))
        {
            text.Append("\n\n").Append(failure.GetType().Name).Append(": ").Append(failure.Message);
        }

        return text.ToString();
    }
}
