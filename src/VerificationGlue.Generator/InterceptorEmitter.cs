using System.IO;
using CSharpAuthor;
using CSharpAuthor.Expressions;

namespace VerificationGlue.Generator;

/// <summary>Writes the tier 2 interceptors of one source file.</summary>
internal static class InterceptorEmitter
{
    public const string GeneratedNamespace = "VerificationGlue.Generated";

    // The compiler recognises InterceptsLocationAttribute by name, and the BCL does not ship it, so
    // each file declares its own copy as a file-local type. CSharpAuthor writes one namespace per
    // file, so the declaration goes in front of its output as text.
    public const string InterceptsLocationDeclaration =
        "namespace System.Runtime.CompilerServices\n{\n" +
        "    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]\n" +
        "    file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute\n    {\n    }\n}\n";

    private static readonly Ex Condition = Ex.Id("condition");
    private static readonly Ex Expression = Ex.Id("expression");

    /// <summary>A hint name that is unique per source path, since two folders can hold files of the same name.</summary>
    public static string HintName(string path)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in path)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return $"{Path.GetFileNameWithoutExtension(path)}.{hash:x8}.Interceptors.g.cs";
        }
    }

    /// <summary>
    /// The path a message reports: relative to the project directory with forward slashes, or the
    /// file name when the file is outside the project.
    /// </summary>
    public static string DisplayPath(string path, string projectDirectory)
    {
        var normalized = path.Replace('\\', '/');
        var directory = projectDirectory.Replace('\\', '/');
        if (directory.Length > 0 && !directory.EndsWith("/", System.StringComparison.Ordinal))
        {
            directory += "/";
        }

        if (directory.Length > 0 && normalized.StartsWith(directory, System.StringComparison.OrdinalIgnoreCase))
        {
            return normalized.Substring(directory.Length);
        }

        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? normalized : normalized.Substring(slash + 1);
    }

    public static string Emit(FileCalls file)
    {
        var source = new CSharpFileDefinition(GeneratedNamespace);
        var type = source.AddClass("Interceptors");
        type.Modifiers = ComponentModifier.File | ComponentModifier.Static;
        foreach (var call in file.Calls)
        {
            var path = Ex.Str(DisplayPath(call.MappedPath, file.ProjectDirectory));
            var line = Ex.Int(call.MappedLine);
            var message = call.Message is null ? Ex.Null : Ex.Str(call.Message);
            var method = type.AddMethod($"{call.Kind.ToString().Replace("WithMessage", "")}_{call.Line}_{call.Column}");
            method.Modifiers = ComponentModifier.Public | ComponentModifier.Static;
            method.AddAttribute(KnownTypes.InterceptsLocationAttribute, Ex.Int(call.LocationVersion), Ex.Str(call.LocationData));
            switch (call.Kind)
            {
                case CallKind.Verify:
                    method.SetReturnType(KnownTypes.VerifyResult);
                    method.AddParameter(typeof(bool), "condition");
                    AddExpression(method);
                    method.If(Ex.Not(Condition)).AddIndentedStatement(Ex.Throw(
                        Ex.Call(KnownTypes.VerifyFailure, "Static", Expression, message, path, line)));
                    method.Return(Ex.Raw("default"));
                    break;
                case CallKind.VerifyWithMessage:
                    method.SetReturnType(KnownTypes.VerifyResult);
                    method.AddParameter(typeof(bool), "condition");
                    method.AddParameter(KnownTypes.VerifyMessageHandler, "message").Modifier = ParameterModifier.Ref;
                    AddExpression(method);
                    method.If(Ex.Not(Condition)).AddIndentedStatement(Ex.Throw(
                        Ex.Call(KnownTypes.VerifyFailure, "Static", Expression, message, path, line, Ex.RefArg(Ex.Id("message")))));
                    method.Return(Ex.Raw("default"));
                    break;
                case CallKind.Check:
                    method.AddParameter(typeof(bool), "condition");
                    AddExpression(method);
                    method.If(Ex.Not(Condition)).AddIndentedStatement(
                        Ex.Call(KnownTypes.VerifyFailure, "CheckFailed", Expression, message, path, line));
                    break;
                case CallKind.Throws:
                case CallKind.ThrowsAsync:
                case CallKind.ThrowsExactly:
                case CallKind.ThrowsExactlyAsync:
                    var async = call.Kind is CallKind.ThrowsAsync or CallKind.ThrowsExactlyAsync;
                    var exception = new TypeParameterDefinition("T");
                    method.AddGenericParameter(exception);
                    method.AddConstraint("T").Implements(KnownTypes.Exception);
                    method.SetReturnType(async ? new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "System.Threading.Tasks", "Task", new ITypeDefinition[] { exception }) : exception);
                    method.AddParameter(async ? KnownTypes.FuncOfTask : KnownTypes.Action, "action");
                    AddExpression(method);
                    method.Return(Ex.CallGeneric(KnownTypes.VerifyFailure, call.Kind.ToString(), new ITypeDefinition[] { exception }, Ex.Id("action"), Expression, message, path, line));
                    break;
                case CallKind.Single:
                case CallKind.SingleWhere:
                    var item = new TypeParameterDefinition("T");
                    method.AddGenericParameter(item);
                    method.SetReturnType(item);
                    method.AddParameter(new GenericTypeDefinition(TypeDefinitionEnum.InterfaceDefinition, "System.Collections.Generic", "IEnumerable", new ITypeDefinition[] { item }).MakeNullable(), "items");
                    if (call.Kind == CallKind.Single)
                    {
                        AddExpression(method);
                        method.Return(Ex.Call(KnownTypes.VerifyFailure, "Single", Ex.Id("items"), Expression, path, line));
                        break;
                    }

                    method.AddParameter(new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "System", "Func", new ITypeDefinition[] { item, KnownTypes.Bool }), "predicate");
                    AddExpression(method);
                    method.AddParameter(KnownTypes.String.MakeNullable(), "predicateExpression").DefaultValue = Ex.Null;
                    method.Return(Ex.Call(KnownTypes.VerifyFailure, "Single", Ex.Id("items"), Ex.Id("predicate"), Expression, Ex.Id("predicateExpression"), path, line));
                    break;
                case CallKind.Is:
                    var target = new TypeParameterDefinition("T");
                    method.AddGenericParameter(target);
                    method.SetReturnType(target);
                    method.AddParameter(KnownTypes.Object.MakeNullable(), "value");
                    AddExpression(method);
                    method.Return(Ex.CallGeneric(KnownTypes.VerifyFailure, "Is", new ITypeDefinition[] { target }, Ex.Id("value"), Expression, path, line));
                    break;
                case CallKind.NotNull:
                case CallKind.NotNullValue:
                    var value = new TypeParameterDefinition("T");
                    method.AddGenericParameter(value);
                    var constraint = method.AddConstraint("T");
                    if (call.Kind == CallKind.NotNull)
                    {
                        constraint.Class();
                    }
                    else
                    {
                        constraint.Struct();
                    }

                    method.SetReturnType(value);
                    method.AddParameter(value.MakeNullable(), "value");
                    AddExpression(method);
                    method.Return(Ex.Call(KnownTypes.VerifyFailure, "NotNull", Ex.Id("value"), Expression, path, line));
                    break;
            }
        }

        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global, GenerateDocumentation = false });
        source.WriteOutput(context);
        return "// <auto-generated/>\n#nullable enable\n#pragma warning disable\n" + InterceptsLocationDeclaration + context.Output();
    }

    private static void AddExpression(MethodDefinition method) =>
        method.AddParameter(KnownTypes.String.MakeNullable(), "expression").DefaultValue = Ex.Null;
}
