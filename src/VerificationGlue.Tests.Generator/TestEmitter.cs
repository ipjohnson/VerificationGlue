using System.Collections.Generic;
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
        var aliases = file.Methods.SelectMany(method => method.ExternAliases).Distinct().OrderBy(alias => alias, System.StringComparer.Ordinal);
        var text = Header(aliases);
        text.Append("    file static class Registration\n    {\n");
        text.Append("        [").Append(Render(KnownTypes.ModuleInitializerAttribute)).Append("]\n");
        text.Append("        internal static void Register()\n        {\n");

        foreach (var method in file.Methods)
        {
            foreach (var row in method.Rows)
            {
                AppendDescriptor(text, testClass, method, row.Uid, "new object?[] { " + string.Join(", ", row.Arguments) + " }", rewrite, null);
            }

            if (method.IsPlain || method.ExpandsAtDiscovery)
            {
                AppendDescriptor(text, testClass, method, method.Uid, "global::System.Array.Empty<object?>()", rewrite, method.ExpandsAtDiscovery ? DataSources(method) : null);
            }
        }

        text.Append("        }\n    }\n}\n");
        return text.ToString();
    }

    /// <summary>
    /// A lambda that creates the method's data sources: a typed reader for each <c>[RowSource]</c> member,
    /// then the method's attributes that implement <c>ITestDataSource</c>.
    /// </summary>
    private static string DataSources(TestMethodModel method)
    {
        var helper = Render(KnownTypes.TestDataSources);
        var members = method.Sources.Select(source =>
            "\n                    " + helper + "." + (source.IsAsync ? "AsyncMember" : "Member") + "<" + source.ElementType + ">(" +
            Literal(source.MemberName) + ", static () => " + source.Read + ", " + source.Values + ", " + (source.DeterministicReplay ? "true" : "false") + ")");
        return "static test => " + helper + ".With(test" + string.Concat(members.Select(member => "," + member)) + ")";
    }

    private static void AppendDescriptor(StringBuilder text, TestClassModel testClass, TestMethodModel method, string uid, string arguments, bool rewrite, string? dataSources)
    {
        text.Append("            ").Append(Render(KnownTypes.TestRegistry)).Append(".Add(new ").Append(Render(KnownTypes.TestDescriptor)).Append("(\n");
        text.Append("                ").Append(Literal(uid)).Append(",\n");
        text.Append("                static context => ").Append(method.NameFormatter).Append(".Format(in context),\n");
        text.Append("                ").Append(arguments).Append(",\n");
        text.Append("                typeof(").Append(Render(testClass.Type)).Append("),\n");
        text.Append("                ").Append(Literal(testClass.Namespace)).Append(",\n");
        text.Append("                ").Append(Literal(testClass.TypeName)).Append(",\n");
        text.Append("                ").Append(Literal(method.Name)).Append(",\n");
        text.Append("                ").Append(Literal(method.ReturnTypeName)).Append(",\n");
        text.Append("                ").Append(Literal(method.FilePath)).Append(",\n");
        text.Append("                ").Append(method.Line).Append(",\n");
        text.Append("                ").Append(Attributes(testClass.Attributes)).Append(",\n");
        text.Append("                ").Append(Attributes(method.Attributes)).Append(",\n");
        text.Append("                ").Append(Parameters(method.IsStatic ? [] : testClass.ConstructorParameters, isConstructor: true)).Append(",\n");
        text.Append("                ").Append(Parameters(method.Parameters, isConstructor: false)).Append(",\n");
        text.Append("                ").Append(CreateInstance(testClass, method)).Append(",\n");
        text.Append("                static async (context, instance) =>\n                {\n");
        AppendInvoke(text, testClass, method, rewrite);
        text.Append("                }");
        AppendSchedule(text, method.Schedule);
        if (dataSources is not null)
        {
            text.Append(",\n                createDataSources: ").Append(dataSources);
        }

        if (method.Traits.Count > 0)
        {
            const string pair = "global::System.Collections.Generic.KeyValuePair<string, string>";
            text.Append(",\n                traits: new ").Append(pair).Append("[] { ")
                .Append(string.Join(", ", method.Traits.Select(trait => "new " + pair + "(" + Literal(trait.Name) + ", " + Literal(trait.Value) + ")")))
                .Append(" }");
        }

        text.Append("));\n");
    }

    /// <summary>The assembly's attributes, which every scope's attribute list starts with.</summary>
    public static string EmitAssemblyAttributes(EquatableArray<AttributeModel> attributes)
    {
        var text = Header([]);
        text.Append("    file static class ").Append(AssemblyAttributesClass).Append("\n    {\n");
        text.Append("        [").Append(Render(KnownTypes.ModuleInitializerAttribute)).Append("]\n");
        text.Append("        internal static void Register() =>\n");
        text.Append("            ").Append(Render(KnownTypes.TestRegistry)).Append(".SetAssemblyAttributes(").Append(Attributes(attributes)).Append(");\n");
        text.Append("    }\n}\n");
        return text.ToString();
    }

    private static string Attributes(EquatableArray<AttributeModel> attributes) =>
        "static () => [" + string.Join(", ", attributes.Select(New)) + "]";

    private static string Parameters(EquatableArray<ParameterModel> parameters, bool isConstructor)
    {
        if (parameters.Count == 0)
        {
            return "global::System.Array.Empty<" + Render(KnownTypes.TestParameter) + ">()";
        }

        var text = new StringBuilder("new ").Append(Render(KnownTypes.TestParameter)).Append("[]\n                {\n");
        var position = 0;
        foreach (var parameter in parameters)
        {
            text.Append("                    new(").Append(Literal(parameter.Name))
                .Append(", typeof(").Append(parameter.Type).Append(')')
                .Append(", ").Append(Literal(parameter.TypeName))
                .Append(", ").Append(position++)
                .Append(", ").Append(isConstructor ? "true" : "false")
                .Append(", ").Append(Attributes(parameter.Attributes))
                .Append(", ").Append(parameter.IsMock ? "static () => " + Render(KnownTypes.Mock) + ".Of<" + parameter.Type + ">()" : "null")
                .Append(", ").Append(parameter.ServiceKey ?? "null")
                .Append(", ").Append(parameter.DefaultValue is null ? "false, null" : "true, " + parameter.DefaultValue)
                .Append("),\n");
        }

        return text.Append("                }").ToString();
    }

    private static string CreateInstance(TestClassModel testClass, TestMethodModel method)
    {
        if (method.IsStatic)
        {
            return "null";
        }

        if (!testClass.CanConstruct)
        {
            return "static context => throw " + Render(KnownTypes.TestRuntime) + ".NoConstructor(" +
                Literal(testClass.Namespace.Length == 0 ? testClass.TypeName : testClass.Namespace + "." + testClass.TypeName) + ")";
        }

        return "static context => new " + Render(testClass.Type) + "(" + Arguments(testClass.ConstructorParameters, "ConstructorArgument") + ")";
    }

    private static void AppendInvoke(StringBuilder text, TestClassModel testClass, TestMethodModel method, bool rewrite)
    {
        const string Indent = "                    ";
        var target = method.Name + (rewrite && method.WillRewrite ? VerifyRewriteRules.Suffix : "");
        text.Append(Indent)
            .Append(method.Return == ReturnKind.Awaitable ? "await " : "")
            .Append(method.IsStatic ? Render(testClass.Type) : "((" + Render(testClass.Type) + ")instance!)")
            .Append('.').Append(target).Append('(')
            .Append(Arguments(method.Parameters, "Argument"))
            .Append(");\n");
    }

    /// <summary>The schedule as named arguments, left out when the test is in the default group with no resources.</summary>
    private static void AppendSchedule(StringBuilder text, ScheduleModel schedule)
    {
        if (schedule.IsDefault)
        {
            return;
        }

        text.Append(",\n                scheduleGroup: ").Append(Literal(schedule.Group))
            .Append(",\n                maxParallel: ").Append(schedule.MaxParallel);
        if (schedule.RunAfter.Count > 0)
        {
            text.Append(",\n                runAfter: ").Append(Strings(schedule.RunAfter));
        }

        if (schedule.Resources.Count > 0)
        {
            text.Append(",\n                resources: ").Append(Strings(schedule.Resources));
        }
    }

    private static string Strings(EquatableArray<string> values) =>
        "new string[] { " + string.Join(", ", values.Select(Literal)) + " }";

    private static string Arguments(EquatableArray<ParameterModel> parameters, string helper) =>
        string.Join(", ", parameters.Select((parameter, position) =>
            "(" + parameter.Type + ")" + Render(KnownTypes.TestRuntime) + "." + helper + "(context, " + position + ")!"));

    private static StringBuilder Header(IEnumerable<string> externAliases)
    {
        var text = new StringBuilder("// <auto-generated/>\n#nullable enable\n#pragma warning disable\n");
        foreach (var alias in externAliases)
        {
            text.Append("extern alias ").Append(alias).Append(";\n");
        }

        return text.Append("namespace ").Append(KnownTypes.GeneratedNamespace).Append("\n{\n");
    }

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
