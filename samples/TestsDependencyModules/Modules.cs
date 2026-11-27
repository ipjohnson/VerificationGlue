using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using ModulesShop;
using TestsDependencyModules;
using VerificationGlue.Tests.DependencyInjection;

#region modules-assembly
[assembly: ServiceCollection]
[assembly: AuditModule]
#endregion

namespace TestsDependencyModules;

public interface IAuditLog
{
    void Write(string entry);
}

public sealed class AuditLog : IAuditLog
{
    public void Write(string entry)
    {
    }
}

public sealed class QuietAuditLog : IAuditLog
{
    public void Write(string entry)
    {
    }
}

#region audit-module
[DependencyModule(OnlyRealm = true)]
public partial class AuditModule : IServiceCollectionConfiguration
{
    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IAuditLog, AuditLog>();
}
#endregion

[DependencyModule(OnlyRealm = true)]
public partial class QuietAuditModule : IServiceCollectionConfiguration
{
    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IAuditLog, QuietAuditLog>();
}

public sealed class ReceiptPrinter(IClock clock)
{
    public string Print(int total) => $"{total} at {clock.Now:HH:mm}";
}

#region receipt-module
[DependencyModule(OnlyRealm = true)]
public partial class ReceiptModule : IServiceCollectionConfiguration
{
    public void ConfigureServices(IServiceCollection services) =>
        services.AddTransient<ReceiptPrinter>();
}
#endregion

public sealed class FixedClock : IClock
{
    public DateTime Now => new(2030, 1, 1);
}

[DependencyModule(OnlyRealm = true)]
public partial class FixedClockModule : IServiceCollectionConfiguration
{
    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IClock, FixedClock>();
}

public sealed class FixedClockServices
{
    public static void Configure(IServiceCollection services) =>
        services.AddSingleton<IClock, FixedClock>();
}

public sealed record Greeting(string Text);

#region greeting-module
[DependencyModule(OnlyRealm = true)]
public partial class GreetingModule(string word, int times) : IServiceCollectionConfiguration
{
    public string Punctuation { get; set; } = ".";

    private Greeting Greeting => new(string.Join(" ", Enumerable.Repeat(word + Punctuation, times)));

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton(Greeting);

    public override bool Equals(object? obj) =>
        obj is GreetingModule other && other.Greeting == Greeting;

    public override int GetHashCode() => Greeting.GetHashCode();
}
#endregion
