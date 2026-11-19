using System.Linq;
using System.Threading;
using CSharpAuthor.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VerificationGlue.Shared;

namespace VerificationGlue.Generator;

/// <summary>
/// Tier 2: one interceptor per call to a <c>VerifySyntax</c> method, such as <c>Verify</c>,
/// <c>Check</c>, <c>Throws</c> or <c>Single</c>, reporting the file, the line and a message for the shape of the call, all
/// fixed at compile time. Tier 3, when the <c>VerificationGlueRewrite</c> property is true: a
/// <c>__Verified</c> copy of each eligible method that reports captured values, and interceptors
/// that send direct calls of those methods to their copies.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class VerificationGlueGenerator : IIncrementalGenerator
{
    public const string CallsStep = "Calls";
    public const string MethodsStep = "Methods";
    public const string RedirectsStep = "Redirects";
    public const string ProjectDirectoryProperty = "build_property.ProjectDir";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var projectDirectory = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue(ProjectDirectoryProperty, out var value) ? value : "");
        var rewrite = context.AnalyzerConfigOptionsProvider.Select(static (options, _) => VerifyRewriteRules.IsEnabled(options.GlobalOptions));

        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => VerifyCalls.IsCandidate(node), static (syntax, token) => ToCall(syntax, token))
            .Where(static call => call is not null)
            .Select(static (call, _) => call!)
            .WithTrackingName(CallsStep);

        // A tier 3 copy calls the comparer of each VerifyEquivalent call in it, so the comparers
        // come from every call, including those that get no interceptor.
        var equivalence = calls
            .Where(static call => call.Equivalence is not null)
            .Select(static (call, _) => call.Equivalence!)
            .Collect();
        context.RegisterSourceOutput(equivalence, static (output, models) =>
        {
            if (EquivalenceEmitter.Emit(models) is { } text)
            {
                output.AddSource(EquivalenceEmitter.HintName, text);
            }
        });

        var files = calls.Collect().Combine(projectDirectory).Combine(rewrite).SelectMany(static (pair, _) =>
            pair.Left.Left
                .Where(call => !(pair.Right && call.InRunnerCopy))
                .GroupBy(call => call.FilePath)
                .Select(group => new FileCalls(
                    group.Key,
                    pair.Left.Right,
                    new EquatableArray<CallModel>(group.OrderBy(call => call.Line).ThenBy(call => call.Column).ToArray()))));

        context.RegisterSourceOutput(files, static (output, file) =>
        {
            output.AddSource(InterceptorEmitter.HintName(file.FilePath), InterceptorEmitter.Emit(file));
            if (file.Calls.Any(LambdaEmitter.IsLambda))
            {
                output.AddSource(LambdaEmitter.HintName(file.FilePath), LambdaEmitter.Emit(file));
            }
        });

        var verifyMethods = context.SyntaxProvider.ForAttributeWithMetadataName(
            "VerificationGlue.VerifyAttribute",
            static (node, _) => node is MethodDeclarationSyntax,
            static (syntax, token) => ToMethod(syntax.TargetNode, syntax.SemanticModel, runnerOnly: false, token));
        var runnerMethods = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is MethodDeclarationSyntax { AttributeLists.Count: > 0 },
            static (syntax, token) => ToMethod(syntax.Node, syntax.SemanticModel, runnerOnly: true, token));

        RegisterMethods(context, verifyMethods, rewrite, projectDirectory);
        RegisterMethods(context, runnerMethods, rewrite, projectDirectory);

        var redirects = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is InvocationExpressionSyntax, static (syntax, token) => ToRedirect(syntax, token))
            .Where(static redirect => redirect is not null)
            .Select(static (redirect, _) => redirect!)
            .WithTrackingName(RedirectsStep);

        var redirectFiles = redirects.Collect().Combine(rewrite).SelectMany(static (pair, _) =>
            pair.Right
                ? pair.Left.GroupBy(redirect => redirect.FilePath).Select(group => new FileRedirects(
                    group.Key,
                    new EquatableArray<Redirect>(group.OrderBy(redirect => redirect.Line).ThenBy(redirect => redirect.Column).ToArray())))
                : Enumerable.Empty<FileRedirects>());

        context.RegisterSourceOutput(redirectFiles, static (output, file) =>
            output.AddSource(InterceptorEmitter.HintName(file.FilePath).Replace(".Interceptors.g.cs", ".Redirects.g.cs"), CopyEmitter.Redirects(file)));
    }

    private static void RegisterMethods(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MethodModel?> methods,
        IncrementalValueProvider<bool> rewrite,
        IncrementalValueProvider<string> projectDirectory)
    {
        var enabled = methods
            .Where(static method => method is not null)
            .Select(static (method, _) => method!)
            .WithTrackingName(MethodsStep)
            .Combine(rewrite)
            .Where(static pair => pair.Right)
            .Select(static (pair, _) => pair.Left)
            .Combine(projectDirectory);

        context.RegisterSourceOutput(enabled, static (output, pair) =>
            output.AddSource(pair.Left.HintName, CopyEmitter.Copy(pair.Left, pair.Right)));
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

        var span = invocation.GetLocation().GetLineSpan();
        string? message = null;
        LambdaModel? lambda = null;
        EquivalenceModel? equivalence = null;
        if (kind is CallKind.VerifyEquivalent)
        {
            equivalence = EquivalenceBuilder.Build(method, syntax.SemanticModel.Compilation);
        }
        else if (kind is CallKind.VerifyLambda or CallKind.CheckLambda)
        {
            if (VerifyCalls.LambdaBody(VerifyCalls.Argument(invocation, VerifyCalls.ConditionParameter)) is { } body)
            {
                message = Shapes.Condition(body, syntax.SemanticModel, token);
            }

            var site = (span.StartLinePosition.Line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" +
                       (span.StartLinePosition.Character + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            lambda = LambdaBuilder.Build(invocation, kind, syntax.SemanticModel, site, token);
        }
        else if (kind is CallKind.Verify or CallKind.VerifyWithMessage or CallKind.Check)
        {
            if (VerifyCalls.Argument(invocation, VerifyCalls.ConditionParameter) is { } condition)
            {
                message = Shapes.Condition(condition, syntax.SemanticModel, token);
            }
        }
        else if (kind is CallKind.Throws or CallKind.ThrowsAsync or CallKind.ThrowsExactly or CallKind.ThrowsExactlyAsync &&
                 VerifyCalls.Argument(invocation, VerifyCalls.ActionParameter) is { } action)
        {
            message = Shapes.Throws(action, method.TypeArguments[0], kind is CallKind.ThrowsExactly or CallKind.ThrowsExactlyAsync);
        }

        var inRunnerCopy = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is { } declaration &&
            syntax.SemanticModel.GetDeclaredSymbol(declaration, token) is { } enclosing &&
            VerifyRewriteRules.HasRewriteAttribute(enclosing, out var runnerCallsCopy) && runnerCallsCopy &&
            VerifyRewriteRules.WillRewrite(enclosing);

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
            message,
            inRunnerCopy,
            lambda,
            equivalence);
    }

    /// <summary>
    /// The copy of a method that tier 3 rewrites. A method with both <c>[Verify]</c> and an
    /// attribute marked <c>[VerifyRewrite]</c> is taken by the second provider only.
    /// </summary>
    private static MethodModel? ToMethod(SyntaxNode node, SemanticModel model, bool runnerOnly, CancellationToken token)
    {
        if (node is not MethodDeclarationSyntax declaration ||
            model.GetDeclaredSymbol(declaration, token) is not { } method ||
            !VerifyRewriteRules.HasRewriteAttribute(method, out var runnerCallsCopy) ||
            runnerOnly != runnerCallsCopy ||
            !VerifyRewriteRules.WillRewrite(method))
        {
            return null;
        }

        return CopyBuilder.Build(declaration, method, model, token);
    }

    private static Redirect? ToRedirect(GeneratorSyntaxContext syntax, CancellationToken token)
    {
        var invocation = (InvocationExpressionSyntax)syntax.Node;
        if (syntax.SemanticModel.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol target ||
            VerifyCalls.KindOf(target) is not null ||
            !VerifyRewriteRules.WillRewrite(target) ||
            syntax.SemanticModel.GetInterceptableLocation(invocation, token) is not { } location)
        {
            return null;
        }

        var declared = target.ReducedFrom ?? target;
        var kind = target.ReducedFrom is not null ? RedirectKind.Extension : target.IsStatic ? RedirectKind.Static : RedirectKind.Instance;
        var span = invocation.GetLocation().GetLineSpan();
        return new Redirect(
            invocation.SyntaxTree.FilePath,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            location.Version,
            location.Data,
            kind,
            declared.ContainingType.ToDisplayString(TypeNames.Format),
            declared.Name,
            declared.ReturnsVoid ? null : declared.ReturnType.ToDisplayString(TypeNames.Format),
            new EquatableArray<Parameter>(declared.Parameters.Select(parameter => new Parameter(
                parameter.Type.ToDisplayString(TypeNames.Format),
                parameter.Name,
                parameter.RefKind switch
                {
                    RefKind.Ref => "ref",
                    RefKind.Out => "out",
                    RefKind.In => "in",
                    RefKind.RefReadOnlyParameter => "ref readonly",
                    _ => "",
                })).ToArray()));
    }
}

internal static class TypeNames
{
    public static readonly SymbolDisplayFormat Format = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
}
