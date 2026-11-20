using System.Runtime.InteropServices;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;

namespace Lifecycle;

/// <summary>
/// Appends lines to the file that <c>VG_FIXTURE_LOG</c> names. Each resource writes <c>acquire</c> when it
/// is acquired and <c>release</c> when its owner releases it, so the specs can check that every acquired
/// resource is released exactly once. Does nothing when the variable is unset.
/// </summary>
public static class Log
{
    private static readonly string? s_path = Environment.GetEnvironmentVariable("VG_FIXTURE_LOG");
    private static readonly Lock s_lock = new();

    public static void Write(string line)
    {
        if (s_path is not null)
        {
            lock (s_lock)
            {
                File.AppendAllText(s_path, line + "\n");
            }
        }
    }
}

/// <summary>
/// The faults that <c>VG_INJECT</c> names, separated by <c>;</c>. Each is <c>level:boundary:fault</c>,
/// where level is assembly, class or test, and fault is exception or cancellation. A fault at a boundary
/// that each test passes, such as middleware entry, fires only in tests named First.
/// </summary>
public static class Inject
{
    private const int SigInt = 2;

    private static readonly Dictionary<(string Level, string Boundary), string> s_faults = Parse(Environment.GetEnvironmentVariable("VG_INJECT"));

    /// <summary>The run's cancellation token, which the assembly scope's listener stores.</summary>
    public static CancellationToken RunToken { get; set; }

    public static bool IsFirst(string uid) => uid.Contains(".First(", StringComparison.Ordinal);

    public static string Level(TestScopeKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>
    /// Throws when <c>VG_INJECT</c> names this boundary. A cancellation fault cancels the run with SIGINT,
    /// as Ctrl+C does, waits for the run's token, and throws the exception that code watching it throws.
    /// </summary>
    public static void At(string level, string boundary, string? step = null)
    {
        if (!s_faults.TryGetValue((level, boundary), out var fault))
        {
            return;
        }

        var where = step is null ? $"{level} {boundary}" : $"{level} {boundary} ({step})";
        Log.Write($"inject {fault} at {where}");
        if (fault == "exception")
        {
            throw new InvalidOperationException($"Injected at {where}.");
        }

        if (!RunToken.IsCancellationRequested)
        {
            _ = Kill(Environment.ProcessId, SigInt);
            if (!RunToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(30)))
            {
                Log.Write($"the run was not cancelled at {where}");
                throw new TimeoutException($"SIGINT did not cancel the run at {where}.");
            }
        }

        throw new OperationCanceledException($"Cancelled at {where}.", RunToken);
    }

    private static Dictionary<(string, string), string> Parse(string? value)
    {
        var faults = new Dictionary<(string, string), string>();
        foreach (var entry in (value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
            faults[(parts[0], parts[1])] = parts[2];
        }

        return faults;
    }

    [DllImport("libc", EntryPoint = "kill")]
    private static extern int Kill(int pid, int signal);
}

/// <summary>A service that each container creates and disposes.</summary>
public sealed class Service : IDisposable
{
    private static int s_next;

    public Service(string owner) => Name = $"service {Interlocked.Increment(ref s_next)} of {owner}";

    public string Name { get; }

    public static Service Create(string owner)
    {
        var service = new Service(owner);
        Log.Write("acquire " + service.Name);
        return service;
    }

    public void Dispose() => Log.Write("release " + Name);
}

public interface ICalculator
{
    int Add(int a, int b);
}

/// <summary>
/// Logs each scope it is told about. It is an assembly attribute, so it hears about the assembly scope,
/// each class scope and each test scope.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ProbeAttribute : Attribute, ITestScopeListener
{
    public ValueTask OnCreatedAsync(TestScope scope)
    {
        if (scope.Kind == TestScopeKind.Assembly)
        {
            Inject.RunToken = scope.CancellationToken;
        }

        var level = Inject.Level(scope.Kind);
        if (Applies(scope))
        {
            Inject.At(level, "scope-creation");
        }

        Log.Write($"acquire listener {level} {scope.Uid}");
        return default;
    }

    public ValueTask OnDisposingAsync(TestScope scope)
    {
        var level = Inject.Level(scope.Kind);
        Log.Write($"release listener {level} {scope.Uid}");
        if (Applies(scope))
        {
            Inject.At(level, "disposal", "listener");
        }

        return default;
    }

    private static bool Applies(TestScope scope) => scope.Kind != TestScopeKind.Test || Inject.IsFirst(scope.Uid);
}

public abstract class ProbeMiddleware(string level) : ITestMiddleware
{
    public async ValueTask InvokeAsync(TestContext context, Func<ValueTask> next)
    {
        var first = Inject.IsFirst(context.Uid);
        if (first)
        {
            Inject.At(level, "middleware-entry");
        }

        Log.Write($"acquire middleware {level} {context.Uid}");
        try
        {
            await next();
        }
        finally
        {
            Log.Write($"release middleware {level} {context.Uid}");
            if (first)
            {
                Inject.At(level, "middleware-exit");
            }
        }
    }
}

public sealed class AssemblyMiddleware() : ProbeMiddleware("assembly");

public sealed class ClassMiddleware() : ProbeMiddleware("class");

public sealed class MethodMiddleware() : ProbeMiddleware("test");

/// <summary>Declines every parameter, after injecting a fault for the test method's parameters.</summary>
public abstract class ProbeProviderAttribute(string level) : Attribute, ITestParameterProvider
{
    public bool TryProvide(TestContext context, TestParameter parameter, out object? value)
    {
        if (!parameter.IsConstructorParameter && Inject.IsFirst(context.Uid))
        {
            Inject.At(level, "parameter");
        }

        value = null;
        return false;
    }
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class AssemblyProviderAttribute() : ProbeProviderAttribute("assembly");

[AttributeUsage(AttributeTargets.Class)]
public sealed class ClassProviderAttribute() : ProbeProviderAttribute("class");

[AttributeUsage(AttributeTargets.Method)]
public sealed class MethodProviderAttribute() : ProbeProviderAttribute("test");

public sealed class ProbeBootstrap : ITestContainerBootstrap, ISupportsChildOverrides
{
    public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context)
    {
        var level = Inject.Level(context.Scope.Kind);
        if (context.Scope.Kind != TestScopeKind.Test || Inject.IsFirst(context.Uid))
        {
            Inject.At(level, "container");
        }

        return new(new ProbeContainer(level, context.Uid, null));
    }
}

/// <summary>
/// Creates a <see cref="Service"/> for each test, a <see cref="ContainerOwned"/> test class, and one
/// <see cref="ICalculator"/> mock that its children share. It disposes what it created, newest first.
/// </summary>
public sealed class ProbeContainer : ITestContainer, IServiceProvider
{
    private readonly string _level;
    private readonly string _name;
    private readonly ProbeContainer? _parent;
    private readonly List<IDisposable> _owned = [];
    private ICalculator? _calculator;

    public ProbeContainer(string level, string name, ProbeContainer? parent)
    {
        _level = level;
        _name = name;
        _parent = parent;
        Log.Write($"acquire container {level} {name}");
    }

    public IServiceProvider Services => this;

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(ICalculator))
        {
            return _parent?.GetService(serviceType) ?? (_calculator ??= Mock.Of<ICalculator>());
        }

        IDisposable? created = serviceType == typeof(Service) ? Service.Create(_name)
            : serviceType == typeof(ContainerOwned) ? new ContainerOwned(_name)
            : null;
        if (created is not null)
        {
            lock (_owned)
            {
                _owned.Add(created);
            }
        }

        return created;
    }

    public bool TryResolve(TestParameter parameter, out object? value)
    {
        value = GetService(parameter.Type);
        return value is not null;
    }

    public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context) =>
        new(new ProbeContainer("test", context.Uid, this));

    public ValueTask DisposeAsync()
    {
        var failures = new List<Exception>();
        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            try
            {
                _owned[i].Dispose();
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }

        Log.Write($"release container {_level} {_name}");
        if (_level != "test" || Inject.IsFirst(_name))
        {
            try
            {
                Inject.At(_level, "disposal", "container");
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }

        return failures.Count switch
        {
            0 => default,
            1 => ValueTask.FromException(failures[0]),
            _ => ValueTask.FromException(new AggregateException(failures)),
        };
    }
}
