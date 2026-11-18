using CSharpAuthor;

namespace VerificationGlue.Tests.Generator;

internal static class KnownTypes
{
    public const string VerifyTestAttribute = "VerificationGlue.Tests.VerifyTestAttribute";
    public const string GeneratedNamespace = "VerificationGlue.Tests.Generated";
    public const string ContainerAttribute = "VerificationGlue.Tests.ContainerAttribute";
    public const string ScheduleGroupAttribute = "VerificationGlue.Tests.ScheduleGroupAttribute";
    public const string ResourceAttribute = "VerificationGlue.Tests.ResourceAttribute";
    public const string RunAfterAttribute = "VerificationGlue.Tests.RunAfterAttribute";
    public const string ISupportsChildOverrides = "VerificationGlue.Tests.ISupportsChildOverrides";
    public const string RowAttribute = "VerificationGlue.Tests.RowAttribute";
    public const string NameFormatterAttribute = "VerificationGlue.Tests.NameFormatterAttribute";
    public const string DefaultNameFormatter = "global::VerificationGlue.Tests.DefaultNameFormatter";
    public const string MockAttribute = "VerificationGlue.Mocks.MockAttribute";
    public const string FromKeyedServicesAttribute = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";

    public static readonly ITypeDefinition TestParameter = TypeDefinition.Get("VerificationGlue.Tests", "TestParameter");
    public static readonly ITypeDefinition Mock = TypeDefinition.Get("VerificationGlue.Mocks", "Mock");

    public static readonly ITypeDefinition TestRegistry = TypeDefinition.Get("VerificationGlue.Tests", "TestRegistry");
    public static readonly ITypeDefinition TestDescriptor = TypeDefinition.Get("VerificationGlue.Tests", "TestDescriptor");
    public static readonly ITypeDefinition TestRuntime = TypeDefinition.Get("VerificationGlue.Tests", "TestRuntime");
    public static readonly ITypeDefinition Attribute = TypeDefinition.Get("System", "Attribute");
    public static readonly ITypeDefinition ModuleInitializerAttribute = TypeDefinition.Get("System.Runtime.CompilerServices", "ModuleInitializerAttribute");
}
