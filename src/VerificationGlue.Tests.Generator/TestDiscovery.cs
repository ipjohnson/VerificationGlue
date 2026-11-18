using System.Collections.Generic;
using System.Collections.Immutable;
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

        if (method.Parameters.FirstOrDefault(parameter => !CanBox(parameter.Type)) is { } unboxable)
        {
            return $"has the parameter {unboxable.Name}, whose type cannot be passed as an object";
        }

        return null;
    }

    /// <summary>
    /// The constructor the runner calls: the accessible one with the most parameters that it can
    /// supply. Null when there is none, or the method is static and needs no instance.
    /// </summary>
    public static IMethodSymbol? Constructor(IMethodSymbol method, Compilation compilation) =>
        method.IsStatic || method.ContainingType.IsStatic
            ? null
            : method.ContainingType.InstanceConstructors
                .Where(constructor =>
                    compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly) &&
                    constructor.Parameters.All(parameter => parameter.RefKind == RefKind.None && CanBox(parameter.Type)))
                .OrderByDescending(constructor => constructor.Parameters.Length)
                .FirstOrDefault();

    /// <summary>True for a <c>[Mock]</c> parameter whose type <c>Mock.Of&lt;T&gt;()</c> accepts.</summary>
    public static bool IsMock(IParameterSymbol parameter) =>
        parameter.Type is { IsReferenceType: true } and not ITypeParameterSymbol &&
        parameter.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == KnownTypes.MockAttribute);

    /// <summary>
    /// The bootstrap and scope of the class or assembly container of a test that has <c>[Mock]</c>
    /// parameters, when the bootstrap does not implement <c>ISupportsChildOverrides</c> (VGT0008).
    /// Null in every other case, including a <c>Scope</c> that a derived attribute sets in its own
    /// constructor, which only the runner can see.
    /// </summary>
    public static (string Bootstrap, string Scope)? SharedContainerWithoutChildOverrides(IMethodSymbol method, Compilation compilation)
    {
        var parameters = method.Parameters.AsEnumerable();
        if (Constructor(method, compilation) is { } constructor)
        {
            parameters = constructor.Parameters.Concat(parameters);
        }

        if (!parameters.Any(IsMock))
        {
            return null;
        }

        var container = FindContainer(method.GetAttributes()) ?? FindContainer(method.ContainingType.GetAttributes()) ?? FindContainer(compilation.Assembly.GetAttributes());
        if (container is null || Bootstrap(container.AttributeClass) is not { } bootstrap)
        {
            return null;
        }

        var scope = container.NamedArguments.FirstOrDefault(pair => pair.Key == "Scope").Value.Value switch
        {
            1 => "Class",
            2 => "Assembly",
            _ => null,
        };

        return scope is not null && !bootstrap.AllInterfaces.Any(type => type.ToDisplayString() == KnownTypes.ISupportsChildOverrides)
            ? (bootstrap.ToDisplayString(), scope)
            : null;
    }

    private static AttributeData? FindContainer(IEnumerable<AttributeData> attributes) =>
        attributes.LastOrDefault(attribute => Bootstrap(attribute.AttributeClass) is not null);

    private static ITypeSymbol? Bootstrap(INamedTypeSymbol? attribute) => TypeArgument(attribute, KnownTypes.ContainerAttribute);

    /// <summary>
    /// The global type name of the <c>ITestNameFormatter</c> from the narrowest <c>[NameFormatter&lt;T&gt;]</c>:
    /// the method's, then the class's, then the assembly's. <c>DefaultNameFormatter</c> when none applies.
    /// </summary>
    public static string NameFormatter(IMethodSymbol method, Compilation compilation)
    {
        var formatter = Formatter(method.GetAttributes()) ?? Formatter(method.ContainingType.GetAttributes()) ?? Formatter(compilation.Assembly.GetAttributes());
        return formatter?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? KnownTypes.DefaultNameFormatter;

        static ITypeSymbol? Formatter(IEnumerable<AttributeData> attributes) =>
            attributes.Select(attribute => TypeArgument(attribute.AttributeClass, KnownTypes.NameFormatterAttribute)).LastOrDefault(type => type is not null);
    }

    /// <summary>The <c>T</c> of <paramref name="attribute"/> when it is, or derives from, <c>{genericName}&lt;T&gt;</c>.</summary>
    private static ITypeSymbol? TypeArgument(INamedTypeSymbol? attribute, string genericName)
    {
        for (var type = attribute; type is not null; type = type.BaseType)
        {
            if (type is { IsGenericType: true, TypeArguments.Length: 1 } && type.ConstructedFrom.ToDisplayString() == genericName + "<T>")
            {
                return type.TypeArguments[0];
            }
        }

        return null;
    }

    private static bool CanBox(ITypeSymbol type) =>
        type is not (IPointerTypeSymbol or IFunctionPointerTypeSymbol) && !type.IsRefLikeType && type.SpecialType != SpecialType.System_Void;

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
        var parameters = method.Parameters.Select(parameter => Parameter(parameter, compilation)).ToArray();
        var constructor = Constructor(method, compilation);

        var location = method.Locations.FirstOrDefault(candidate => candidate.IsInSource);
        var span = location?.GetLineSpan();
        var rows = RowReader.Read(method, compilation);
        var sources = RowSourceReader.Read(method, compilation);
        var testClass = new TestClassModel(
            MetadataName(type),
            type.GetTypeDefinition(),
            type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString(),
            NestedName(type, '+'),
            constructor is not null,
            constructor?.Parameters.Select(parameter => Parameter(parameter, compilation)).ToArray() ?? [],
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
            AttributeMaterializer.Materialize(method.GetAttributes().Where(attribute => !RowReader.IsRow(attribute)).ToImmutableArray(), compilation),
            rows.Count > 0,
            rows.Where(row => row.Row is not null).Select(row => row.Row!).ToArray(),
            NameFormatter(method, compilation),
            Schedule(method, compilation),
            sources.Count > 0,
            sources.Where(source => source.Source is not null).Select(source => source.Source!).ToArray(),
            method.GetAttributes().Any(RowSourceReader.IsDataSource));

        return new TestEntry(testClass, testMethod);
    }

    /// <summary>
    /// The schedule group of <paramref name="method"/> from the narrowest <c>[ScheduleGroup]</c>: the
    /// method's, the class's, then the assembly's. The <c>[RunAfter]</c> groups come from the class, and
    /// the <c>[Resource]</c> keys from the class and the method, widest first.
    /// </summary>
    public static ScheduleModel Schedule(IMethodSymbol method, Compilation compilation)
    {
        var type = method.ContainingType;
        var group = FindLast(method.GetAttributes(), KnownTypes.ScheduleGroupAttribute)
            ?? FindLast(type.GetAttributes(), KnownTypes.ScheduleGroupAttribute)
            ?? FindLast(compilation.Assembly.GetAttributes(), KnownTypes.ScheduleGroupAttribute);
        var name = group is null ? null : StringArgument(group);
        var maxParallel = group?.NamedArguments.FirstOrDefault(pair => pair.Key == "MaxParallel").Value.Value is int limit ? limit : 0;

        return new ScheduleModel(
            name ?? ScheduleModel.DefaultGroup,
            name is null ? 0 : maxParallel,
            Strings(type.GetAttributes(), KnownTypes.RunAfterAttribute),
            Strings(type.GetAttributes().Concat(method.GetAttributes()), KnownTypes.ResourceAttribute));
    }

    /// <summary>The <c>[RunAfter]</c> attributes of the class that declares <paramref name="method"/>, with their group names.</summary>
    public static IEnumerable<(AttributeData Attribute, string Group)> RunAfter(IMethodSymbol method) =>
        method.ContainingType.GetAttributes()
            .Where(attribute => attribute.AttributeClass?.ToDisplayString() == KnownTypes.RunAfterAttribute)
            .Select(attribute => (attribute, StringArgument(attribute)!))
            .Where(pair => pair.Item2 is not null);

    private static AttributeData? FindLast(IEnumerable<AttributeData> attributes, string name) =>
        attributes.LastOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == name);

    private static string? StringArgument(AttributeData attribute) =>
        attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string { Length: > 0 } value ? value : null;

    private static string[] Strings(IEnumerable<AttributeData> attributes, string name) =>
        attributes
            .Where(attribute => attribute.AttributeClass?.ToDisplayString() == name)
            .Select(StringArgument)
            .OfType<string>()
            .Distinct(System.StringComparer.Ordinal)
            .ToArray();

    /// <summary>A name for the registration file that no other class shares.</summary>
    private static string MetadataName(INamedTypeSymbol type)
    {
        var name = NestedName(type, '+');
        return type.ContainingNamespace.IsGlobalNamespace ? name : type.ContainingNamespace.ToDisplayString() + "." + name;
    }

    private static string NestedName(INamedTypeSymbol type, char separator) =>
        type.ContainingType is { } outer ? NestedName(outer, separator) + separator + type.Name : type.Name;

    private static ParameterModel Parameter(IParameterSymbol parameter, Compilation compilation)
    {
        var serviceKey = parameter.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == KnownTypes.FromKeyedServicesAttribute && attribute.ConstructorArguments.Length == 1)
            is { } keyed
            ? AttributeMaterializer.Write(keyed.ConstructorArguments[0], compilation)
            : null;

        return new ParameterModel(
            parameter.Name,
            parameter.Type.TypeKind == TypeKind.Dynamic ? "object" : parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            TypeName(parameter.Type),
            IsMock(parameter),
            serviceKey,
            AttributeMaterializer.WriteDefault(parameter),
            AttributeMaterializer.Materialize(parameter.GetAttributes(), compilation));
    }

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
