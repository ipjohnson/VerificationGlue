// What the generator would emit.
using System.Runtime.CompilerServices;
using Microsoft.Testing.Platform.Builder;
using VerificationGlue;

namespace VerificationGlue.Generated;

file static class MyApp_Tests_ClockTests
{
    private static readonly Attribute[] s_classAttributes = [new global::MyApp.Tests.ClockAttribute()];

    private static readonly TestParameter[] s_NowIsFixed_parameters =
        [new("clock", typeof(global::MyApp.Tests.IClock), 0, [])];

    private static ValueTask<ITestContainer> Build<T>(TestContainerContext context)
        where T : ITestContainerBootstrap => T.BuildAsync(context);

    [ModuleInitializer]
    internal static void Register()
    {
        TestRegistry.Tests.Add(new TestDescriptor(
            Uid: "MyApp.Tests.ClockTests.NowIsFixed(MyApp.Tests.IClock)",
            DisplayName: "NowIsFixed",
            Namespace: "MyApp.Tests",
            ClassName: "ClockTests",
            MethodName: "NowIsFixed",
            FilePath: "UserTests.cs",
            Line: 38,
            BuildContainer: static context => Build<global::MyApp.Tests.ClockBootstrap>(context),
            Attributes: static () => s_classAttributes,
            Parameters: s_NowIsFixed_parameters,
            Invoke: static context =>
            {
                var instance = new global::MyApp.Tests.ClockTests();
                instance.NowIsFixed(context.Resolve<global::MyApp.Tests.IClock>(s_NowIsFixed_parameters[0]));
                return default;
            }));

        TestRegistry.Tests.Add(new TestDescriptor(
            Uid: "MyApp.Tests.ClockTests.Fails(MyApp.Tests.IClock)",
            DisplayName: "Fails",
            Namespace: "MyApp.Tests",
            ClassName: "ClockTests",
            MethodName: "Fails",
            FilePath: "UserTests.cs",
            Line: 44,
            BuildContainer: static context => Build<global::MyApp.Tests.ClockBootstrap>(context),
            Attributes: static () => s_classAttributes,
            Parameters: s_NowIsFixed_parameters,
            Invoke: static async context =>
            {
                var instance = new global::MyApp.Tests.ClockTests();
                await instance.Fails(context.Resolve<global::MyApp.Tests.IClock>(s_NowIsFixed_parameters[0]));
            }));
    }
}

file static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddVerificationGlue();
        using var app = await builder.BuildAsync();
        var exit = await app.RunAsync();
        Console.WriteLine($"containers disposed: {(global::MyApp.Tests.FixedClock.Disposed)}");
        return exit;
    }
}
