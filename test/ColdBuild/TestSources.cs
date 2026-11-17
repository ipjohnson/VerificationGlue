using System;
using System.Collections.Generic;
using System.Linq;

namespace ColdBuild;

/// <summary>
/// The test files of one benchmark assembly. Each class holds up to ten <c>[VerifyTest]</c> methods
/// and one <c>[Verify]</c> helper that every test in it calls, so tier 3 has a call to redirect.
/// </summary>
public sealed class TestSources(string assemblyName, int tests, int callsPerTest)
{
    public const int TestsPerClass = 10;

    private static readonly Func<int, string>[] Shapes =
    [
        _ => "order is not null",
        test => $"order.Id == {test + 1}",
        _ => "order.Name != \"x\"",
        _ => "count > 0",
        _ => "count <= 3 && order.Id >= 1",
        test => $"order is {{ Name: \"order{test}\" }}",
        _ => "order.Name.StartsWith(\"order\")",
        _ => "!string.IsNullOrEmpty(order.Name)",
        _ => "order.Lines[0] == 1 || order.Lines[1] == 2",
        _ => "order.Lines.Count + count == 6",
    ];

    public int Classes => (tests + TestsPerClass - 1) / TestsPerClass;

    /// <summary>Every <c>Verify</c> call: the calls in each test and the one in each helper.</summary>
    public int Calls => (tests * callsPerTest) + Classes;

    public IEnumerable<(string Path, string Text)> Files()
    {
        yield return ("Order.cs", $$"""
            using System.Collections.Generic;

            namespace {{assemblyName}};

            public sealed class Order(int id, string name, IReadOnlyList<int> lines)
            {
                public int Id { get; } = id;

                public string Name { get; } = name;

                public IReadOnlyList<int> Lines { get; } = lines;
            }

            """);

        // Stands in for [VerifyTest] from VerificationGlue.Tests, whose runner always calls the copy.
        yield return ("VerifyTestAttribute.cs", $$"""
            namespace {{assemblyName}};

            [VerificationGlue.VerifyRewrite]
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class VerifyTestAttribute : System.Attribute
            {
            }

            """);

        for (var index = 0; index < Classes; index++)
        {
            var first = index * TestsPerClass;
            var count = Math.Min(TestsPerClass, tests - first);
            yield return ($"OrderTests{index}.cs", Class(index, Enumerable.Range(first, count)));
        }
    }

    private string Class(int index, IEnumerable<int> testNumbers)
    {
        var methods = string.Concat(testNumbers.Select(Test));
        return $$"""
            using VerificationGlue;
            using static VerificationGlue.VerifySyntax;

            namespace {{assemblyName}};

            public partial class OrderTests{{index}}
            {
                [Verify]
                private static void VerifyOrder(Order order)
                {
                    Verify(order.Lines.Count == 3);
                }
            {{methods}}
            }

            """;
    }

    private string Test(int test)
    {
        var calls = string.Concat(Enumerable.Range(0, callsPerTest).Select(call => $"        Verify({Shapes[call % Shapes.Length](test)});\n"));
        return $$"""


                [VerifyTest]
                public void Test{{test}}()
                {
                    var order = new Order({{test + 1}}, "order{{test}}", new[] { 1, 2, 3 });
                    var count = order.Lines.Count;
            {{calls}}        VerifyOrder(order);
                }
            """;
    }
}
