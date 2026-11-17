using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace VerificationGlue.Generator;

/// <summary>Reports a <c>Verify</c> condition that is constant, changes state, or compares floating-point values exactly.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VerifyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Diagnostics.ConstantCondition,
        Diagnostics.SideEffect,
        Diagnostics.FloatingPointEquality);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (VerifyCalls.KindOf(invocation.TargetMethod) is not (CallKind.Verify or CallKind.VerifyWithMessage))
        {
            return;
        }

        IOperation? condition = null;
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name == VerifyCalls.ConditionParameter)
            {
                condition = argument.Value;
            }
        }

        if (condition is null)
        {
            return;
        }

        if (condition.ConstantValue is { HasValue: true, Value: bool constant })
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.ConstantCondition, condition.Syntax.GetLocation(), condition.Syntax.ToString(), constant ? "true" : "false"));
            return;
        }

        foreach (var operation in Evaluated(condition))
        {
            switch (operation)
            {
                case IAssignmentOperation or IIncrementOrDecrementOperation:
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.SideEffect, operation.Syntax.GetLocation(), operation.Syntax.ToString()));
                    break;
                case IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals, OperatorMethod: null } binary
                    when (FloatingPointName(binary.LeftOperand.Type) ?? FloatingPointName(binary.RightOperand.Type)) is { } name:
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.FloatingPointEquality,
                        binary.Syntax.GetLocation(),
                        binary.Syntax.ToString(),
                        name,
                        binary.OperatorKind == BinaryOperatorKind.Equals ? "==" : "!="));
                    break;
            }
        }
    }

    /// <summary>The operation and its descendants, without the bodies of lambdas and local functions, which run elsewhere.</summary>
    private static IEnumerable<IOperation> Evaluated(IOperation root)
    {
        var pending = new Stack<IOperation>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var operation = pending.Pop();
            yield return operation;
            foreach (var child in operation.ChildOperations)
            {
                if (child is not (IAnonymousFunctionOperation or ILocalFunctionOperation))
                {
                    pending.Push(child);
                }
            }
        }
    }

    private static string? FloatingPointName(ITypeSymbol? type) => type?.SpecialType switch
    {
        SpecialType.System_Double => "double",
        SpecialType.System_Single => "float",
        _ => null,
    };
}
