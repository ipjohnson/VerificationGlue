using CSharpAuthor;
using CSharpAuthor.Collections;

namespace VerificationGlue.Mocks.Generator;

internal enum MemberKind
{
    Method,
    Property,
    Indexer,
    Event,
}

/// <summary>Picks the <c>MockAdapter</c> method a member calls.</summary>
internal enum ReturnKind
{
    Void,
    Value,
    Ref,
    Task,
    TaskOfT,
    ValueTask,
    ValueTaskOfT,
}

internal enum RefReturn
{
    None,
    Ref,
    RefReadonly,
}

/// <summary>
/// A type parameter and the constraints the emitter writes for it. On the mock class these are the
/// interface's constraints. On an explicit method implementation C# allows only <c>class</c>,
/// <c>struct</c> and <c>default</c>, so <see cref="Primary"/> is one of those or null and the other
/// parts are empty.
/// </summary>
internal sealed record TypeParameterModel(
    string Name,
    string? Primary,
    EquatableArray<ITypeDefinition> Types,
    bool HasConstructor);

internal sealed record ParameterModel(
    string Name,
    ITypeDefinition Type,
    ParameterModifier Modifier,
    bool IsParams,
    bool IsScoped);

/// <summary>
/// One interface member. <see cref="Unsupported"/> holds the reason when the member compiles but
/// throws, which the generator reports as VGM0003.
/// </summary>
internal sealed record MockMemberModel(
    MemberKind Kind,
    int Id,
    string Name,
    ITypeDefinition Interface,
    ITypeDefinition? Type,
    ReturnKind ReturnKind,
    RefReturn RefReturn,
    ITypeDefinition? AwaitedType,
    EquatableArray<ParameterModel> Parameters,
    EquatableArray<TypeParameterModel> TypeParameters,
    bool HasGetter,
    bool HasSetter,
    bool IsInit,
    bool IsUnsafe,
    bool CanRaise,
    string? Unsupported);

internal sealed record DiagnosticModel(string Id, EquatableArray<string> Arguments);

/// <summary>
/// Everything the emitter reads for one mocked type. It holds strings, enums, CSharpAuthor type
/// definitions and equatable arrays, and no symbol, syntax node or compilation.
/// </summary>
/// <param name="TypeName">The mocked type as written in a message.</param>
/// <param name="MockName">The generated class name.</param>
/// <param name="Type">The mocked type. For an open generic interface its arguments are the mock class's own type parameters.</param>
/// <param name="TypeParameters">The mock class's type parameters, empty unless the interface is an open generic.</param>
/// <param name="ExternAliases">The extern aliases the generated file declares.</param>
/// <param name="Members">The members, empty when the type is refused.</param>
/// <param name="Diagnostics">VGM0001 when the type is refused, and VGM0003 for each member that throws.</param>
/// <param name="IsObsolete">
/// True when the type or a type in its signatures is obsolete. The mock class is then marked
/// obsolete, because C# reports no use of an obsolete type inside an obsolete one, and
/// <c>[Obsolete(error: true)]</c> cannot be suppressed any other way.
/// </param>
internal sealed record MockModel(
    string TypeName,
    string MockName,
    ITypeDefinition Type,
    EquatableArray<TypeParameterModel> TypeParameters,
    EquatableArray<string> ExternAliases,
    EquatableArray<MockMemberModel> Members,
    EquatableArray<DiagnosticModel> Diagnostics,
    bool IsObsolete = false)
{
    public bool IsRefused
    {
        get
        {
            foreach (var diagnostic in Diagnostics)
            {
                if (diagnostic.Id == MockDiagnostics.TypeCannotBeMocked.Id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
