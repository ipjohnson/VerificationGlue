using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using VerificationGlue.Shared;

namespace VerificationGlue.Tests.Generator;

/// <summary>
/// The rules for <c>[VerifyTest]</c> methods that the generator and <see cref="TestAnalyzer"/> share:
/// which methods can run, the test ID, and how the runner supplies the class and the parameters.
/// </summary>
internal static class TestDiscovery
{
    private const string CancellationToken = "System.Threading.CancellationToken";
    private const string TestContext = "VerificationGlue.Tests.TestContext";

    /// <summary>Why the runner cannot call <paramref name="method"/>, or null when it can.</summary>
    public static string? WhyNotRunnable(IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Ordinary)
        {
            return "is not an ordinary method";
        }

        if (method.DeclaredAccessibility != Accessibility.Public)
        {
            return "is not public";
        }

        if (method.IsGenericMethod)
        {
            return "is generic, and a test needs data rows to close it";
        }

        for (var type = method.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.IsGenericType)
            {
                return $"is in the generic type {type.Name}";
            }

            if (type.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal || type.IsFileLocal)
            {
                return $"is in {type.Name}, which generated code cannot reach";
            }
        }

        if (method.ContainingType.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return "is not declared in a class or a struct";
        }

        if (!method.IsStatic && method.ContainingType.IsAbstract)
        {
            return "is an instance method of an abstract class";
        }

        if (method.IsAsync && method.ReturnsVoid)
        {
            return "is async void, so the runner cannot wait for it";
        }

        if (!method.ReturnsVoid && (method.ReturnsByRef || method.ReturnsByRefReadonly || !IsAwaitable(method.ReturnType)))
        {
            return "does not return void, Task or ValueTask";
        }

        if (method.Parameters.Any(parameter => parameter.RefKind != RefKind.None))
        {
            return "has a ref, out or in parameter";
        }

        return null;
    }

    /// <summary>
    /// The test ID: the containing type, the method name and the parameter types, written with
    /// framework type names. It does not depend on the display name or on the source position.
    /// </summary>
    public static string Uid(IMethodSymbol method)
    {
        var text = new StringBuilder();
        AppendTypeName(text, method.ContainingType);
        text.Append('.').Append(method.Name).Append('(');
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (i > 0)
            {
                text.Append(',');
            }

            AppendTypeName(text, method.Parameters[i].Type);
        }

        return text.Append(')').ToString();
    }

    /// <summary>A type written as the test ID writes it, such as <c>System.Collections.Generic.List&lt;System.Int32&gt;</c>.</summary>
    public static string TypeName(ITypeSymbol type)
    {
        var text = new StringBuilder();
        AppendTypeName(text, type);
        return text.ToString();
    }

    public static TestEntry Build(IMethodSymbol method, Compilation compilation)
    {
        var type = method.ContainingType;
        var parameters = method.Parameters
            .Select(parameter => new ParameterModel(
                parameter.Name,
                TypeName(parameter.Type),
                parameter.Type.ToDisplayString() switch
                {
                    CancellationToken => "context.CancellationToken",
                    TestContext => "context",
                    _ => null,
                }))
            .ToArray();

        var location = method.Locations.FirstOrDefault(candidate => candidate.IsInSource);
        var span = location?.GetLineSpan();
        var testClass = new TestClassModel(
            MetadataName(type),
            type.GetTypeDefinition(),
            type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString(),
            NestedName(type, '+'),
            type.TypeKind == TypeKind.Struct || CanConstruct(type, compilation),
            AttributeMaterializer.Materialize(type.GetAttributes(), compilation));

        var testMethod = new TestMethodModel(
            method.Name,
            Uid(method),
            span?.Path ?? "",
            (span?.StartLinePosition.Line ?? -1) + 1,
            method.IsStatic,
            method.ReturnsVoid ? ReturnKind.Void : ReturnKind.Awaitable,
            method.ReturnsVoid ? "System.Void" : TypeName(method.ReturnType),
            parameters,
            VerifyRewriteRules.WillRewrite(method),
            AttributeMaterializer.Materialize(method.GetAttributes(), compilation));

        return new TestEntry(testClass, testMethod);
    }

    /// <summary>A name for the registration file that no other class shares.</summary>
    private static string MetadataName(INamedTypeSymbol type)
    {
        var name = NestedName(type, '+');
        return type.ContainingNamespace.IsGlobalNamespace ? name : type.ContainingNamespace.ToDisplayString() + "." + name;
    }

    private static string NestedName(INamedTypeSymbol type, char separator) =>
        type.ContainingType is { } outer ? NestedName(outer, separator) + separator + type.Name : type.Name;

    private static bool CanConstruct(INamedTypeSymbol type, Compilation compilation) =>
        !type.IsStatic && type.InstanceConstructors.Any(constructor =>
            compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly) &&
            constructor.Parameters.All(parameter => parameter.IsOptional || parameter.IsParams));

    private static bool IsAwaitable(ITypeSymbol type) =>
        type is INamedTypeSymbol { ContainingNamespace: { Name: "Tasks", ContainingNamespace: { Name: "Threading", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } } named &&
        named.Name is "Task" or "ValueTask" && named.TypeArguments.Length <= 1;

    private static void AppendTypeName(StringBuilder text, ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                AppendTypeName(text, array.ElementType);
                text.Append('[').Append(',', array.Rank - 1).Append(']');
                return;
            case IPointerTypeSymbol pointer:
                AppendTypeName(text, pointer.PointedAtType);
                text.Append('*');
                return;
            case IDynamicTypeSymbol:
                text.Append("System.Object");
                return;
            case INamedTypeSymbol named:
                named = named.TupleUnderlyingType ?? named;
                if (named.ContainingType is { } outer)
                {
                    AppendTypeName(text, outer);
                    text.Append('.');
                }
                else if (!named.ContainingNamespace.IsGlobalNamespace)
                {
                    text.Append(named.ContainingNamespace.ToDisplayString()).Append('.');
                }

                text.Append(named.Name);
                IReadOnlyList<ITypeSymbol> arguments = named.TypeArguments;
                if (arguments.Count > 0)
                {
                    text.Append('<');
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        if (i > 0)
                        {
                            text.Append(',');
                        }

                        AppendTypeName(text, arguments[i]);
                    }

                    text.Append('>');
                }

                return;
            default:
                text.Append(type.ToDisplayString());
                return;
        }
    }
}
