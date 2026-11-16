using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Glue.Mocks.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var parse = new CSharpParseOptions(LanguageVersion.Latest);
var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
    .Split(Path.PathSeparator)
    .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
    .ToList();
platform.Add(MetadataReference.CreateFromFile(typeof(Glue.Mocks.Mock).Assembly.Location));

SyntaxTree Tree(string text, string path) => CSharpSyntaxTree.ParseText(text, parse, path, Encoding.UTF8);

CSharpCompilation Compile(string name, IEnumerable<MetadataReference> references, params SyntaxTree[] trees) =>
    CSharpCompilation.Create(name, trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

GeneratorDriver Driver() =>
    CSharpGeneratorDriver.Create(
        [new MockGenerator().AsSourceGenerator()],
        parseOptions: parse,
        driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

string Report(GeneratorDriver driver, string step)
{
    var result = driver.GetRunResult().Results[0];
    var reasons = result.TrackedSteps.TryGetValue(step, out var steps)
        ? steps.SelectMany(s => s.Outputs).Select(o => o.Reason.ToString())
        : [];
    return $"{step}=[{string.Join(",", reasons)}]";
}

string Outputs(GeneratorDriver driver)
{
    var result = driver.GetRunResult().Results[0];
    var reasons = result.TrackedOutputSteps.SelectMany(kv => kv.Value).SelectMany(s => s.Outputs).Select(o => o.Reason.ToString());
    return $"SourceOutputs=[{string.Join(",", reasons)}]";
}

string MockSource(GeneratorDriver driver) =>
    driver.GetRunResult().Results[0].GeneratedSources.First(s => s.HintName.Contains("ICalc_Mock")).SourceText.ToString();

void Line(string label, GeneratorDriver driver, string contains) =>
    Console.WriteLine($"{label,-58} {Report(driver, "MockModels")} {Report(driver, "Intercepts")} {Outputs(driver)} mockHasSub={MockSource(driver).Contains(contains)}");

const string InterfaceV1 = "namespace App; public interface ICalc { int Add(int a, int b); }";
const string InterfaceV2 = "namespace App; public interface ICalc { int Add(int a, int b); int Sub(int a, int b); }";
const string Usage = """
    using Glue.Mocks;
    namespace App;
    public class Tests
    {
        public void One() { var calc = Substitute.For<ICalc>(); calc.Add(1, 2).Returns(3); }
        public void Two([Mock] ICalc calc) { }
    }
    """;
const string Unrelated = "namespace App; public class Other { public int X; }";

Console.WriteLine("== A. Interface in the same compilation, in a different file from the usage");
{
    var iface = Tree(InterfaceV1, "ICalc.cs");
    var usage = Tree(Usage, "Tests.cs");
    var other = Tree(Unrelated, "Other.cs");
    var compilation = Compile("App", platform, iface, usage, other);
    var driver = Driver().RunGenerators(compilation);
    Line("first run", driver, "Sub");

    var other2 = Tree(Unrelated.Replace("X", "Y"), "Other.cs");
    compilation = compilation.ReplaceSyntaxTree(other, other2);
    driver = driver.RunGenerators(compilation);
    Line("edit an unrelated file", driver, "Sub");

    var usage2 = Tree("// a comment\n" + Usage, "Tests.cs");
    compilation = compilation.ReplaceSyntaxTree(usage, usage2);
    driver = driver.RunGenerators(compilation);
    Line("edit the test file above the call site", driver, "Sub");

    var iface2 = Tree(InterfaceV2, "ICalc.cs");
    compilation = compilation.ReplaceSyntaxTree(iface, iface2);
    driver = driver.RunGenerators(compilation);
    Line("add a member to the interface", driver, "Sub");

    var iface3 = Tree(InterfaceV2.Replace("int Sub(int a, int b)", "int Sub(int a, long b)"), "ICalc.cs");
    compilation = compilation.ReplaceSyntaxTree(iface2, iface3);
    driver = driver.RunGenerators(compilation);
    Line("change a parameter type", driver, "long b");

    var iface4 = Tree(InterfaceV2.Replace("int Sub(int a, int b)", "int Sub(int a, long b)") + " // trivia", "ICalc.cs");
    compilation = compilation.ReplaceSyntaxTree(iface3, iface4);
    driver = driver.RunGenerators(compilation);
    Line("add a comment to the interface file", driver, "Sub");
}

Console.WriteLine();
Console.WriteLine("== B. Interface in a referenced assembly (metadata reference)");
{
    MetadataReference Library(string source)
    {
        var library = Compile("Contracts", platform, Tree(source, "ICalc.cs"));
        var stream = new MemoryStream();
        var emit = library.Emit(stream);
        if (!emit.Success) throw new Exception(string.Join("\n", emit.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    var v1 = Library(InterfaceV1);
    var compilation = Compile("App", platform.Append(v1), Tree(Usage, "Tests.cs"));
    var driver = Driver().RunGenerators(compilation);
    Line("first run", driver, "Sub");

    var v1Rebuilt = Library(InterfaceV1 + " // rebuilt, same API, new MVID");
    compilation = compilation.ReplaceReference(v1, v1Rebuilt);
    driver = driver.RunGenerators(compilation);
    Line("referenced assembly rebuilt with the same API", driver, "Sub");

    var v2 = Library(InterfaceV2);
    compilation = compilation.ReplaceReference(v1Rebuilt, v2);
    driver = driver.RunGenerators(compilation);
    Line("referenced assembly adds a member", driver, "Sub");

    var diagnostics = compilation.AddSyntaxTrees(driver.GetRunResult().GeneratedTrees).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "CS9137").ToArray();
    Console.WriteLine($"errors compiling the regenerated mock against v2: {diagnostics.Length}");
}

Console.WriteLine();
Console.WriteLine("== C. The pitfall: a model compared by type name only");
{
    MockGenerator.CompareByNameOnly = true;
    var iface = Tree(InterfaceV1, "ICalc.cs");
    var compilation = Compile("App", platform, iface, Tree(Usage, "Tests.cs"));
    var driver = Driver().RunGenerators(compilation);
    Line("first run", driver, "Sub");

    compilation = compilation.ReplaceSyntaxTree(iface, Tree(InterfaceV2, "ICalc.cs"));
    driver = driver.RunGenerators(compilation, default);
    Line("add a member to the interface", driver, "Sub");

    var errors = compilation.AddSyntaxTrees(driver.GetRunResult().GeneratedTrees).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "CS9137").Select(d => d.Id + ": " + d.GetMessage()).Distinct().ToArray();
    Console.WriteLine($"errors compiling the stale mock: {string.Join(" | ", errors)}");

    var fresh = Driver().RunGenerators(compilation);
    Line("a fresh driver (what a command-line build is)", fresh, "Sub");
    MockGenerator.CompareByNameOnly = false;
}

Console.WriteLine();
Console.WriteLine("== D. Size and time");
{
    string Wide(string name, int members)
    {
        var sb = new StringBuilder($"namespace App; public interface {name} {{ ");
        for (var i = 0; i < members; i++)
        {
            sb.Append((i % 4) switch
            {
                0 => $"int M{i}(int a, string b); ",
                1 => $"System.Threading.Tasks.Task<string> M{i}(System.Guid id, System.Threading.CancellationToken ct); ",
                2 => $"string P{i} {{ get; set; }} ",
                _ => $"bool M{i}(string key, out int value); ",
            });
        }
        return sb.Append('}').ToString();
    }

    void Measure(string label, int interfaces, int members)
    {
        var trees = new List<SyntaxTree>();
        var usage = new StringBuilder("using Glue.Mocks; namespace App; public class Tests { public void T() { ");
        for (var i = 0; i < interfaces; i++)
        {
            trees.Add(Tree(Wide($"IWide{i}", members), $"IWide{i}.cs"));
            usage.Append($"Substitute.For<IWide{i}>(); ");
        }
        usage.Append("} }");
        trees.Add(Tree(usage.ToString(), "Tests.cs"));
        var compilation = Compile("App", platform, trees.ToArray());

        var baselineWatch = Stopwatch.StartNew();
        var baseline = new MemoryStream();
        var features = parse.WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "Glue.Mocks.Generated")]);
        compilation.GetDiagnostics();
        baselineWatch.Stop();

        Driver().RunGenerators(compilation);
        var watch = Stopwatch.StartNew();
        var driver = Driver().RunGenerators(compilation);
        watch.Stop();
        var result = driver.GetRunResult();
        var chars = result.GeneratedTrees.Sum(t => t.Length);
        var lines = result.GeneratedTrees.Sum(t => t.GetText().Lines.Count);

        var unrelated = Tree("namespace App; class Z {}", "Z.cs");
        var edited = compilation.AddSyntaxTrees(unrelated);
        edited.GetDiagnostics();
        var incremental = Stopwatch.StartNew();
        driver = driver.RunGenerators(edited);
        incremental.Stop();

        var withGenerated = compilation.AddSyntaxTrees(result.GeneratedTrees.Where(t => !t.FilePath.Contains("Interceptors")));
        var emitWatch = Stopwatch.StartNew();
        var stream = new MemoryStream();
        var emit = withGenerated.Emit(stream);
        emitWatch.Stop();
        compilation.Emit(baseline);

        Console.WriteLine($"{label,-34} generator {watch.ElapsedMilliseconds,5} ms, rerun after unrelated edit {incremental.ElapsedMilliseconds,4} ms, {lines,7} lines, {chars / 1024,6} KB source, emit {emitWatch.ElapsedMilliseconds,5} ms, IL +{(stream.Length - baseline.Length) / 1024,5} KB, success={emit.Success}");
    }

    Measure("warm up", 2, 10);
    Measure("1 interface x 20 members", 1, 20);
    Measure("1 interface x 500 members", 1, 500);
    Measure("100 interfaces x 20 members", 100, 20);
    Measure("500 interfaces x 20 members", 500, 20);
}
