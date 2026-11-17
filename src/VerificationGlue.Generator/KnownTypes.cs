using System;
using System.Threading.Tasks;
using CSharpAuthor;

namespace VerificationGlue.Generator;

internal static class KnownTypes
{
    public static readonly ITypeDefinition VerifyFailure = TypeDefinition.Get("VerificationGlue", "VerifyFailure");
    public static readonly ITypeDefinition VerifyMessageHandler = TypeDefinition.Get("VerificationGlue", "VerifyMessageHandler");
    public static readonly ITypeDefinition InterceptsLocationAttribute = TypeDefinition.Get("System.Runtime.CompilerServices", "InterceptsLocationAttribute");

    public static readonly ITypeDefinition String = TypeDefinition.Get(typeof(string));
    public static readonly ITypeDefinition Exception = TypeDefinition.Get(typeof(Exception));
    public static readonly ITypeDefinition Action = TypeDefinition.Get(typeof(Action));
    public static readonly ITypeDefinition FuncOfTask = TypeDefinition.Get(typeof(Func<Task>));
}
