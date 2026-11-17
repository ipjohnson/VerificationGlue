using System.IO;
using System.Linq;
using System.Text;
using CSharpAuthor;
using CSharpAuthor.Expressions;
using Microsoft.CodeAnalysis.CSharp;

namespace ColdBuild.Generator;

internal static class Emitter
{
    private const string GeneratedNamespace = "VerificationGlue.Generated";
    private const int ContractVersion = 1;

    private static readonly ITypeDefinition FailureType = TypeDefinition.Get("VerificationGlue", "VerifyFailure");
    private static readonly ITypeDefinition CopyMarker = TypeDefinition.Get("VerificationGlue", "VerifiedCopyAttribute");
    private static readonly ITypeDefinition InterceptsLocation = TypeDefinition.Get("System.Runtime.CompilerServices", "InterceptsLocationAttribute");
    private static readonly ITypeDefinition StringType = TypeDefinition.Get(typeof(string));

    // CSharpAuthor writes one namespace per file and no `using static` or alias directives, so the
    // copied usings and the file-local attribute go in front of its output as text.
    private const string InterceptsLocationDeclaration =
        "namespace System.Runtime.CompilerServices\n{\n" +
        "    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]\n" +
        "    file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute\n    {\n    }\n}\n";

    /// <summary>A hint name that is unique per source path, since two folders can hold files of the same name.</summary>
    public static string HintName(string path, string suffix)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in path)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return $"{Path.GetFileNameWithoutExtension(path)}.{hash:x8}.{suffix}.g.cs";
        }
    }

    public static string Interceptors(FileCalls file)
    {
        var source = new CSharpFileDefinition(GeneratedNamespace);
        var type = source.AddClass("Interceptors");
        type.Modifiers = ComponentModifier.File | ComponentModifier.Static;
        var index = 0;
        foreach (var call in file.Calls)
        {
            var method = type.AddMethod("Verify_" + index++);
            method.Modifiers = ComponentModifier.Public | ComponentModifier.Static;
            method.AddAttribute(InterceptsLocation, Ex.Int(call.LocationVersion), Ex.Str(call.LocationData));
            method.AddParameter(typeof(bool), "condition");
            method.AddParameter(StringType.MakeNullable(), "expression").DefaultValue = Ex.Null;
            method.If(Ex.Not(Ex.Id("condition"))).AddIndentedStatement(Ex.Throw(
                Ex.Call(FailureType, "Static", Ex.Str(call.Expression), Ex.Str(call.Message), Ex.Str(call.FilePath), Ex.Int(call.Line))));
        }

        return Write(source, InterceptsLocationDeclaration);
    }

    public static string Redirects(FileRedirects file)
    {
        var source = new CSharpFileDefinition(GeneratedNamespace);
        var type = source.AddClass("Redirects");
        type.Modifiers = ComponentModifier.File | ComponentModifier.Static;
        var index = 0;
        foreach (var redirect in file.Redirects)
        {
            var method = type.AddMethod("Redirect_" + index++);
            method.Modifiers = ComponentModifier.Public | ComponentModifier.Static;
            method.AddAttribute(InterceptsLocation, Ex.Int(redirect.LocationVersion), Ex.Str(redirect.LocationData));
            if (redirect.ReturnType is not null)
            {
                method.SetReturnType(redirect.ReturnType);
            }

            Ex receiver;
            if (redirect.IsStatic)
            {
                receiver = Ex.Type(redirect.ContainingType);
            }
            else
            {
                method.AddParameter(redirect.ContainingType, "@this").This = true;
                receiver = Ex.Id("@this");
            }

            AddParameters(method, redirect.Parameters);
            var call = receiver.Call(redirect.Name + Eligibility.Suffix, redirect.Parameters.Select(Argument).ToArray());
            if (redirect.ReturnType is null)
            {
                method.AddIndentedStatement(call);
            }
            else
            {
                method.Return(call);
            }
        }

        return Write(source, InterceptsLocationDeclaration);
    }

    public static string Copy(MethodModel model)
    {
        var source = new CSharpFileDefinition(model.Namespace ?? "");
        ClassDefinition? type = null;
        foreach (var shell in model.Types)
        {
            type = type is null ? source.AddClass(shell.Name) : type.AddClass(shell.Name);
            type.Modifiers = ComponentModifier.NoAccessibility | ComponentModifier.Partial;
            type.TypeKeyword = shell.Keyword switch
            {
                "struct" => ClassKeyword.Struct,
                "record class" => ClassKeyword.Record,
                "record struct" => ClassKeyword.RecordStruct,
                _ => ClassKeyword.Class,
            };
        }

        var method = type!.AddMethod(model.Name + Eligibility.Suffix);
        method.Modifiers = ComponentModifier.Internal |
            (model.IsStatic ? ComponentModifier.Static : ComponentModifier.None) |
            (model.IsAsync ? ComponentModifier.Async : ComponentModifier.None);
        method.AddAttribute(CopyMarker, Ex.Int(ContractVersion));
        if (model.ReturnType is not null)
        {
            method.SetReturnType(model.ReturnType);
        }

        AddParameters(method, model.Parameters);
        foreach (var statement in model.Statements)
        {
            if (statement.Verify is not { } verify)
            {
                method.Add(LineDirectiveComponent.At(statement.Line, model.FilePath));
                method.AddCode(statement.Text);
                continue;
            }

            method.Add(LineDirectiveComponent.Hidden());
            foreach (var capture in verify.Captures)
            {
                method.AddCode("{arg1} [arg2] = default!;", capture.Type, capture.Name);
            }

            method.Add(LineDirectiveComponent.At(statement.Line, model.FilePath));
            var failure = Ex.Call(FailureType, "Start", Ex.Str(verify.Expression), Ex.Str(model.FilePath), Ex.Int(statement.Line));
            foreach (var capture in verify.Captures)
            {
                failure = capture.Guard.Count == 0
                    ? failure.Call("Value", Ex.Str(capture.Label), Ex.Id(capture.Name))
                    : failure.Call("Value", Ex.Str(capture.Label), Ex.Id(capture.Name), Guard(capture));
            }

            method.If(Ex.Not(Ex.Paren(Ex.Raw(verify.Condition)))).AddIndentedStatement(Ex.Throw(failure.Call("Build")));
        }

        method.Add(LineDirectiveComponent.Default());

        var prefix = new StringBuilder();
        foreach (var directive in model.Usings)
        {
            prefix.Append(directive).Append('\n');
        }

        return Write(source, prefix.ToString());
    }

    private static Ex Guard(Capture capture)
    {
        Ex? guard = null;
        foreach (var term in capture.Guard)
        {
            var value = term.Negated ? Ex.Not(Ex.Id(term.Capture)) : Ex.Id(term.Capture);
            guard = guard is null ? value : guard & value;
        }

        return guard!;
    }

    private static void AddParameters(MethodDefinition method, CSharpAuthor.Collections.EquatableArray<Parameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            method.AddParameter(parameter.Type, Escape(parameter.Name)).Modifier = parameter.RefKind switch
            {
                "ref" => ParameterModifier.Ref,
                "out" => ParameterModifier.Out,
                "in" => ParameterModifier.In,
                _ => ParameterModifier.None,
            };
        }
    }

    private static Ex Argument(Parameter parameter)
    {
        var name = Ex.Id(parameter.Name);
        return parameter.RefKind.Length == 0 ? name : Ex.Raw(parameter.RefKind + " " + Escape(parameter.Name));
    }

    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    private static string Write(CSharpFileDefinition source, string prefix)
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global, GenerateDocumentation = false });
        source.WriteOutput(context);
        return "// <auto-generated/>\n#nullable enable\n#pragma warning disable\n" + prefix + context.Output();
    }
}
