using CSharpAuthor;

namespace VerificationGlue.Mocks.Generator;

internal static class KnownTypes
{
    public static readonly ITypeDefinition MockAdapter = TypeDefinition.Get("VerificationGlue.Mocks", "MockAdapter");
    public static readonly ITypeDefinition IMock = TypeDefinition.Get(TypeDefinitionEnum.InterfaceDefinition, "VerificationGlue.Mocks", "IMock");
}
