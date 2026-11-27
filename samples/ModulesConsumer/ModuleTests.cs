using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using ModulesConsumer;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.DependencyInjection;
using static VerificationGlue.VerifySyntax;

[assembly: ServiceCollection]
[assembly: ClockModule]

namespace ModulesConsumer;

public interface IClock
{
    DateTime Now { get; }
}

[SingletonService]
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.UtcNow;
}

[TransientService]
public sealed class Stamper(IClock clock)
{
    public string Stamp(string text) => $"{text} {clock.Now:yyyy-MM-dd}";
}

[DependencyModule]
public partial class ClockModule;

public sealed record Banner(string Text);

[DependencyModule(OnlyRealm = true)]
public partial class BannerModule(string text, int width) : IServiceCollectionConfiguration
{
    public char Fill { get; set; } = ' ';

    private Banner Banner => new(text.PadLeft(width, Fill));

    public void ConfigureServices(IServiceCollection services) => services.AddSingleton(Banner);

    public override bool Equals(object? obj) => obj is BannerModule other && other.Banner == Banner;

    public override int GetHashCode() => Banner.GetHashCode();
}

// Each test checks a part that Native AOT could break: the module attributes the Tests generator
// writes from syntax, the generated AddModules source, new T() for [ServiceCollection<T>] and mocks.
public partial class ModuleTests
{
    [VerifyTest]
    public void The_assembly_module_registers_the_clock(IClock clock)
    {
        Verify(clock is SystemClock);
    }

    [VerifyTest]
    public void The_service_receives_the_mock([Mock] IClock clock, Stamper stamper)
    {
        clock.Now.Returns(new DateTime(2026, 11, 27));

        Verify(stamper.Stamp("shipped") == "shipped 2026-11-27");
    }

    [VerifyTest]
    [BannerModule("sale", 6, Fill = '*')]
    public void A_module_gets_its_arguments(Banner banner)
    {
        Verify(banner.Text == "**sale");
    }

    [VerifyTest]
    [ServiceCollection<FreeBannerModule>]
    public void A_named_module_adds_its_services(Banner banner)
    {
        Verify(banner.Text == "free");
    }
}

[DependencyModule(OnlyRealm = true)]
public partial class FreeBannerModule : IServiceCollectionConfiguration
{
    public void ConfigureServices(IServiceCollection services) => services.AddSingleton(new Banner("free"));
}
