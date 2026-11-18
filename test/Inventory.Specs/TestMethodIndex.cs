using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Inventory.Specs;

// Finds a test method from a reference such as path/File.cs#Outer.Inner.Method.
internal sealed class TestMethodIndex(string repositoryRoot)
{
    private static readonly HashSet<string> TestAttributes =
        ["Fact", "Theory", "Test", "TestMethod", "DataTestMethod", "TestCase", "VerifyTest", "Case"];

    private readonly Dictionary<string, CompilationUnitSyntax> files = [];

    // Returns why the reference names no test method, or null when it does.
    public string? Problem(string path, string member)
    {
        var dot = member.LastIndexOf('.');
        if (dot <= 0 || dot == member.Length - 1)
        {
            return $"'{member}' is not of the form Type.Method";
        }

        var type = member[..dot];
        var method = member[(dot + 1)..];
        var full = Path.Combine(repositoryRoot, path);
        if (!File.Exists(full))
        {
            return $"the file {path} does not exist";
        }

        if (!files.TryGetValue(path, out var unit))
        {
            unit = CSharpSyntaxTree.ParseText(File.ReadAllText(full)).GetCompilationUnitRoot();
            files[path] = unit;
        }

        var types = unit.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(t => NestedName(t) == type).ToList();
        if (types.Count == 0)
        {
            return $"{path} declares no type {type}";
        }

        var methods = types
            .SelectMany(t => t.Members.OfType<MethodDeclarationSyntax>())
            .Where(m => m.Identifier.Text == method)
            .ToList();
        if (methods.Count == 0)
        {
            return $"{type} in {path} has no method {method}";
        }

        return methods.Any(IsTest) ? null : $"{type}.{method} in {path} has no test attribute";
    }

    private static string NestedName(TypeDeclarationSyntax type)
    {
        var name = type.Identifier.Text;
        for (var parent = type.Parent as TypeDeclarationSyntax; parent is not null; parent = parent.Parent as TypeDeclarationSyntax)
        {
            name = parent.Identifier.Text + "." + name;
        }

        return name;
    }

    private static bool IsTest(MethodDeclarationSyntax method) =>
        method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => TestAttributes.Contains(SimpleName(attribute.Name)));

    private static string SimpleName(NameSyntax name)
    {
        var simple = name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
            SimpleNameSyntax plain => plain.Identifier.Text,
            _ => name.ToString(),
        };

        return simple.EndsWith("Attribute", StringComparison.Ordinal) ? simple[..^"Attribute".Length] : simple;
    }
}
