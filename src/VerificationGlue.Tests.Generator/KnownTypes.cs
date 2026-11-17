using CSharpAuthor;

namespace VerificationGlue.Tests.Generator;

internal static class KnownTypes
{
    public const string VerifyTestAttribute = "VerificationGlue.Tests.VerifyTestAttribute";
    public const string GeneratedNamespace = "VerificationGlue.Tests.Generated";

    public static readonly ITypeDefinition TestRegistry = TypeDefinition.Get("VerificationGlue.Tests", "TestRegistry");
    public static readonly ITypeDefinition TestDescriptor = TypeDefinition.Get("VerificationGlue.Tests", "TestDescriptor");
    public static readonly ITypeDefinition TestRuntime = TypeDefinition.Get("VerificationGlue.Tests", "TestRuntime");
    public static readonly ITypeDefinition Attribute = TypeDefinition.Get("System", "Attribute");
    public static readonly ITypeDefinition ModuleInitializerAttribute = TypeDefinition.Get("System.Runtime.CompilerServices", "ModuleInitializerAttribute");
}
