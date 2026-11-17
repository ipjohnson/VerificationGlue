using System.Reflection;
using Generator.Harness;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MockSweep;

namespace VerificationGlue.Mocks.Generator.Specs;

public interface ICalculator
{
    int Add(int a, int b);
    bool TryParse(string text, out int value);
    void Reset();
    Task<int> ComputeAsync(int value);
    ValueTask FlushAsync();
    string? Name { get; set; }
    int this[int index] { get; }
    event EventHandler<int>? Changed;
    T Echo<T>(T value);
    ref int Slot(int index);
}

public class MockRuntimeSpecs
{
    private static readonly Lazy<(ICalculator Mock, string Source)> Generated = new(Generate);

    [Fact]
    public void Generated_source_matches_snapshot() =>
        Snapshot.Match(typeof(MockRuntimeSpecs), nameof(Generated_source_matches_snapshot), Generated.Value.Source);

    [Fact]
    public async Task Unconfigured_members_return_auto_values()
    {
        var calculator = Generated.Value.Mock;

        Assert.Equal(0, calculator.Add(1, 2));
        Assert.False(calculator.TryParse("7", out var parsed));
        Assert.Equal(0, parsed);
        Assert.True(calculator.ComputeAsync(5).IsCompletedSuccessfully);
        Assert.Equal(0, await calculator.ComputeAsync(5));
        Assert.True(calculator.FlushAsync().IsCompletedSuccessfully);
        Assert.Null(calculator.Name);
        Assert.Equal(0, calculator[3]);
        Assert.Null(calculator.Echo<string>("x"));
        calculator.Slot(0) = 4;
        Assert.Equal(0, calculator.Slot(0));
    }

    [Fact]
    public void Every_call_is_recorded_with_its_arguments()
    {
        var calculator = Generate().Mock;

        calculator.Add(1, 2);
        calculator.TryParse("7", out _);
        calculator.Reset();
        calculator.Name = "abc";
        _ = calculator[3];
        calculator.Echo(4L);
        EventHandler<int> handler = (_, _) => { };
        calculator.Changed += handler;
        calculator.Changed -= handler;

        var calls = ((IMock)calculator).Adapter.ReceivedCalls();
        Assert.Equal(
            ["Add(1, 2)", "TryParse(7, 0)", "Reset()", "set_Name(abc)", "get_Item(3)", "Echo<Int64>(4)"],
            calls.Take(6).Select(call => call.ToString()));
        Assert.Equal(["add_Changed", "remove_Changed"], calls.Skip(6).Select(call => call.MemberName));
        Assert.Same(handler, calls[6].Arguments[0]);
        Assert.Equal(calls.Count, calls.Select(call => (call.MemberName.StartsWith("set_") || call.MemberName.StartsWith("remove_") ? -1 : 1) * call.MemberId).Distinct().Count());
    }

    private static (ICalculator Mock, string Source) Generate()
    {
        var compilation = CSharpCompilation.Create(
            "MockRuntimeCase" + Guid.NewGuid().ToString("N"),
            [],
            ShapeCase.FrameworkReferences
                .Append(MockCompiler.RuntimeReference)
                .Append(MetadataReference.CreateFromFile(typeof(ICalculator).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var type = compilation.GetTypeByMetadataName(typeof(ICalculator).FullName!)!;

        var outcome = MockCompiler.Compile(compilation, [type], out var otherErrors, out var compiled).Single();
        Assert.Empty(outcome.Errors);
        Assert.Empty(otherErrors);

        using var image = new MemoryStream();
        var emit = compiled.Emit(image);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        var assembly = Assembly.Load(image.ToArray());
        var mock = (ICalculator)Activator.CreateInstance(assembly.GetType(MockEmitter.GeneratedNamespace + "." + outcome.MockName)!)!;
        return (mock, outcome.Source!);
    }
}
