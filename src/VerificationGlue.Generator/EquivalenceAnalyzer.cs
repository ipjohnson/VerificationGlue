using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace VerificationGlue.Generator;

/// <summary>
/// Reports a <c>VerifyEquivalent</c> or <c>CheckEquivalent</c> option that the generated comparer
/// does not apply, and a type that the comparer reaches but cannot walk.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EquivalenceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Diagnostics.EquivalenceOption, Diagnostics.EquivalenceUnwalked);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (VerifyCalls.KindOf(invocation.TargetMethod) is not { } kind ||
            !VerifyCalls.IsEquivalent(kind) ||
            invocation.Syntax is not InvocationExpressionSyntax syntax ||
            invocation.SemanticModel is not { } model)
        {
            return;
        }

        var name = invocation.TargetMethod.Name;
        var options = VerifyCalls.Options(syntax, invocation.TargetMethod);
        if (options is not null)
        {
            foreach (var problem in EquivalenceBuilder.Problems(invocation.TargetMethod, context.Compilation, options, model))
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EquivalenceOption, problem.Syntax.GetLocation(), name, problem.Syntax.ToString(), problem.Reason));
            }
        }

        foreach (var type in EquivalenceBuilder.Unwalked(invocation.TargetMethod, context.Compilation, options, model))
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EquivalenceUnwalked, syntax.GetLocation(), name, type.Type, type.Fallback, type.Reason));
        }
    }
}
