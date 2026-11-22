using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace VerificationGlue.Generator.Specs;

public class OverloadSpecs
{
    [VerifyTest]
    [Row("Verify(x == 1)", "Verify", "condition")]
    [Row("Verify(x == 1, \"text\")", "Verify", "condition, message")]
    [Row("Verify(x == 1, Reason)", "Verify", "condition, message")]
    [Row("Verify(x == 1, reason)", "Verify", "condition, message")]
    [Row("Verify(x == 1, $\"x is {x}\")", "VerifyWithMessage", "condition, message")]
    [Row("Verify(x == 1, $\"text\")", "Verify", "condition, message")]
    [Row("Verify(condition: x == 1, message: $\"x is {x}\")", "VerifyWithMessage", "condition, message")]
    [Row("Verify(expression: \"the total\", condition: x == 1)", "Verify", "expression, condition")]
    [Row("Verify(x == 1, expression: \"the total\")", "Verify", "condition, expression")]
    [Row("Verify(x == 1, $\"x is {x}\", \"the total\")", "VerifyWithMessage", "condition, message, expression")]
    [Row("Verify(() => x == 1)", "VerifyLambda", "condition")]
    [Row("Verify(() => x == 1, \"the total\")", "VerifyLambda", "condition, expression")]
    [Row("Check(x == 1)", "Check", "condition")]
    [Row("Check(x == 1, \"text\")", "Check", "condition, message")]
    [Row("Check(x == 1, expression: \"the total\")", "Check", "condition, expression")]
    [Row("Check(() => x == 1)", "CheckLambda", "condition")]
    public void Each_call_shape_binds_its_arguments_to_the_named_parameters(string call, string kind, string parameters)
    {
        var compilation = GeneratorHarness.Compilation(("/repo/Project/Calls.cs", $$"""
            using static VerificationGlue.VerifySyntax;

            public static class Calls
            {
                private const string Reason = "text";

                public static void M(int x)
                {
                    var reason = "text";
                    {{call}};
                }
            }
            """));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.ToString()));

        var tree = compilation.SyntaxTrees.Single();
        var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var operation = Assert.IsAssignableFrom<IInvocationOperation>(compilation.GetSemanticModel(tree).GetOperation(invocation));

        Assert.Equal(kind, VerifyCalls.KindOf(operation.TargetMethod).ToString());
        Assert.Equal(
            parameters,
            string.Join(", ", operation.Arguments
                .Where(argument => argument.ArgumentKind != ArgumentKind.DefaultValue)
                .OrderBy(argument => argument.Syntax.SpanStart)
                .Select(argument => argument.Parameter!.Name)));
    }
}
