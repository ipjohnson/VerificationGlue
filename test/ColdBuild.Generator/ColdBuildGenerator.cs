using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CSharpAuthor.Collections;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ColdBuild.Generator;

/// <summary>
/// Tier 2 interceptors for every <c>Verify</c> call, and with <c>ColdBuildRewrite</c> set to
/// <c>true</c>, tier 3 copies of eligible methods and redirects of direct calls to them.
/// </summary>
/// <remarks>
/// Only top-level statements of a copied body are rewritten. A <c>Verify</c> call nested in a block
/// or a lambda is copied as written and runs as tier 1. The benchmark's tests have flat bodies.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ColdBuildGenerator : IIncrementalGenerator
{
    public const string RewriteProperty = "build_property.ColdBuildRewrite";
    public const string CallSitesStep = "CallSites";
    public const string MethodsStep = "Methods";
    public const string RedirectsStep = "Redirects";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rewrite = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue(RewriteProperty, out var value) && value.Equals("true", System.StringComparison.OrdinalIgnoreCase));

        var callSites = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => IsNamed(node, "Verify"), static (syntax, token) => ToCallSite(syntax, token))
            .Where(static site => site is not null)
            .Select(static (site, _) => site!)
            .WithTrackingName(CallSitesStep);

        var callFiles = callSites.Collect().Combine(rewrite).SelectMany(static (pair, _) =>
            pair.Left.Where(site => !(pair.Right && site.InRunnerCopy))
                .GroupBy(site => site.FilePath)
                .Select(group => new FileCalls(group.Key, new EquatableArray<CallSite>(group.ToArray()))));

        context.RegisterSourceOutput(callFiles, static (output, file) =>
            output.AddSource(Emitter.HintName(file.FilePath, "Interceptors"), Emitter.Interceptors(file)));

        var verifyMethods = context.SyntaxProvider
            .ForAttributeWithMetadataName("VerificationGlue.VerifyAttribute", static (node, _) => node is MethodDeclarationSyntax, static (syntax, token) => ToMethod(syntax.TargetNode, syntax.SemanticModel, token));
        var runnerMethods = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is MethodDeclarationSyntax { AttributeLists.Count: > 0 }, static (syntax, token) => ToMethod(syntax.Node, syntax.SemanticModel, token, runnerOnly: true));

        RegisterMethods(context, verifyMethods, rewrite);
        RegisterMethods(context, runnerMethods, rewrite);

        var redirects = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is InvocationExpressionSyntax && !IsNamed(node, "Verify"), static (syntax, token) => ToRedirect(syntax, token))
            .Where(static redirect => redirect is not null)
            .Select(static (redirect, _) => redirect!)
            .WithTrackingName(RedirectsStep);

        var redirectFiles = redirects.Collect().Combine(rewrite).SelectMany(static (pair, _) =>
            pair.Right
                ? pair.Left.GroupBy(redirect => redirect.FilePath).Select(group => new FileRedirects(group.Key, new EquatableArray<Redirect>(group.ToArray())))
                : []);

        context.RegisterSourceOutput(redirectFiles, static (output, file) =>
            output.AddSource(Emitter.HintName(file.FilePath, "Redirects"), Emitter.Redirects(file)));
    }

    private static void RegisterMethods(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<MethodModel?> methods, IncrementalValueProvider<bool> rewrite)
    {
        var enabled = methods
            .Where(static method => method is not null)
            .Select(static (method, _) => method!)
            .WithTrackingName(MethodsStep)
            .Combine(rewrite)
            .Where(static pair => pair.Right)
            .Select(static (pair, _) => pair.Left);

        context.RegisterSourceOutput(enabled, static (output, method) =>
            output.AddSource(Emitter.HintName(method.FilePath, string.Join(".", method.Types.Select(type => type.Name)) + "." + method.Name + "." + method.Line + ".Verified"),Emitter.Copy(method)));
    }

    private static bool IsNamed(SyntaxNode node, string name) =>
        node is InvocationExpressionSyntax invocation && invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == name,
            _ => false,
        };

    private static CallSite? ToCallSite(GeneratorSyntaxContext syntax, CancellationToken token)
    {
        var invocation = (InvocationExpressionSyntax)syntax.Node;
        if (syntax.SemanticModel.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol target || !Eligibility.IsVerifyCall(target) ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            syntax.SemanticModel.GetInterceptableLocation(invocation, token) is not { } location)
        {
            return null;
        }

        var inRunnerCopy = syntax.SemanticModel.GetEnclosingSymbol(invocation.SpanStart, token) is IMethodSymbol { MethodKind: MethodKind.Ordinary } method &&
            Eligibility.HasRewriteAttribute(method, out var runnerCallsCopy) && runnerCallsCopy && Eligibility.WillRewrite(method);
        var condition = invocation.ArgumentList.Arguments[0].Expression;
        var span = invocation.GetLocation().GetLineSpan();
        return new CallSite(
            span.Path,
            span.StartLinePosition.Line + 1,
            location.Version,
            location.Data,
            Shapes.OneLine(condition),
            Shapes.Message(condition, syntax.SemanticModel),
            inRunnerCopy);
    }

    private static MethodModel? ToMethod(SyntaxNode node, SemanticModel model, CancellationToken token, bool runnerOnly = false)
    {
        if (node is not MethodDeclarationSyntax { Body: { } body } declaration ||
            model.GetDeclaredSymbol(declaration, token) is not { } method ||
            !Eligibility.HasRewriteAttribute(method, out var runnerCallsCopy) ||
            (runnerOnly && !runnerCallsCopy) ||
            (!runnerOnly && runnerCallsCopy) ||
            !Eligibility.WillRewrite(method))
        {
            // A method with both kinds of attribute is taken by the second provider only.
            return null;
        }

        var root = (CompilationUnitSyntax)declaration.SyntaxTree.GetRoot(token);
        var usings = root.Usings.Select(directive => directive.ToString()).ToList();
        string? ns = null;
        var types = new List<TypeShell>();
        foreach (var ancestor in declaration.Ancestors().Reverse())
        {
            switch (ancestor)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    ns = ns is null ? namespaceDeclaration.Name.ToString() : ns + "." + namespaceDeclaration.Name;
                    usings.AddRange(namespaceDeclaration.Usings.Select(directive => directive.ToString()));
                    break;
                case TypeDeclarationSyntax type:
                    types.Add(new TypeShell(type is RecordDeclarationSyntax record ? "record " + record.ClassOrStructKeyword.ValueText : type.Keyword.ValueText, type.Identifier.ValueText));
                    break;
            }
        }

        var path = declaration.SyntaxTree.FilePath;
        var statements = new List<Statement>();
        var site = 0;
        foreach (var statement in body.Statements)
        {
            var line = statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            if (statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation } &&
                invocation.ArgumentList.Arguments.Count == 1 &&
                model.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol target && Eligibility.IsVerifyCall(target))
            {
                statements.Add(new Statement(line, "", new Rewriter(model, site++).Rewrite(invocation.ArgumentList.Arguments[0].Expression)));
            }
            else
            {
                statements.Add(new Statement(line, CallCopies(statement, model, token), null));
            }
        }

        return new MethodModel(
            path,
            declaration.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            new EquatableArray<string>(usings.ToArray()),
            ns,
            new EquatableArray<TypeShell>(types.ToArray()),
            method.Name,
            method.IsStatic,
            method.IsAsync,
            method.ReturnsVoid ? null : method.ReturnType.GetTypeDefinition(),
            Parameters(method),
            new EquatableArray<Statement>(statements.ToArray()));
    }

    /// <summary>The statement's text, with each direct call to a rewritten method sent to its copy.</summary>
    private static string CallCopies(StatementSyntax statement, SemanticModel model, CancellationToken token)
    {
        var text = statement.ToString();
        var renames = new List<int>();
        foreach (var invocation in statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = invocation.Expression switch
            {
                IdentifierNameSyntax identifier => identifier,
                MemberAccessExpressionSyntax access => access.Name,
                _ => null,
            };
            if (name is not null && model.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol target && !Eligibility.IsVerifyCall(target) && Eligibility.WillRewrite(target))
            {
                renames.Add(name.Identifier.Span.End - statement.SpanStart);
            }
        }

        foreach (var end in renames.OrderByDescending(end => end))
        {
            text = text.Insert(end, Eligibility.Suffix);
        }

        return text;
    }

    private static Redirect? ToRedirect(GeneratorSyntaxContext syntax, CancellationToken token)
    {
        var invocation = (InvocationExpressionSyntax)syntax.Node;
        if (syntax.SemanticModel.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol target ||
            target.IsExtensionMethod || !Eligibility.WillRewrite(target) ||
            syntax.SemanticModel.GetInterceptableLocation(invocation, token) is not { } location)
        {
            return null;
        }

        return new Redirect(
            invocation.SyntaxTree.FilePath,
            location.Version,
            location.Data,
            target.ContainingType.GetTypeDefinition(),
            target.IsStatic,
            target.Name,
            target.ReturnsVoid ? null : target.ReturnType.GetTypeDefinition(),
            Parameters(target));
    }

    private static EquatableArray<Parameter> Parameters(IMethodSymbol method) =>
        new(method.Parameters.Select(parameter => new Parameter(
            parameter.Type.GetTypeDefinition(),
            parameter.Name,
            parameter.RefKind switch { RefKind.Ref => "ref", RefKind.Out => "out", RefKind.In => "in", _ => "" })).ToArray());
}
