using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Glue.Mocks.Generator
{
    [Generator]
    public sealed class MockGenerator : IIncrementalGenerator
    {
        /// <summary>
        /// Test switch for the caching pitfall: compare mock models by type name only, the way a
        /// model that forgot its members would.
        /// </summary>
        public static bool CompareByNameOnly;

        /// <summary>Test switch: turn off the positional Arg interceptors to show the run-time rule alone.</summary>
        public static bool InterceptArgMatchers = true;

        private static readonly string[] s_mockAttributes =
        {
            "Glue.Mocks.MockAttribute",
            "Glue.Mocks.NUnit.MockAttribute",
        };

        private static readonly DiagnosticDescriptor s_memberUnsupported = new DiagnosticDescriptor(
            "GM001",
            "Member cannot be mocked",
            "'{0}.{1}' cannot be mocked ({2}); the generated member throws NotSupportedException",
            "Glue.Mocks",
            DiagnosticSeverity.Warning,
            true
        );

        private static readonly DiagnosticDescriptor s_typeUnsupported = new DiagnosticDescriptor(
            "GM002",
            "Type cannot be mocked",
            "'{0}' cannot be mocked: {1}",
            "Glue.Mocks",
            DiagnosticSeverity.Error,
            true
        );

        private static readonly SymbolDisplayFormat s_typeFormat =
            SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                    | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            );

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Mock.Of<T>() and Substitute.For<T>() call sites.
            var calls = context
                .SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) =>
                        node
                            is InvocationExpressionSyntax
                            {
                                Expression: MemberAccessExpressionSyntax
                                {
                                    Name: GenericNameSyntax { Identifier.ValueText: "Of" or "For" }
                                }
                            },
                    static (ctx, ct) =>
                    {
                        var invocation = (InvocationExpressionSyntax)ctx.Node;

                        if (
                            ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol
                                is not IMethodSymbol
                                {
                                    TypeArguments.Length: 1,
                                    ContainingType:
                                    {
                                        Name: "Mock" or "Substitute",
                                        ContainingNamespace:
                                        {
                                            Name: "Mocks",
                                            ContainingNamespace.Name: "Glue"
                                        }
                                    }
                                } method
                            || method.TypeArguments[0] is not INamedTypeSymbol type
                        )
                        {
                            return null;
                        }

                        var mock = ModelBuilder.Build(type);
                        InterceptModel? intercept = null;

                        if (method.ContainingType.Name == "Substitute" && mock.Refusal == null)
                        {
                            var location = ctx.SemanticModel.GetInterceptableLocation(invocation, ct);

                            if (location != null)
                            {
                                intercept = new InterceptModel(
                                    mock.Type,
                                    mock.MockName,
                                    location.Version,
                                    location.Data
                                );
                            }
                        }

                        return new Discovery(mock, intercept);
                    }
                )
                .Where(static d => d != null)
                .Select(static (d, _) => d!)
                .WithTrackingName("CallDiscovery");

            // [Mock] parameters, properties and fields.
            var attributed = s_mockAttributes
                .Select(name =>
                    context
                        .SyntaxProvider.ForAttributeWithMetadataName(
                            name,
                            static (_, _) => true,
                            static (ctx, _) =>
                            {
                                var type = ctx.TargetSymbol switch
                                {
                                    IParameterSymbol p => p.Type,
                                    IPropertySymbol p => p.Type,
                                    IFieldSymbol f => f.Type,
                                    _ => null,
                                };

                                return type is INamedTypeSymbol named ? ModelBuilder.Build(named) : null;
                            }
                        )
                        .Where(static m => m != null)
                        .Select(static (m, _) => m!)
                        .Collect()
                )
                .ToArray();

            var all = calls.Select(static (d, _) => d.Mock!).Collect();

            foreach (var provider in attributed)
            {
                all = all.Combine(provider).Select(static (pair, _) => pair.Left.AddRange(pair.Right));
            }

            // One entry per mocked type, in a stable order. The interceptor locations are kept out of
            // this model on purpose: they change whenever the calling file is edited.
            var mocks = all.SelectMany(
                static (models, _) =>
                    models
                        .GroupBy(m => m.Type)
                        .Select(g => g.First())
                        .OrderBy(m => m.Type, StringComparer.Ordinal)
                        .ToImmutableArray()
            );

            if (CompareByNameOnly)
            {
                mocks = mocks.WithComparer(NameOnlyComparer.Instance);
            }

            mocks = mocks.WithTrackingName("MockModels");

            context.RegisterSourceOutput(
                mocks,
                static (spc, model) =>
                {
                    if (model.Refusal != null)
                    {
                        spc.ReportDiagnostic(
                            Diagnostic.Create(s_typeUnsupported, Location.None, model.Type, model.Refusal)
                        );
                        return;
                    }

                    foreach (var member in model.Members)
                    {
                        if (member.Unsupported != null)
                        {
                            spc.ReportDiagnostic(
                                Diagnostic.Create(
                                    s_memberUnsupported,
                                    Location.None,
                                    model.Type,
                                    member.Name,
                                    member.Unsupported
                                )
                            );
                        }
                    }

                    spc.AddSource(model.MockName + ".g.cs", Emitter.EmitMock(model));
                }
            );

            var registrations = mocks
                .Where(static m => m.Refusal == null)
                .Select(static (m, _) => (m.Type, m.MockName))
                .Collect()
                .WithComparer(PairArrayComparer.Instance)
                .WithTrackingName("Registrations");

            context.RegisterSourceOutput(
                registrations,
                static (spc, items) =>
                {
                    if (items.Length > 0)
                    {
                        spc.AddSource("GlueMocks.Registry.g.cs", Emitter.EmitRegistry(items));
                    }
                }
            );

            var intercepts = calls
                .Where(static d => d.Intercept != null)
                .Select(static (d, _) => d.Intercept!)
                .Collect()
                .WithTrackingName("Intercepts");

            // Arg.Any<T>() and Arg.Is<T>(...) written directly as an argument. The interceptor passes
            // the parameter's ordinal, which the run time cannot work out on its own.
            var argIntercepts = InterceptArgMatchers
                ? context
                    .SyntaxProvider.CreateSyntaxProvider(
                        static (node, _) =>
                            node
                                is InvocationExpressionSyntax
                                {
                                    Parent: ArgumentSyntax { Parent: BaseArgumentListSyntax },
                                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Any" or "Is" }
                                },
                        static (ctx, ct) =>
                        {
                            var invocation = (InvocationExpressionSyntax)ctx.Node;

                            if (
                                ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol
                                    is not IMethodSymbol
                                    {
                                        ContainingType:
                                        {
                                            Name: "Arg",
                                            ContainingNamespace: { Name: "Mocks", ContainingNamespace.Name: "Glue" }
                                        }
                                    } method
                                || ctx.SemanticModel.GetOperation(invocation.Parent!, ct)
                                    is not Microsoft.CodeAnalysis.Operations.IArgumentOperation { Parameter: { } parameter }
                                || ctx.SemanticModel.GetInterceptableLocation(invocation, ct) is not { } location
                            )
                            {
                                return null;
                            }

                            var kind =
                                method.Parameters.Length == 0 ? "Any"
                                : method.Parameters[0].Type.TypeKind == TypeKind.Delegate ? "IsPredicate"
                                : "IsValue";

                            return new ArgInterceptModel(kind, parameter.Ordinal, location.Version, location.Data);
                        }
                    )
                    .Where(static m => m != null)
                    .Select(static (m, _) => m!)
                    .Collect()
                : context.CompilationProvider.Select(static (_, _) => ImmutableArray<ArgInterceptModel>.Empty);

            context.RegisterSourceOutput(
                intercepts.Combine(argIntercepts),
                static (spc, items) =>
                {
                    if (items.Left.Length > 0 || items.Right.Length > 0)
                    {
                        spc.AddSource("GlueMocks.Interceptors.g.cs", Emitter.EmitInterceptors(items.Left, items.Right));
                    }
                }
            );
        }

        private sealed class NameOnlyComparer : IEqualityComparer<MockModel>
        {
            public static readonly NameOnlyComparer Instance = new NameOnlyComparer();

            public bool Equals(MockModel x, MockModel y) => x.Type == y.Type;

            public int GetHashCode(MockModel obj) => obj.Type.GetHashCode();
        }

        private sealed class PairArrayComparer : IEqualityComparer<ImmutableArray<(string, string)>>
        {
            public static readonly PairArrayComparer Instance = new PairArrayComparer();

            public bool Equals(ImmutableArray<(string, string)> x, ImmutableArray<(string, string)> y) =>
                x.SequenceEqual(y);

            public int GetHashCode(ImmutableArray<(string, string)> obj) => obj.Length;
        }

        private static class ModelBuilder
        {
            public static MockModel Build(INamedTypeSymbol type)
            {
                var typeName = type.ToDisplayString(s_typeFormat);
                var mockName = Sanitize(type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)) + "_Mock";
                var isClass = type.TypeKind == TypeKind.Class;

                string? refusal = null;

                if (type.TypeKind != TypeKind.Interface && type.TypeKind != TypeKind.Class)
                {
                    refusal = "only interfaces and classes can be mocked";
                }
                else if (type.IsSealed || type.IsStatic)
                {
                    refusal = "the class is sealed or static";
                }
                else if (
                    isClass
                    && !type.InstanceConstructors.Any(c =>
                        c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected
                    )
                )
                {
                    refusal = "the class has no accessible parameterless constructor";
                }

                if (refusal != null)
                {
                    return new MockModel(typeName, mockName, isClass, default, refusal);
                }

                var members = new List<MemberModel>();

                if (isClass)
                {
                    var seen = new HashSet<string>();

                    for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
                    {
                        foreach (var member in current.GetMembers())
                        {
                            if (
                                member.IsStatic
                                || member.DeclaredAccessibility != Accessibility.Public
                                || member.IsSealed
                                || !(member.IsVirtual || member.IsAbstract || member.IsOverride)
                            )
                            {
                                continue;
                            }

                            var model = Convert(member, typeName, true);

                            if (model != null && seen.Add(model.Name + "(" + string.Join(",", model.Parameters.Select(p => p.Type)) + ")"))
                            {
                                members.Add(model);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var contract in new[] { type }.Concat(type.AllInterfaces))
                    {
                        var contractName = contract.ToDisplayString(s_typeFormat);

                        foreach (var member in contract.GetMembers())
                        {
                            if (member.IsStatic)
                            {
                                if (member.IsAbstract)
                                {
                                    refusal = "the interface has static abstract members";
                                }

                                continue;
                            }

                            if (member.DeclaredAccessibility != Accessibility.Public)
                            {
                                continue;
                            }

                            var model = Convert(member, contractName, false);

                            if (model != null)
                            {
                                members.Add(model);
                            }
                        }
                    }
                }

                return new MockModel(typeName, mockName, isClass, members.ToEquatableArray(), refusal);
            }

            private static MemberModel? Convert(ISymbol member, string containingType, bool isOverride)
            {
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                    {
                        var (kind, awaited) = Classify(method.ReturnType);
                        var returnType = method.ReturnType.ToDisplayString(s_typeFormat);
                        string? unsupported = null;

                        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                        {
                            unsupported = "ref return";
                            returnType = (method.ReturnsByRefReadonly ? "ref readonly " : "ref ") + returnType;
                        }
                        else if (method.ReturnType.IsRefLikeType)
                        {
                            unsupported = "ref struct return";
                        }
                        else if (method.Parameters.Any(p => p.Type.IsRefLikeType || p.Type.TypeKind == TypeKind.Pointer))
                        {
                            unsupported = "ref struct or pointer parameter";
                        }

                        return new MemberModel(
                            MemberKind.Method,
                            method.Name,
                            containingType,
                            returnType,
                            kind,
                            awaited,
                            Parameters(method.Parameters),
                            method.TypeParameters.Select(t => t.Name).ToEquatableArray(),
                            false,
                            false,
                            isOverride,
                            unsupported
                        );
                    }

                    case IPropertySymbol property:
                    {
                        var (kind, awaited) = Classify(property.Type);

                        return new MemberModel(
                            property.IsIndexer ? MemberKind.Indexer : MemberKind.Property,
                            property.Name,
                            containingType,
                            property.Type.ToDisplayString(s_typeFormat),
                            kind,
                            awaited,
                            Parameters(property.Parameters),
                            default,
                            property.GetMethod != null,
                            property.SetMethod != null,
                            isOverride,
                            property.ReturnsByRef || property.ReturnsByRefReadonly ? "ref property" : null
                        );
                    }

                    case IEventSymbol @event when @event.Type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke }:
                        return new MemberModel(
                            MemberKind.Event,
                            @event.Name,
                            containingType,
                            @event.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(s_typeFormat),
                            ReturnKind.Void,
                            null,
                            Parameters(invoke.Parameters),
                            default,
                            false,
                            false,
                            isOverride,
                            null
                        );

                    default:
                        return null;
                }
            }

            private static EquatableArray<ParameterModel> Parameters(ImmutableArray<IParameterSymbol> parameters) =>
                parameters
                    .Select(p => new ParameterModel(
                        SyntaxFacts.GetKeywordKind(p.Name) != SyntaxKind.None ? "@" + p.Name : p.Name,
                        p.Type.ToDisplayString(s_typeFormat),
                        (int)p.RefKind
                    ))
                    .ToEquatableArray();

            private static (ReturnKind, string?) Classify(ITypeSymbol type)
            {
                if (type.SpecialType == SpecialType.System_Void)
                {
                    return (ReturnKind.Void, null);
                }

                if (
                    type is INamedTypeSymbol
                    {
                        ContainingNamespace:
                        {
                            Name: "Tasks",
                            ContainingNamespace: { Name: "Threading", ContainingNamespace.Name: "System" }
                        }
                    } named
                )
                {
                    switch (named.MetadataName)
                    {
                        case "Task":
                            return (ReturnKind.Task, null);
                        case "Task`1":
                            return (ReturnKind.TaskOfT, named.TypeArguments[0].ToDisplayString(s_typeFormat));
                        case "ValueTask":
                            return (ReturnKind.ValueTask, null);
                        case "ValueTask`1":
                            return (ReturnKind.ValueTaskOfT, named.TypeArguments[0].ToDisplayString(s_typeFormat));
                    }
                }

                return (ReturnKind.Value, null);
            }

            private static string Sanitize(string name)
            {
                var builder = new StringBuilder(name.Length);

                foreach (var c in name)
                {
                    builder.Append(char.IsLetterOrDigit(c) ? c : '_');
                }

                return builder.ToString();
            }
        }
    }
}
