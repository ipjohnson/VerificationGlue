using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VerificationGlue.Mocks.Generator;

/// <summary>
/// Writes the typed setup surface of one mock: a <c>_Setup</c> class and a <c>_Calls</c> class
/// with one member per mocked member, each taking <c>Arg&lt;T&gt;</c> for each parameter, and the
/// <c>Setup()</c> and <c>Calls()</c> extension methods. The extension methods are in the
/// <c>VerificationGlue.Mocks</c> namespace, which a test imports for <c>Mock.Of</c>.
/// </summary>
/// <remarks>
/// A member is left out when it throws (VGM0003), has a pointer, has no getter, or is an event.
/// A member is also left out when an earlier one already has its C# signature, such as a member
/// inherited from two interfaces, or <c>M(int)</c> and <c>M(out int)</c>.
/// </remarks>
internal static class SurfaceEmitter
{
    private const string Runtime = "global::VerificationGlue.Mocks.";
    private const string Adapter = "this.__adapter";
    private const string MockSuffix = "_Mock";

    public static string SetupName(MockModel model) => BaseName(model) + "_Setup";

    public static string CallsName(MockModel model) => BaseName(model) + "_Calls";

    public static string Emit(MockModel model)
    {
        var members = SurfaceMembers(model);
        var typeParameters = model.TypeParameters.Count == 0 ? "" : "<" + string.Join(", ", model.TypeParameters.Select(p => p.Name)) + ">";
        var constraints = model.TypeParameters.Select(Constraint).Where(text => text.Length > 0).ToList();
        var obsolete = model.IsObsolete ? "    [global::System.Obsolete]\n" : "";

        var text = new StringBuilder();
        text.Append("namespace ").Append(MockEmitter.GeneratedNamespace).Append("\n{\n");
        WriteClass(text, SetupName(model), typeParameters, constraints, obsolete, members, setup: true);
        text.Append('\n');
        WriteClass(text, CallsName(model), typeParameters, constraints, obsolete, members, setup: false);
        text.Append("}\n\n");

        var generated = "global::" + MockEmitter.GeneratedNamespace + ".";
        var mocked = MockEmitter.Render(model.Type);
        var where = string.Concat(constraints.Select(constraint => "\n            " + constraint));
        text.Append("namespace VerificationGlue.Mocks\n{\n");
        text.Append(obsolete);
        text.Append("    internal static class ").Append(BaseName(model)).Append("_MockExtensions\n    {\n");
        text.Append("        public static ").Append(generated).Append(SetupName(model)).Append(typeParameters)
            .Append(" Setup").Append(typeParameters).Append("(this ").Append(mocked).Append(" mock)").Append(where)
            .Append("\n            => new(").Append(Runtime).Append("MockAdapter.Of(mock));\n\n");
        text.Append("        public static ").Append(generated).Append(CallsName(model)).Append(typeParameters)
            .Append(" Calls").Append(typeParameters).Append("(this ").Append(mocked).Append(" mock)").Append(where)
            .Append("\n            => new(").Append(Runtime).Append("MockAdapter.Of(mock));\n");
        text.Append("    }\n}\n");
        return text.ToString();
    }

    private static void WriteClass(
        StringBuilder text,
        string name,
        string typeParameters,
        List<string> constraints,
        string obsolete,
        List<MockMemberModel> members,
        bool setup)
    {
        text.Append(obsolete);
        text.Append("    internal sealed class ").Append(name).Append(typeParameters).Append('\n');
        foreach (var constraint in constraints)
        {
            text.Append("        ").Append(constraint).Append('\n');
        }

        text.Append("    {\n");
        text.Append("        private readonly ").Append(Runtime).Append("MockAdapter __adapter;\n\n");
        text.Append("        internal ").Append(name).Append('(').Append(Runtime).Append("MockAdapter adapter)\n");
        text.Append("        {\n            ").Append(Adapter).Append(" = adapter;\n        }\n");

        foreach (var member in members)
        {
            text.Append('\n');
            WriteMember(text, member, setup);
        }

        text.Append("    }\n");
    }

    private static void WriteMember(StringBuilder text, MockMemberModel member, bool setup)
    {
        var result = member.ReturnKind == ReturnKind.Void
            ? (setup ? Runtime + "MockSetup" : Runtime + "CallCount")
            : (setup ? Runtime + "MockSetup<" + MockEmitter.Render(member.Type!) + ">" : Runtime + "CallCount");
        var parameters = string.Join(", ", member.Parameters.Select(p => Runtime + "Arg<" + MockEmitter.Render(p.Type) + "> " + MockEmitter.Escape(p.Name)));
        var arguments = member.Parameters.Count == 0
            ? "global::System.Array.Empty<object?>()"
            : "new object?[] { " + string.Join(", ", member.Parameters.Select(p => MockEmitter.Escape(p.Name))) + " }";
        var typeArguments = member.TypeParameters.Count == 0
            ? "null"
            : "new global::System.Type[] { " + string.Join(", ", member.TypeParameters.Select(p => "typeof(" + MockEmitter.Escape(p.Name) + ")")) + " }";

        var memberName = member.Kind == MemberKind.Method ? member.Name : "get_" + member.Name;
        var call = Adapter + "." + (!setup ? "CountCalls" : member.ReturnKind == ReturnKind.Void ? "SetupVoidCall" : "SetupCall<" + MockEmitter.Render(member.Type!) + ">") +
                   "(" + member.Id + ", " + MockEmitter.Literal(memberName) + ", " + typeArguments + ", " + arguments + ")";

        text.Append("        public ").Append(result).Append(' ');
        switch (member.Kind)
        {
            case MemberKind.Method:
                text.Append(MockEmitter.Escape(member.Name));
                if (member.TypeParameters.Count > 0)
                {
                    text.Append('<').Append(string.Join(", ", member.TypeParameters.Select(p => MockEmitter.Escape(p.Name)))).Append('>');
                }

                text.Append('(').Append(parameters).Append(')');
                foreach (var parameter in member.TypeParameters)
                {
                    if (parameter.Primary is "class" or "struct")
                    {
                        text.Append("\n            where ").Append(MockEmitter.Escape(parameter.Name)).Append(" : ").Append(parameter.Primary);
                    }
                }

                break;
            case MemberKind.Indexer:
                text.Append("this[").Append(parameters).Append(']');
                break;
            default:
                text.Append(MockEmitter.Escape(member.Name));
                break;
        }

        text.Append("\n            => ").Append(call).Append(";\n");
    }

    /// <summary>The members the surface writes, in model order, without those whose C# signature an earlier member already has.</summary>
    private static List<MockMemberModel> SurfaceMembers(MockModel model)
    {
        var names = new Dictionary<string, MemberKind>();
        var signatures = new HashSet<string>();
        var result = new List<MockMemberModel>();
        foreach (var member in model.Members)
        {
            if (member.Kind == MemberKind.Event || member.Unsupported != null || member.IsUnsafe ||
                (member.Kind != MemberKind.Method && !member.HasGetter))
            {
                continue;
            }

            var name = member.Kind == MemberKind.Indexer ? "Item" : member.Name;
            if (names.TryGetValue(name, out var kind) && (kind != member.Kind || kind == MemberKind.Property))
            {
                continue;
            }

            if (!signatures.Add(member.SurfaceKey))
            {
                continue;
            }

            names[name] = member.Kind;
            result.Add(member);
        }

        return result;
    }

    private static string Constraint(TypeParameterModel parameter)
    {
        var parts = new List<string>();
        if (parameter.Primary != null)
        {
            parts.Add(parameter.Primary);
        }

        parts.AddRange(parameter.Types.Select(MockEmitter.Render));
        if (parameter.HasConstructor)
        {
            parts.Add("new()");
        }

        return parts.Count == 0 ? "" : "where " + MockEmitter.Escape(parameter.Name) + " : " + string.Join(", ", parts);
    }

    private static string BaseName(MockModel model) =>
        model.MockName.EndsWith(MockSuffix, System.StringComparison.Ordinal)
            ? model.MockName.Substring(0, model.MockName.Length - MockSuffix.Length)
            : model.MockName;
}
