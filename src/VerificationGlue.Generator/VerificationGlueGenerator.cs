using System.Linq;
using System.Threading;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VerificationGlue.Generator;

/// <summary>
/// Tier 2: one interceptor per call to <c>Verify</c>, <c>Throws</c> or <c>ThrowsAsync</c>. Each
/// interceptor reports the file, the line and a message for the shape of the call, all fixed at
/// compile time.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class VerificationGlueGenerator : IIncrementalGenerator
{
    public const string CallsStep = "Calls";
    public const string ProjectDirectoryProperty = "build_property.ProjectDir";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var projectDirectory = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue(ProjectDirectoryProperty, out var value) ? value : "");

        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => VerifyCalls.IsCandidate(node), static (syntax, token) => ToCall(syntax, token))
            .Where(static call => call is not null)
            .Select(static (call, _) => call!)
            .WithTrackingName(CallsStep);

        var files = calls.Collect().Combine(projectDirectory).SelectMany(static (pair, _) =>
            pair.Left
                .GroupBy(call => call.FilePath)
                .Select(group => new FileCalls(
                    group.Key,
                    pair.Right,
                    new EquatableArray<CallModel>(group.OrderBy(call => call.Line).ThenBy(call => call.Column).ToArray()))));

        context.RegisterSourceOutput(files, static (output, file) =>
            output.AddSource(InterceptorEmitter.HintName(file.FilePath), InterceptorEmitter.Emit(file)));
    }

    private static CallModel? ToCall(GeneratorSyntaxContext syntax, CancellationToken token)
    {
        var invocation = (InvocationExpressionSyntax)syntax.Node;
        if (syntax.SemanticModel.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method ||
            VerifyCalls.KindOf(method) is not { } kind ||
            syntax.SemanticModel.GetInterceptableLocation(invocation, token) is not { } location)
        {
            return null;
        }

        string? message = null;
        if (kind is CallKind.Verify or CallKind.VerifyWithMessage)
        {
            if (VerifyCalls.Argument(invocation, VerifyCalls.ConditionParameter) is { } condition)
            {
                message = Shapes.Condition(condition, syntax.SemanticModel, token);
            }
        }
        else if (VerifyCalls.Argument(invocation, VerifyCalls.ActionParameter) is { } action)
        {
            message = Shapes.Throws(action, method.TypeArguments[0]);
        }

        var span = invocation.GetLocation().GetLineSpan();
        var mapped = invocation.GetLocation().GetMappedLineSpan();
        return new CallModel(
            invocation.SyntaxTree.FilePath,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            mapped.Path,
            mapped.StartLinePosition.Line + 1,
            location.Version,
            location.Data,
            kind,
            message);
    }
}
