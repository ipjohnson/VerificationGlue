using VerificationGlue.Mocks.Generator;

namespace VerificationGlue.Tests.Generator.Specs;

/// <summary>
/// The registrations pass a <c>[FromKeyedServices]</c> key with a <c>[Mock]</c> override, and each
/// parameter's default value when nothing else supplies it.
/// </summary>
public class ParameterSpecs
{
    private const string Tests = """
        using Microsoft.Extensions.DependencyInjection;
        using VerificationGlue.Mocks;
        using VerificationGlue.Tests;
        using static VerificationGlue.VerifySyntax;

        namespace Microsoft.Extensions.DependencyInjection
        {
            [AttributeUsage(AttributeTargets.Parameter)]
            public sealed class FromKeyedServicesAttribute(object key) : Attribute
            {
                public object Key { get; } = key;
            }
        }

        namespace Shop
        {
            public interface IClock { DateTime Now { get; } }

            public enum Level { Low, High }

            public sealed class RecordingBootstrap : ITestContainerBootstrap
            {
                public static IReadOnlyList<TestOverride> Overrides { get; private set; } = [];

                public static ValueTask<ITestContainer> BuildAsync(TestContainerContext context)
                {
                    Overrides = context.Overrides;
                    return new(new Empty());
                }

                private sealed class Empty : ITestContainer, IServiceProvider
                {
                    public IServiceProvider Services => this;
                    public object? GetService(Type serviceType) => null;
                    public bool TryResolve(TestParameter parameter, out object? value) { value = null; return false; }
                    public ValueTask<ITestContainer> CreateChildAsync(TestContainerContext context) => new(this);
                    public ValueTask DisposeAsync() => default;
                }
            }

            [Container<RecordingBootstrap>]
            public partial class OrderTests
            {
                [VerifyTest]
                public void Keyed([Mock][FromKeyedServices("backup")] IClock clock, int count = 7, Level level = Level.High, string? name = null, decimal rate = 1.5m)
                {
                    var entry = RecordingBootstrap.Overrides.Single();
                    Verify(ReferenceEquals(entry.Instance, clock));
                    Verify(entry.ServiceType == typeof(IClock));
                    Verify((string?)entry.Key == "backup");
                    Verify(count == 7 && level == Level.High && name is null && rate == 1.5m);
                }
            }
        }
        """;

    [Fact]
    public async Task A_keyed_mock_is_registered_under_its_key_and_defaults_fill_the_rest()
    {
        var tests = Registrations.Load(Tests, rewrite: false, new MockGenerator(), new TestGenerator());

        var failures = await Assert.Single(tests).Value();

        Assert.Empty(failures);
    }
}
