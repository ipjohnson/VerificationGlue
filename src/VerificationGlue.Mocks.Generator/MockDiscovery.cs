using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Mocks.Generator;

internal enum ArgKind
{
    Any,
    IsPredicate,
    IsValue,
}

/// <summary>Recognises the runtime's members and the places that name a type to mock. The generator and the analyzer share it.</summary>
internal static class MockDiscovery
{
    public const string MockAttribute = "VerificationGlue.Mocks.MockAttribute";
    public const string GenerateMockAttribute = "VerificationGlue.Mocks.GenerateMockAttribute";

    /// <summary>A syntax check for <c>Mock.Of&lt;T&gt;()</c>, written with or without the class name.</summary>
    public static bool IsMockOfSyntax(SyntaxNode node) =>
        node is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocation &&
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Of", TypeArgumentList.Arguments.Count: 1 } } => true,
            GenericNameSyntax { Identifier.ValueText: "Of", TypeArgumentList.Arguments.Count: 1 } => true,
            _ => false,
        };

    /// <summary>A syntax check for <c>Arg.Any</c> or <c>Arg.Is</c> written directly as an argument.</summary>
    public static bool IsArgSyntax(SyntaxNode node) =>
        node is InvocationExpressionSyntax { Parent: ArgumentSyntax } invocation &&
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Any" or "Is" } => true,
            SimpleNameSyntax { Identifier.ValueText: "Any" or "Is" } => true,
            _ => false,
        };

    public static bool IsMockOf(IMethodSymbol method) =>
        method is { Name: "Of", TypeArguments.Length: 1, Parameters.Length: 0 } && IsRuntimeType(method.ContainingType, "Mock");

    public static ArgKind? ArgKindOf(IMethodSymbol method)
    {
        if (!IsRuntimeType(method.ContainingType, "Arg"))
        {
            return null;
        }

        return method switch
        {
            { Name: "Any", Parameters.Length: 0 } => ArgKind.Any,
            { Name: "Is", Parameters.Length: 1 } when method.Parameters[0].Type.TypeKind == TypeKind.Delegate => ArgKind.IsPredicate,
            { Name: "Is", Parameters.Length: 1 } => ArgKind.IsValue,
            _ => null,
        };
    }

    /// <summary>True for <c>Returns</c> and <c>Throws</c>, which apply to the mock call before them.</summary>
    public static bool IsSetup(IMethodSymbol method)
    {
        var definition = method.ReducedFrom ?? method;
        return definition.Name is "Returns" or "Throws" && IsRuntimeType(definition.ContainingType, "MockSyntax");
    }

    /// <summary>A member a generated mock implements and records: an instance member of an interface.</summary>
    public static bool IsMockableMember(ISymbol? member) =>
        member is { IsStatic: false, ContainingType.TypeKind: TypeKind.Interface } && (member.IsAbstract || member.IsVirtual);

    /// <summary>
    /// The type to mock, when it is a named type with no type parameters in it. A type parameter is
    /// only known when the code runs, so the generator cannot discover it.
    /// </summary>
    public static INamedTypeSymbol? Closed(ITypeSymbol? type) =>
        type?.WithNullableAnnotation(NullableAnnotation.NotAnnotated) is INamedTypeSymbol { IsUnboundGenericType: false } named && !ContainsTypeParameter(named)
            ? named
            : null;

    /// <summary>The type of the parameter, property or field a <c>[Mock]</c> attribute marks.</summary>
    public static ITypeSymbol? TypeOfTarget(ISymbol? target) => target switch
    {
        IParameterSymbol parameter => parameter.Type,
        IPropertySymbol property => property.Type,
        IFieldSymbol field => field.Type,
        _ => null,
    };

    public static bool ContainsTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
        IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
        INamedTypeSymbol named => AnyTypeParameter(named),
        _ => false,
    };

    private static bool AnyTypeParameter(INamedTypeSymbol named)
    {
        foreach (var argument in named.TypeArguments)
        {
            if (ContainsTypeParameter(argument))
            {
                return true;
            }
        }

        return named.ContainingType != null && ContainsTypeParameter(named.ContainingType);
    }

    private static bool IsRuntimeType(INamedTypeSymbol? type, string name) =>
        type is { ContainingNamespace: { Name: "Mocks", ContainingNamespace: { Name: "VerificationGlue", ContainingNamespace.IsGlobalNamespace: true } } } &&
        type.Name == name;
}
