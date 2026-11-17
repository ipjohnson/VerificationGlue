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
/// Reports VGM0001 to VGM0005 and VGM0007 at the code that causes them: the place that names a type
/// to mock, a misplaced <c>Arg</c> matcher, or a setup on a value that no mock call returned.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MockAnalyzer : DiagnosticAnalyzer
{
    private const string OpenGenericReason = "it is an open generic type. Name a closed type, such as typeof(IRepository<Order>)";

    private static readonly SymbolDisplayFormat MessageFormat = SymbolDisplayFormat.CSharpErrorMessageFormat;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MockDiagnostics.TypeCannotBeMocked,
        MockDiagnostics.MatcherWithoutPosition,
        MockDiagnostics.MatcherInTypedSurface,
        MockDiagnostics.MemberCannotBeMocked,
        MockDiagnostics.TypeNotDiscovered,
        MockDiagnostics.SetupWithoutMockCall,
        MockDiagnostics.BeyondRecursionDepth);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var depth = RecursiveMocks.ReadDepth(start.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
            var hostAttributes = MockDiscovery.ReadMockAttributes(start.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
            var state = new CompilationState(start.Compilation, depth, hostAttributes);
            start.RegisterOperationAction(operation => AnalyzeInvocation(operation, state), OperationKind.Invocation);
            start.RegisterSyntaxNodeAction(node => AnalyzeAttribute(node, state), SyntaxKind.Attribute);
            start.RegisterCompilationEndAction(end =>
            {
                state.ReportUndiscovered(end);
                state.ReportBeyondDepth(end);
            });
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

        if (MockDiscovery.ArgKindOf(method) is { } kind)
        {
            if (ArgumentOf(invocation) is IArgumentOperation { Parameter: { } parameter } && MockDiscovery.IsTypedArg(parameter.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MockDiagnostics.MatcherInTypedSurface, invocation.Syntax.GetLocation(), invocation.Syntax.ToString(), TypedReplacement(invocation, kind)));
            }
            else if (!IsMockArgument(invocation))
            {
                context.ReportDiagnostic(Diagnostic.Create(MockDiagnostics.MatcherWithoutPosition, invocation.Syntax.GetLocation(), invocation.Syntax.ToString()));
            }
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

        if ((name != MockDiscovery.MockAttribute && !state.HostAttributes.Contains(name)) || !MockDiscovery.IsAttributeOnMockTarget(attribute))
        {
            return;
        }

        var symbol = MockDiscovery.TargetOf(attribute, context.SemanticModel, context.CancellationToken);
        if (MockDiscovery.TypeOfTarget(symbol) is { } targetType && !MockDiscovery.ContainsTypeParameter(targetType))
        {
            state.Discover(targetType, attribute.GetLocation(), context.ReportDiagnostic);
        }
    }

    /// <summary>The operation that takes the matcher's value, past implicit conversions such as the one to <c>Arg&lt;T&gt;</c>.</summary>
    private static IOperation? ArgumentOf(IOperation matcher) => Outermost(matcher).Parent;

    private static IOperation Outermost(IOperation matcher)
    {
        var current = matcher;
        while (current.Parent is IConversionOperation { IsImplicit: true } conversion)
        {
            current = conversion;
        }

        return current;
    }

    /// <summary>The <c>Arg&lt;T&gt;</c> form of an NSubstitute-style matcher, such as <c>Arg&lt;int&gt;.Any()</c> for <c>Arg.Any&lt;int&gt;()</c>.</summary>
    private static string TypedReplacement(IInvocationOperation invocation, ArgKind kind)
    {
        var type = "Arg<" + invocation.TargetMethod.TypeArguments[0].ToDisplayString(MessageFormat) + ">";
        return kind == ArgKind.Any
            ? type + ".Any()"
            : type + ".Is(" + invocation.Arguments[0].Syntax + ")";
    }

    /// <summary>True when the matcher is passed directly to a mock member, or assigned directly to a mock property.</summary>
    private static bool IsMockArgument(IOperation matcher)
    {
        var current = Outermost(matcher);
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

    /// <summary>What one compilation's analysis has seen, for VGM0004 and VGM0007 at its end.</summary>
    private sealed class CompilationState(Compilation compilation, int depth, CSharpAuthor.Collections.EquatableArray<string> hostAttributes)
    {
        /// <summary>The <c>VerificationGlueMockAttribute</c> names, which mark a mock like <c>[Mock]</c>.</summary>
        public CSharpAuthor.Collections.EquatableArray<string> HostAttributes { get; } = hostAttributes;

        private readonly ConcurrentDictionary<ITypeSymbol, MockModel> _models = new(SymbolEqualityComparer.Default);

        private readonly ConcurrentBag<(string MockName, Location Location)> _sites = new();

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
            _sites.Add((model.MockName, location));
            foreach (var diagnostic in model.Diagnostics)
            {
                report(Diagnostic.Create(MockDiagnostics.Find(diagnostic.Id), location, diagnostic.Arguments.ToArray()));
            }
        }

        /// <summary>
        /// Walks every discovered type as the generator does, and reports each member beyond the
        /// depth at the places that name the discovered type whose walk reached it.
        /// </summary>
        public void ReportBeyondDepth(CompilationAnalysisContext context)
        {
            if (_models.IsEmpty)
            {
                return;
            }

            var walk = RecursiveMocks.Walk(_models.Values, depth, compilation);
            var sites = _sites.ToLookup(site => site.MockName, site => site.Location);
            foreach (var (node, member) in walk.BeyondDepth())
            {
                foreach (var location in sites[node.Root].Distinct())
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MockDiagnostics.BeyondRecursionDepth,
                        location,
                        node.Model.TypeName,
                        member.Kind == MemberKind.Indexer ? "this[]" : member.Name,
                        member.MockReturn!.TypeName,
                        depth));
                }
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
