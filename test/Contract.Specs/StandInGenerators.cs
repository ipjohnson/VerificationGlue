using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VerificationGlue.Shared;

namespace Contract.Specs;

/// <summary>
/// Adds <c>Calls()</c> to <c>ICalculator</c> the way the typed mock surface of issue #16 will: as
/// a generated extension method, which no other generator can see. Its <c>Count()</c> returns a
/// struct, as a <c>CallCount</c> is, so the name also matches <c>Enumerable.Count</c>.
/// </summary>
public sealed class TypedCallsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterSourceOutput(context.CompilationProvider, static (output, _) => output.AddSource("ICalculator.Calls.g.cs", """
            namespace Contract
            {
                public sealed class ICalculator_Calls
                {
                    private readonly global::VerificationGlue.Mocks.IMock _mock;

                    public ICalculator_Calls(ICalculator mock) => _mock = (global::VerificationGlue.Mocks.IMock)mock;

                    public int Add(int a, int b)
                    {
                        var count = 0;
                        foreach (var call in _mock.Adapter.ReceivedCalls())
                        {
                            if (call.MemberName == "Add" && Equals(call.Arguments[0], a) && Equals(call.Arguments[1], b))
                            {
                                count++;
                            }
                        }

                        return count;
                    }

                    public CallTally Count() => new(_mock.Adapter.ReceivedCalls().Count);
                }

                public readonly struct CallTally(int count)
                {
                    public int Value { get; } = count;

                    public static bool operator ==(CallTally left, int right) => left.Value == right;

                    public static bool operator !=(CallTally left, int right) => left.Value != right;

                    public override bool Equals(object? obj) => obj is CallTally other && other.Value == Value;

                    public override int GetHashCode() => Value;

                    public override string ToString() => Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
                }

                public static class ICalculator_CallsExtensions
                {
                    public static ICalculator_Calls Calls(this ICalculator mock) => new(mock);
                }
            }
            """));
}

/// <summary>
/// The runner's side of the contract, with the decision <c>TestGenerator</c> makes: for each method
/// with an attribute marked <c>[VerifyRewrite]</c>, a registry entry that calls the <c>__Verified</c>
/// copy when rewriting is on and <c>WillRewrite</c> is true, and the original otherwise. It cannot see
/// the copies, so it relies on the shared rules alone. <c>TestGenerator</c>'s own registrations are
/// run in <c>test/VerificationGlue.Tests.Generator.Specs/RewriteSpecs.cs</c>.
/// </summary>
public sealed class RunnerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rewrite = context.AnalyzerConfigOptionsProvider.Select(static (options, _) => VerifyRewriteRules.IsEnabled(options.GlobalOptions));
        var tests = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntax, token) => syntax.SemanticModel.GetDeclaredSymbol((MethodDeclarationSyntax)syntax.Node, token) is IMethodSymbol method &&
                                          VerifyRewriteRules.HasRewriteAttribute(method, out var runner) && runner &&
                                          !method.IsGenericMethod && method.Parameters.Length == 0
                    ? new TestEntry(
                        method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        method.ContainingType.ToDisplayString() + "." + method.Name,
                        method.Name,
                        VerifyRewriteRules.WillRewrite(method),
                        method.IsStatic,
                        !method.ReturnsVoid)
                    : null)
            .Where(static entry => entry is not null)
            .Collect()
            .Combine(rewrite);

        context.RegisterSourceOutput(tests, static (output, pair) =>
        {
            var text = new StringBuilder("namespace Contract.Generated\n{\n    public static class Runner\n    {\n");
            text.Append("        public static global::System.Collections.Generic.Dictionary<string, (string Target, global::System.Func<global::System.Threading.Tasks.Task> Run)> Tests => new()\n        {\n");
            foreach (var entry in pair.Left.OrderBy(entry => entry!.Id, StringComparer.Ordinal))
            {
                var target = entry!.Name + (pair.Right && entry.WillRewrite ? VerifyRewriteRules.Suffix : "");
                var call = (entry.IsStatic ? entry.Type : "new " + entry.Type + "()") + "." + target + "()";
                var run = entry.ReturnsTask ? $"async () => await {call}" : $"() => {{ {call}; return global::System.Threading.Tasks.Task.CompletedTask; }}";
                text.Append($"            [\"{entry.Id}\"] = (\"{target}\", {run}),\n");
            }

            output.AddSource("Runner.g.cs", text.Append("        };\n    }\n}\n").ToString());
        });
    }

    private sealed record TestEntry(string Type, string Id, string Name, bool WillRewrite, bool IsStatic, bool ReturnsTask);
}
