using VerificationGlue.Tests;

namespace Fixture;

/// <summary>
/// Appends lifecycle lines to the file that <c>VG_FIXTURE_LOG</c> names, so the specs can check the
/// order of scopes, containers and disposal across tests and processes. Does nothing when it is unset.
/// </summary>
public static class Log
{
    private static readonly string? s_path = Environment.GetEnvironmentVariable("VG_FIXTURE_LOG");

    public static string[] Lines => s_path is not null && File.Exists(s_path) ? File.ReadAllLines(s_path) : [];

    public static void Write(string line)
    {
        if (s_path is not null)
        {
            File.AppendAllText(s_path, line + "\n");
        }
    }
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.UtcNow;
}

public sealed class FixedClock : IClock
{
    public DateTime Now => new(2000, 1, 1);
}

public sealed class OrderService(IClock clock)
{
    public IClock Clock { get; } = clock;
}

public sealed class Database : IDisposable
{
    private static int s_next;

    public Database()
    {
        Id = Interlocked.Increment(ref s_next);
        Log.Write($"database {Id} created");
    }

    public int Id { get; }

    public void Dispose() => Log.Write($"database {Id} disposed");
}

/// <summary>
/// A container for the fixture. A child resolves each registration itself, unless it is shared, so
/// a child applies its own overrides to the services it creates.
/// </summary>
public sealed class FixtureContainer : ITestContainer, IServiceProvider
{
    private static readonly Dictionary<Type, (bool Shared, Func<FixtureContainer, object> Create)> s_registrations = new()
    {
        [typeof(IClock)] = (false, _ => new SystemClock()),
        [typeof(OrderService)] = (false, container => new OrderService((IClock)container.GetService(typeof(IClock))!)),
        [typeof(Database)] = (true, _ => new Database()),
        [typeof(ContainerOwnedTests)] = (false, container => new ContainerOwnedTests((Database)container.GetService(typeof(Database))!)),
    };

    private readonly string _name;
    private readonly FixtureContainer? _parent;
    private readonly IReadOnlyList<TestOverride> _overrides;
    private readonly Dictionary<Type, object> _instances = [];
    private readonly List<object> _owned = [];

    public FixtureContainer(string name, FixtureContainer? parent, IReadOnlyList<TestOverride> overrides)
    {
        _name = name;
        _parent = parent;
        _overrides = overrides;
        Log.Write($"container built: {name}");
    }

    public bool HangOnChild { get; init; }

    public bool ThrowOnDispose { get; init; }

    public IServiceProvider Services => this;

    public object? GetService(Type serviceType) => Resolve(serviceType, null);

    public bool TryResolve(TestParameter parameter, out object? value)
    {
        value = Resolve(parameter.Type, parameter.ServiceKey);
        return value is not null;
    }

    public async ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context)
    {
        if (HangOnChild)
        {
            Log.Write("child container waits");
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }

        return new FixtureContainer(context.Uid, this, context.Overrides);
    }

    public ValueTask DisposeAsync()
    {
        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            (_owned[i] as IDisposable)?.Dispose();
        }

        Log.Write($"container disposed: {_name}");
        return ThrowOnDispose ? throw new InvalidOperationException("The container failed to dispose.") : default;
    }

    private object? Resolve(Type type, object? key)
    {
        for (var i = _overrides.Count - 1; i >= 0; i--)
        {
            if (_overrides[i].ServiceType == type && Equals(_overrides[i].Key, key))
            {
                return _overrides[i].Instance;
            }
        }

        if (key is not null || !s_registrations.TryGetValue(type, out var registration))
        {
            return _parent?.Resolve(type, key);
        }

        if (registration.Shared && _parent is not null)
        {
            return _parent.Resolve(type, key);
        }

        if (!_instances.TryGetValue(type, out var instance))
        {
            instance = registration.Create(this);
            _instances.Add(type, instance);
            _owned.Add(instance);
        }

        return instance;
    }
}

public sealed class FixtureBootstrap : ITestContainerBootstrap, ISupportsChildOverrides
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context)
    {
        context.Output.WriteLine($"building the {context.Scope.Kind} container");
        return new(new FixtureContainer(context.Uid, null, context.Overrides));
    }
}

/// <summary>A bootstrap without <see cref="ISupportsChildOverrides"/>, like one built on Microsoft.Extensions.DependencyInjection.</summary>
public sealed class PlainBootstrap : ITestContainerBootstrap
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context) =>
        new(new FixtureContainer(context.Uid, null, context.Overrides));
}

/// <summary>Builds a class container whose children wait for cancellation when <c>VG_FIXTURE_HANG</c> is set.</summary>
public sealed class HangingChildBootstrap : ITestContainerBootstrap, ISupportsChildOverrides
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context) =>
        new(new FixtureContainer(context.Uid, null, context.Overrides) { HangOnChild = Environment.GetEnvironmentVariable("VG_FIXTURE_HANG") is not null });
}

public sealed class ThrowingDisposeBootstrap : ITestContainerBootstrap
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context) =>
        new(new FixtureContainer(context.Uid, null, context.Overrides) { ThrowOnDispose = true });
}

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class LogScopesAttribute : Attribute, ITestScopeListener
{
    public ValueTask OnCreatedAsync(TestScope scope)
    {
        Log.Write($"{scope.Kind} scope created: {scope.Uid}");
        return default;
    }

    public ValueTask OnDisposingAsync(TestScope scope)
    {
        Log.Write($"{scope.Kind} scope disposing: {scope.Uid}");
        return default;
    }
}

/// <summary>Adds a <see cref="FixedClock"/> override to each test scope.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class FixedClockAttribute : Attribute, ITestScopeListener
{
    public ValueTask OnCreatedAsync(TestScope scope)
    {
        if (scope.Kind == TestScopeKind.Test)
        {
            scope.AddOverride<IClock>(new FixedClock());
        }

        return default;
    }

    public ValueTask OnDisposingAsync(TestScope scope) => default;
}

/// <summary>Supplies every string parameter it applies to.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter)]
public sealed class ProvideAttribute(string value) : Attribute, ITestParameterProvider
{
    public bool TryProvide(TestContext context, TestParameter parameter, out object? provided)
    {
        provided = parameter.Type == typeof(string) ? value : null;
        return provided is not null;
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class ThrowingClassAttribute : Attribute
{
    public ThrowingClassAttribute() => throw new InvalidOperationException("The class attribute constructor failed.");
}

public sealed class LogMiddleware : ITestMiddleware
{
    public async ValueTask InvokeAsync(TestContext context, Func<ValueTask> next)
    {
        context.Output.WriteLine("middleware enter");
        try
        {
            await next();
        }
        finally
        {
            context.Output.WriteLine("middleware leave");
        }
    }
}

public sealed class ThrowOnLeaveMiddleware : ITestMiddleware
{
    public async ValueTask InvokeAsync(TestContext context, Func<ValueTask> next)
    {
        try
        {
            await next();
        }
        finally
        {
            Fail();
        }
    }

    private static void Fail() => throw new InvalidOperationException("The middleware failed on leaving.");
}
