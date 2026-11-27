using Microsoft.CodeAnalysis;
using VerificationGlue.Tests.Generator;

namespace VerificationGlue.Tests.DependencyInjection.Generator.Specs;

public class DiagnosticSpecs
{
    private const string Accepted =
        "The generator accepts a type with 'public static void Configure(IServiceCollection services)' or " +
        "'public static void Configure(IServiceCollection services, TestContainerContext context)', or a DependencyModules module with a public parameterless constructor.";

    private const string Module = "public void PopulateServiceCollection(IServiceCollection services) { }";

    [VerifyTest]
    [Row("public sealed class Sut : DependencyModules.Runtime.Interfaces.IDependencyModule { public Sut(int size) { } " + Module + " }", "it is a DependencyModules module without a public parameterless constructor")]
    [Row("public sealed class Sut : DependencyModules.Runtime.Interfaces.IDependencyModule { internal Sut() { } " + Module + " }", "it is a DependencyModules module without a public parameterless constructor")]
    [Row("public abstract class Sut : DependencyModules.Runtime.Interfaces.IDependencyModule { " + Module + " }", "it is a DependencyModules module that is not a class 'new T()' can create")]
    [Row("[DependencyModules.Runtime.Attributes.DependencyModule] public partial class Sut { public Sut(string name) { } }", "it is a DependencyModules module without a public parameterless constructor")]
    public void A_module_that_new_T_cannot_create_is_VGD0001(string declaration, string reason)
    {
        var (run, _) = GeneratorHarness.Run("[ServiceCollection<Sut>] public partial class OrderTests { }", declaration, GeneratorHarness.DependencyModulesRuntime);

        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("VGD0001", diagnostic.Id);
        Assert.Equal($"[ServiceCollection<Sut>] names 'Sut', but {reason}. {Accepted}", diagnostic.GetMessage());
    }

    [VerifyTest]
    [Row("", "string", "string", "it has no method named Configure")]
    [Row("public sealed class Services { }", "Services", "Services", "it has no method named Configure")]
    [Row("public sealed class Services { public void Configure(IServiceCollection services) { } }", "Services", "Services", "its method 'public void Configure(IServiceCollection services)' does not have one of these signatures")]
    [Row("public sealed class Services { public static int Configure(IServiceCollection services) => 0; }", "Services", "Services", "its method 'public static int Configure(IServiceCollection services)' does not have one of these signatures")]
    [Row("public sealed class Services { internal static void Configure(IServiceCollection services) { } }", "Services", "Services", "its method 'internal static void Configure(IServiceCollection services)' does not have one of these signatures")]
    [Row("public sealed class Services { public static void Configure(object services) { } }", "Services", "Services", "its method 'public static void Configure(object services)' does not have one of these signatures")]
    [Row("public sealed class Services { public static void Configure(IServiceCollection services, int count) { } }", "Services", "Services", "its method 'public static void Configure(IServiceCollection services, int count)' does not have one of these signatures")]
    [Row("public sealed class Services { public static void Configure<T>(IServiceCollection services) { } }", "Services", "Services", "its method 'public static void Configure<T>(IServiceCollection services)' does not have one of these signatures")]
    [Row("public sealed class Services { public static void Configure() { } public static void Configure(ref IServiceCollection services) { } }", "Services", "Services", "its methods 'public static void Configure()' and 'public static void Configure(ref IServiceCollection services)' do not have one of these signatures")]
    public void A_type_without_a_usable_Configure_is_VGD0001(string declaration, string argument, string name, string reason)
    {
        var source = "[ServiceCollection<" + argument + ">] public partial class OrderTests { }";

        var (run, _) = GeneratorHarness.Run(source, declaration);

        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("VGD0001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal($"[ServiceCollection<{name}>] names '{name}', but {reason}. {Accepted}", diagnostic.GetMessage());
        Assert.Equal("/repo/Tests/OrderTests.cs", diagnostic.Location.GetLineSpan().Path);
        Assert.Equal("ServiceCollection<" + argument + ">", (GeneratorHarness.Usings + source).Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
        Assert.Empty(run.GeneratedTrees);
    }

    [VerifyTest]
    public void A_bad_type_on_the_assembly_or_a_method_is_VGD0001_and_the_good_ones_are_registered()
    {
        var (run, _) = GeneratorHarness.Run("""
            [assembly: ServiceCollection<string>]
            [ServiceCollection<Services>]
            public partial class OrderTests
            {
                [VerifyTest]
                [ServiceCollection<int>]
                public void M() { }
            }
            """,
            "public sealed class Services { public static void Configure(IServiceCollection services) { } }");

        Assert.Equal(["'string'", "'int'"], run.Diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" names ")[1].Split(',')[0]).Order().Reverse());
        Assert.All(run.Diagnostics, diagnostic => Assert.Equal("VGD0001", diagnostic.Id));
        Assert.Contains("Register<global::Services>", Assert.Single(run.GeneratedTrees).ToString(), StringComparison.Ordinal);
    }

    [VerifyTest]
    [Row("public partial class OrderTests { [VerifyTest] [ServiceCollection<Services>(Scope = ContainerScope.Class)] public void M() { } }", "ServiceCollection<Services>", "the method 'M'", "Class")]
    [Row("public partial class OrderTests { [VerifyTest] [ServiceCollection(Scope = ContainerScope.Assembly)] public void M() { } }", "ServiceCollection", "the method 'M'", "Assembly")]
    [Row("[ServiceCollection<Services>(Scope = ContainerScope.Assembly)] public partial class OrderTests { [VerifyTest] public void M() { } }", "ServiceCollection<Services>", "the class 'OrderTests'", "Assembly")]
    public void A_source_on_a_narrower_target_than_its_shared_scope_is_VGD0002(string source, string name, string target, string scope)
    {
        var diagnostic = Assert.Single(GeneratorHarness.Analyze(source, Services, new SharedScopeAnalyzer()));

        Assert.Equal("VGD0002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        var (built, move) = scope == "Class" ? ("the class and the assembly", "the class or the assembly") : ("the assembly", "the assembly");
        Assert.Equal(
            $"[{name}] is on {target} with Scope = {scope}, but a container shared with Scope = {scope} is built from the attributes of {built} before any test runs, so it does not read this attribute. Move it to {move}, or give it test scope.",
            diagnostic.GetMessage());
    }

    [VerifyTest]
    [Row("[assembly: ServiceCollection<Services>(Scope = ContainerScope.Assembly)] public partial class OrderTests { [VerifyTest] public void M() { } }")]
    [Row("[assembly: ServiceCollection(Scope = ContainerScope.Class)] public partial class OrderTests { [VerifyTest] public void M() { } }")]
    [Row("[ServiceCollection<Services>(Scope = ContainerScope.Class)] public partial class OrderTests { [VerifyTest] public void M() { } }")]
    [Row("[ServiceCollection<Services>(Scope = ContainerScope.Class)] public partial class OrderTests { [VerifyTest] [ServiceCollection<Services>] public void M() { } }")]
    [Row("public partial class OrderTests { [VerifyTest] [ServiceCollection<Services>(Scope = ContainerScope.Test)] public void M() { } }")]
    public void A_source_its_shared_container_reads_is_not_reported(string source)
    {
        Assert.Empty(GeneratorHarness.Analyze(source, Services, new SharedScopeAnalyzer()));
    }

    [VerifyTest]
    [Row("[ServiceCollection<Services>(Scope = ContainerScope.Class)] public partial class OrderTests { [VerifyTest] public void M([Mock] IClock clock) { } }", "ServiceCollectionBootstrap<Services>", "Class")]
    [Row("[assembly: ServiceCollection(Scope = ContainerScope.Assembly)] public partial class OrderTests { [VerifyTest] public void M([Mock] IClock clock) { } }", "ServiceCollectionBootstrap", "Assembly")]
    public void A_mock_under_a_shared_container_is_VGT0008(string source, string bootstrap, string scope)
    {
        var diagnostic = Assert.Single(GeneratorHarness.Analyze(source, Services, new TestAnalyzer(), new SharedScopeAnalyzer()));

        Assert.Equal("VGT0008", diagnostic.Id);
        Assert.Contains($"VerificationGlue.Tests.DependencyInjection.{bootstrap} is shared with Scope = {scope}", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [VerifyTest]
    public void A_mock_under_a_test_container_is_not_reported()
    {
        Assert.Empty(GeneratorHarness.Analyze(
            "[ServiceCollection<Services>] public partial class OrderTests { [VerifyTest] public void M([Mock] IClock clock) { } }",
            Services,
            new TestAnalyzer(),
            new SharedScopeAnalyzer()));
    }

    private const string Services = """
        public interface IClock { }
        public sealed class Services { public static void Configure(IServiceCollection services) { } }

        """;
}
