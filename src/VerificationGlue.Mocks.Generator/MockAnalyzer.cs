using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace VerificationGlue.Mocks.Generator;

/// <summary>
/// Reports VGM0001 to VGM0005 at the code that causes them: the place that names a type to mock,
/// a misplaced <c>Arg</c> matcher, or a setup on a value that no mock call returned.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MockAnalyzer : DiagnosticAnalyzer
{
    private const string OpenGenericReason = "it is an open generic type. Name a closed type, such as typeof(IRepository<Order>)";

    private static readonly SymbolDisplayFormat MessageFormat = SymbolDisplayFormat.CSharpErrorMessageFormat;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MockDiagnostics.TypeCannotBeMocked,
        MockDiagnostics.MatcherWithoutPosition,
        MockDiagnostics.MemberCannotBeMocked,
        MockDiagnostics.TypeNotDiscovered,
        MockDiagnostics.SetupWithoutMockCall);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var state = new CompilationState(start.Compilation);
            start.RegisterOperationAction(operation => AnalyzeInvocation(operation, state), OperationKind.Invocation);
            start.RegisterSyntaxNodeAction(node => AnalyzeAttribute(node, state), SyntaxKind.Attribute);
            start.RegisterCompilationEndAction(end => state.ReportUndiscovered(end));
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, CompilationState state)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (MockDiscovery.IsMockOf(method))
        {
            var type = method.TypeArguments[0];
            if (type is ITypeParameterSymbol { DeclaringMethod: { } helper } parameter)
            {
                state.OpenSites.Add(new OpenSite(helper.OriginalDefinition, parameter.Ordinal, parameter.Name, invocation.Syntax.GetLocation()));
            }
            else
            {
                state.Discover(type, invocation.Syntax.GetLocation(), context.ReportDiagnostic);
            }
        }
        else if (method.IsGenericMethod && !method.IsDefinition && method.OriginalDefinition.Locations.Any(location => location.IsInSource))
        {
            state.GenericCalls.Add(new GenericCall(method.OriginalDefinition, method.TypeArguments, invocation.Syntax.GetLocation()));
        }

        if (MockDiscovery.ArgKindOf(method) is not null && !IsMockArgument(invocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(MockDiagnostics.MatcherWithoutPosition, invocation.Syntax.GetLocation(), invocation.Syntax.ToString()));
        }

        if (MockDiscovery.IsSetup(method) && invocation.Arguments.FirstOrDefault()?.Value is { } receiver && !IsMockCall(Unwrap(receiver)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MockDiagnostics.SetupWithoutMockCall,
                receiver.Syntax.GetLocation(),
                receiver.Syntax.ToString(),
                (method.ReducedFrom ?? method).Name));
        }
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, CompilationState state)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: { } attributeType })
        {
            return;
        }

        var name = attributeType.ToDisplayString();
        if (name == MockDiscovery.GenerateMockAttribute)
        {
            if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax typeOf &&
                context.SemanticModel.GetTypeInfo(typeOf.Type, context.CancellationToken).Type is { } type)
            {
                if (type is INamedTypeSymbol { IsUnboundGenericType: true })
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MockDiagnostics.TypeCannotBeMocked, attribute.GetLocation(), type.ToDisplayString(MessageFormat), OpenGenericReason));
                    return;
                }

                state.Discover(type, attribute.GetLocation(), context.ReportDiagnostic);
            }

            return;
        }

        if (name != MockDiscovery.MockAttribute || attribute.Parent?.Parent is not { } target)
        {
            return;
        }

        var symbol = target is FieldDeclarationSyntax field
            ? field.Declaration.Variables.Select(variable => context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)).FirstOrDefault()
            : context.SemanticModel.GetDeclaredSymbol(target, context.CancellationToken);
        if (MockDiscovery.TypeOfTarget(symbol) is { } targetType && !MockDiscovery.ContainsTypeParameter(targetType))
        {
            state.Discover(targetType, attribute.GetLocation(), context.ReportDiagnostic);
        }
    }

    /// <summary>True when the matcher is passed directly to a mock member, or assigned directly to a mock property.</summary>
    private static bool IsMockArgument(IOperation matcher)
    {
        var current = matcher;
        while (current.Parent is IConversionOperation { IsImplicit: true } conversion)
        {
            current = conversion;
        }

        return current.Parent switch
        {
            IArgumentOperation { Parameter: { } parameter } => MockDiscovery.IsMockableMember(parameter.ContainingSymbol),
            ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property } assignment when assignment.Value == current =>
                MockDiscovery.IsMockableMember(property.Property),
            _ => false,
        };
    }

    private static bool IsMockCall(IOperation operation) => operation switch
    {
        IInvocationOperation invocation => MockDiscovery.IsMockableMember(invocation.TargetMethod),
        IPropertyReferenceOperation property => MockDiscovery.IsMockableMember(property.Property),
        _ => false,
    };

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private sealed record OpenSite(IMethodSymbol Helper, int Ordinal, string Name, Location Location);

    private sealed record GenericCall(IMethodSymbol Definition, ImmutableArray<ITypeSymbol> TypeArguments, Location Location);

    /// <summary>What one compilation's analysis has seen, for VGM0004 at its end.</summary>
    private sealed class CompilationState(Compilation compilation)
    {
        private readonly ConcurrentDictionary<ITypeSymbol, MockModel> _models = new(SymbolEqualityComparer.Default);

        public ConcurrentBag<OpenSite> OpenSites { get; } = new();

        public ConcurrentBag<GenericCall> GenericCalls { get; } = new();

        /// <summary>Records a type the generator mocks, and reports why it, or one of its members, cannot be mocked.</summary>
        public void Discover(ITypeSymbol type, Location location, System.Action<Diagnostic> report)
        {
            if (MockDiscovery.Closed(type) is not { } closed)
            {
                return;
            }

            var model = _models.GetOrAdd(closed, key => MockModelBuilder.Build((INamedTypeSymbol)key, compilation));
            foreach (var diagnostic in model.Diagnostics)
            {
                report(Diagnostic.Create(MockDiagnostics.Find(diagnostic.Id), location, diagnostic.Arguments.ToArray()));
            }
        }

        public void ReportUndiscovered(CompilationAnalysisContext context)
        {
            if (OpenSites.IsEmpty)
            {
                return;
            }

            var reported = new HashSet<(Location, string)>();
            foreach (var call in GenericCalls)
            {
                foreach (var site in OpenSites)
                {
                    if (!SymbolEqualityComparer.Default.Equals(call.Definition, site.Helper) ||
                        MockDiscovery.Closed(call.TypeArguments[site.Ordinal]) is not { } argument ||
                        _models.ContainsKey(argument))
                    {
                        continue;
                    }

                    var name = argument.ToDisplayString(MessageFormat);
                    if (reported.Add((site.Location, name)))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MockDiagnostics.TypeNotDiscovered,
                            site.Location,
                            [call.Location],
                            site.Helper.ToDisplayString(MessageFormat),
                            site.Name,
                            name));
                    }
                }
            }
        }
    }
}
