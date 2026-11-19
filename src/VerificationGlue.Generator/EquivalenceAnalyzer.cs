using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace VerificationGlue.Generator;

/// <summary>Reports a <c>VerifyEquivalent</c> option that the generated comparer does not apply.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EquivalenceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Diagnostics.EquivalenceOption);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (VerifyCalls.KindOf(invocation.TargetMethod) is not CallKind.VerifyEquivalent ||
            invocation.Syntax is not InvocationExpressionSyntax syntax ||
            VerifyCalls.Options(syntax, invocation.TargetMethod) is not { } options ||
            invocation.SemanticModel is not { } model)
        {
            return;
        }

        foreach (var problem in EquivalenceBuilder.Problems(invocation.TargetMethod, context.Compilation, options, model))
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EquivalenceOption, problem.Syntax.GetLocation(), problem.Syntax.ToString(), problem.Reason));
        }
    }
}
