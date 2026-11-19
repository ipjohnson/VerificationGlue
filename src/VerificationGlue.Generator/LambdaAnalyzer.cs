using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using VerificationGlue.Shared;

namespace VerificationGlue.Generator;

/// <summary>
/// Reports <c>Verify(condition)</c> and <c>Check(condition)</c> in a test method when
/// <c>Verify(() =&gt; condition)</c> would compile and lose nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LambdaAnalyzer : DiagnosticAnalyzer
{
    public const string SeverityProperty = "build_property.VerificationGlueLambdaSeverity";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Diagnostics.PreferLambda);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var options = start.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            if (Severity(options) is not { } severity)
            {
                return;
            }

            var rewrite = VerifyRewriteRules.IsEnabled(options);
            start.RegisterOperationAction(operation => AnalyzeInvocation(operation, severity, rewrite), OperationKind.Invocation);
        });
    }

    /// <summary>The severity that <c>VerificationGlueLambdaSeverity</c> sets, or null for <c>none</c>.</summary>
    public static DiagnosticSeverity? Severity(AnalyzerConfigOptions options)
    {
        options.TryGetValue(SeverityProperty, out var value);
        return value?.Trim().ToLowerInvariant() switch
        {
            "none" => null,
            "warning" => DiagnosticSeverity.Warning,
            "error" => DiagnosticSeverity.Error,
            _ => DiagnosticSeverity.Info,
        };
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, DiagnosticSeverity severity, bool rewrite)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (VerifyCalls.KindOf(invocation.TargetMethod) is not (CallKind.Verify or CallKind.Check) ||
            invocation.Parent is not IExpressionStatementOperation ||
            invocation.Arguments.Any(argument => argument.ArgumentKind == ArgumentKind.Explicit && argument.Parameter?.Name == VerifyCalls.ExpressionParameter) ||
            invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == VerifyCalls.ConditionParameter)?.Value is not { } condition ||
            condition.ConstantValue.HasValue ||
            invocation.Syntax.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } declaration ||
            invocation.SemanticModel is not { } model ||
            model.GetDeclaredSymbol(declaration, context.CancellationToken) is not IMethodSymbol method ||
            !IsTestMethod(method) ||
            (rewrite && VerifyRewriteRules.WillRewrite(method)) ||
            LosesSomething(condition, declaration, invocation.Syntax.Span.End, model, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.PreferLambda,
            condition.Syntax.GetLocation(),
            severity,
            additionalLocations: null,
            properties: null,
            invocation.TargetMethod.Name,
            condition.Syntax.ToString()));
    }

    /// <summary>
    /// True for a method with an attribute named <c>Fact</c>, <c>Theory</c> or <c>Verify</c>, or
    /// a name that starts with <c>Test</c> or ends with <c>Test</c> or <c>TestMethod</c>, in any namespace.
    /// </summary>
    public static bool IsTestMethod(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (name is null)
            {
                continue;
            }

            if (name.EndsWith("Attribute", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - "Attribute".Length);
            }

            if (name is "Fact" or "Theory" or "Verify" ||
                name.StartsWith("Test", StringComparison.Ordinal) ||
                name.EndsWith("Test", StringComparison.Ordinal) ||
                name.EndsWith("TestMethod", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <c>() =&gt; condition</c> would not compile, or would lose what <c>Verify(bool)</c>
    /// gives the code after the call: an <c>out var</c> the condition declares, a value assigned through
    /// <c>out</c>, or a null state that <c>[DoesNotReturnIf(false)]</c> narrows.
    /// </summary>
    /// <remarks>
    /// A pattern variable is not definitely assigned after <c>Verify(bool)</c>, so later code cannot read it.
    /// A null state counts for any part that is maybe-null in the condition and named again later,
    /// which also covers <c>?.</c>, dereferences and calls such as <c>string.IsNullOrEmpty</c>.
    /// </remarks>
    private static bool LosesSomething(IOperation condition, SyntaxNode method, int callEnd, SemanticModel model, OperationAnalysisContext context)
    {
        var usedLater = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var operation in Evaluated(condition))
        {
            switch (operation)
            {
                case IAwaitOperation:
                case IAssignmentOperation or IIncrementOrDecrementOperation:
                case IArgumentOperation { Parameter.RefKind: RefKind.Out, Value: not IDeclarationExpressionOperation }:
                case IInstanceReferenceOperation { Type.IsValueType: true }:
                case ILocalReferenceOperation { Local: var local } when local.RefKind != RefKind.None || local.IsFixed || local.Type.IsRefLikeType:
                case IParameterReferenceOperation { Parameter: var parameter } when parameter.RefKind != RefKind.None || parameter.Type.IsRefLikeType:
                    return true;
                case ILocalReferenceOperation { IsDeclaration: true, Local: var declared }:
                    usedLater.Add(declared);
                    break;
                case ILocalReferenceOperation or IParameterReferenceOperation or IFieldReferenceOperation or IPropertyReferenceOperation
                    when model.GetTypeInfo(operation.Syntax, context.CancellationToken).Nullability.FlowState == NullableFlowState.MaybeNull:
                    usedLater.Add(Referenced(operation)!);
                    break;
            }
        }

        if (usedLater.Count == 0)
        {
            return false;
        }

        foreach (var name in method.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (name.SpanStart >= callEnd &&
                model.GetSymbolInfo(name, context.CancellationToken).Symbol is { } symbol &&
                usedLater.Contains(symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static ISymbol? Referenced(IOperation operation) => operation switch
    {
        ILocalReferenceOperation local => local.Local,
        IParameterReferenceOperation parameter => parameter.Parameter,
        IFieldReferenceOperation field => field.Field,
        IPropertyReferenceOperation property => property.Property,
        _ => null,
    };

    /// <summary>The operation and its descendants, without the bodies of lambdas and local functions.</summary>
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
}
