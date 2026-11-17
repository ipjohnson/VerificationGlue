using System.Linq;
using System.Text;
using CSharpAuthor;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis.CSharp;
using VerificationGlue.Shared;

namespace VerificationGlue.Tests.Generator;

/// <summary>
/// Writes the registration file of one test class: a module initializer that adds a
/// <c>TestDescriptor</c> per test to <c>TestRegistry</c>.
/// </summary>
internal static class TestEmitter
{
    public const string AssemblyAttributesHintName = "VerificationGlue.Tests.AssemblyAttributes.g.cs";
    private const string AssemblyAttributesClass = "AssemblyAttributes";

    public static string HintName(TestClassModel testClass) => testClass.MetadataName + ".Tests.g.cs";

    public static string Emit(TestClassFile file, bool rewrite)
    {
        var testClass = file.Class;
        var text = Header();
        text.Append("    file static class Registration\n    {\n");
        text.Append("        [").Append(Render(KnownTypes.ModuleInitializerAttribute)).Append("]\n");
        text.Append("        internal static void Register()\n        {\n");

        foreach (var method in file.Methods)
        {
            text.Append("            ").Append(Render(KnownTypes.TestRegistry)).Append(".Add(new ").Append(Render(KnownTypes.TestDescriptor)).Append("(\n");
            text.Append("                ").Append(Literal(method.Uid)).Append(",\n");
            text.Append("                ").Append(Literal(method.Name)).Append(",\n");
            text.Append("                typeof(").Append(Render(testClass.Type)).Append("),\n");
            text.Append("                ").Append(Literal(testClass.Namespace)).Append(",\n");
            text.Append("                ").Append(Literal(testClass.TypeName)).Append(",\n");
            text.Append("                ").Append(Literal(method.Name)).Append(",\n");
            text.Append("                ").Append(method.Parameters.Count == 0
                ? "global::System.Array.Empty<string>()"
                : "new string[] { " + string.Join(", ", method.Parameters.Select(parameter => Literal(parameter.TypeName))) + " }").Append(",\n");
            text.Append("                ").Append(Literal(method.ReturnTypeName)).Append(",\n");
            text.Append("                ").Append(Literal(method.FilePath)).Append(",\n");
            text.Append("                ").Append(method.Line).Append(",\n");
            text.Append("                static () => [.. global::").Append(KnownTypes.GeneratedNamespace).Append('.').Append(AssemblyAttributesClass).Append(".Create()");
            foreach (var attribute in testClass.Attributes.Concat(method.Attributes))
            {
                text.Append(", ").Append(New(attribute));
            }

            text.Append("],\n");
            text.Append("                static async context =>\n                {\n");
            AppendInvoke(text, testClass, method, rewrite);
            text.Append("                }));\n");
        }

        text.Append("        }\n    }\n}\n");
        return text.ToString();
    }

    /// <summary>The assembly's attributes, which every test's attribute list starts with.</summary>
    public static string EmitAssemblyAttributes(EquatableArray<AttributeModel> attributes)
    {
        var text = Header();
        text.Append("    internal static class ").Append(AssemblyAttributesClass).Append("\n    {\n");
        text.Append("        internal static ").Append(Render(KnownTypes.Attribute)).Append("[] Create() => [");
        text.Append(string.Join(", ", attributes.Select(New)));
        text.Append("];\n    }\n}\n");
        return text.ToString();
    }

    private static void AppendInvoke(StringBuilder text, TestClassModel testClass, TestMethodModel method, bool rewrite)
    {
        const string Indent = "                    ";
        var missing = method.Parameters.FirstOrDefault(parameter => parameter.Expression is null);
        if (missing is not null)
        {
            text.Append(Indent).Append("throw ").Append(Render(KnownTypes.TestRuntime)).Append(".NoValue(")
                .Append(Literal(missing.Name)).Append(", ").Append(Literal(missing.TypeName)).Append(");\n");
            return;
        }

        if (!method.IsStatic && !testClass.CanConstruct)
        {
            text.Append(Indent).Append("throw ").Append(Render(KnownTypes.TestRuntime)).Append(".NoConstructor(")
                .Append(Literal(testClass.Namespace.Length == 0 ? testClass.TypeName : testClass.Namespace + "." + testClass.TypeName)).Append(");\n");
            return;
        }

        var target = method.Name + (rewrite && method.WillRewrite ? VerifyRewriteRules.Suffix : "");
        var call = new StringBuilder(method.Return == ReturnKind.Awaitable ? "await " : "")
            .Append(method.IsStatic ? Render(testClass.Type) : "instance")
            .Append('.').Append(target).Append('(')
            .Append(string.Join(", ", method.Parameters.Select(parameter => parameter.Expression)))
            .Append(");")
            .ToString();

        if (method.IsStatic)
        {
            text.Append(Indent).Append(call).Append('\n');
            return;
        }

        text.Append(Indent).Append("var instance = new ").Append(Render(testClass.Type)).Append("();\n");
        text.Append(Indent).Append("try\n").Append(Indent).Append("{\n");
        text.Append(Indent).Append("    ").Append(call).Append('\n');
        text.Append(Indent).Append("}\n").Append(Indent).Append("finally\n").Append(Indent).Append("{\n");
        text.Append(Indent).Append("    await ").Append(Render(KnownTypes.TestRuntime)).Append(".DisposeAsync(instance);\n");
        text.Append(Indent).Append("}\n");
    }

    private static StringBuilder Header() =>
        new StringBuilder("// <auto-generated/>\n#nullable enable\n#pragma warning disable\n")
            .Append("namespace ").Append(KnownTypes.GeneratedNamespace).Append("\n{\n");

    private static string New(AttributeModel attribute)
    {
        var text = new StringBuilder("new ").Append(Render(attribute.Type)).Append('(')
            .Append(string.Join(", ", attribute.Arguments)).Append(')');
        if (attribute.NamedArguments.Count > 0)
        {
            text.Append(" { ").Append(string.Join(", ", attribute.NamedArguments.Select(named => named.Name + " = " + named.Value))).Append(" }");
        }

        return text.ToString();
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    private static string Render(ITypeDefinition type)
    {
        var builder = new StringBuilder();
        type.WriteTypeName(builder, TypeOutputMode.Global);
        return builder.ToString();
    }
}
