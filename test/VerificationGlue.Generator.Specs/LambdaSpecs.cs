using System.Reflection;

namespace VerificationGlue.Generator.Specs;

public class LambdaSpecs
{
    private const string Path = "/repo/Project/Tests/Lambdas.cs";

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Sample;

        public sealed record Line(string Sku, int Quantity);

        public sealed class Order(int id, string shipping, List<Line> lines)
        {
            public int Id { get; } = id;
            public string Shipping { get; } = shipping;
            public List<Line> Lines { get; } = lines;
            public int Total => Lines.Sum(line => line.Quantity * 10);
            public override string ToString() => "order " + Id;
        }

        public class Base
        {
            protected int Inherited => 9;
        }

        public sealed class Cases : Base
        {
            private readonly Order _order = NewOrder();
            private static int limit = 3;
            private const int Most = 2;
            private int Expected => 50;
            public int Width { get; } = 4;

            public static Order NewOrder() => new(7, "standard", [new("a", 1), new("b", 3)]);

            public void Local()
            {
                var x = 4;
                Verify(() => x == 7); /*Local*/
            }

            public void Parameter() => WithWidth(4);

            private void WithWidth(int width) => Verify(() => width == 5); /*Parameter*/

            public void MemberChain()
            {
                var order = NewOrder();
                Verify(() => order.Total == 50); /*MemberChain*/
            }

            public void ShortCircuit()
            {
                var order = NewOrder();
                Verify(() => order.Shipping.StartsWith("express") && order.Lines.Count < 3); /*ShortCircuit*/
            }

            public void LoopScope()
            {
                var most = 1;
                for (var i = 0; i < 3; i++)
                {
                    var doubled = i * 2;
                    Verify(() => doubled < most); /*LoopScope*/
                }
            }

            public void ThisMember() => Verify(() => Width == 5); /*ThisMember*/

            public void ThisAndLocal()
            {
                var extra = 1;
                Verify(() => this.Width + extra == 7); /*ThisAndLocal*/
            }

            public void PrivateMembers() => Verify(() => _order.Total == Expected); /*PrivateMembers*/

            public void PrivateMethod()
            {
                var sku = "c";
                Verify(() => HasSku(sku)); /*PrivateMethod*/
            }

            private bool HasSku(string sku) => _order.Lines.Any(line => line.Sku == sku);

            public void PrivateStatic()
            {
                var count = 4;
                Verify(() => count <= limit && count <= Most); /*PrivateStatic*/
            }

            public void Protected() => Verify(() => Inherited == 10); /*Protected*/

            public void NestedLambda()
            {
                var order = NewOrder();
                var minimum = 5;
                Verify(() => order.Lines.Any(line => line.Quantity >= minimum)); /*NestedLambda*/
            }

            public void NoCaptures() => Verify(() => NewOrder().Id == 8); /*NoCaptures*/

            public void SideEffect()
            {
                var queue = new Queue<int>([1, 2, 3]);
                try
                {
                    Verify(() => queue.Dequeue() == 7); /*SideEffect*/
                }
                finally
                {
                    Remaining = queue.Count;
                }
            }

            public int Remaining { get; private set; }

            public void ThrowsInside()
            {
                Order? order = null;
                Verify(() => order!.Total == 1);
            }

            public void Writes()
            {
                var count = 0;
                Verify(() => ++count == 2); /*Writes*/
            }

            public void LocalFunction()
            {
                var n = 3;
                Verify(() => IsEven(n)); /*LocalFunction*/
                static bool IsEven(int value) => value % 2 == 0;
            }

            private sealed record Secret(int Value);

            public void PrivateType()
            {
                var secret = new Secret(3);
                Verify(() => secret.Value == 4); /*PrivateType*/
            }

            public void Generic() => GenericOf(4);

            private static void GenericOf<T>(T value) => Verify(() => value!.Equals(5)); /*Generic*/

            public void Delegate()
            {
                Func<bool> never = () => false;
                Verify(never); /*Delegate*/
            }

            public void CheckWithoutScope()
            {
                var x = 4;
                Check(() => x == 7); /*CheckWithoutScope*/
            }

            public async Task Async()
            {
                var order = NewOrder();
                await Task.Yield();
                var shipped = order.Lines.Count;
                Verify(() => shipped == 3); /*Async*/
            }

            public void Passes()
            {
                var x = 7;
                Verify(() => x == 7);
                Check(() => x == 7);
                Verify(() => HasSku("a") && _order.Total == 40 && Width == 4);
            }
        }

        public sealed class Primary(int size)
        {
            public void Check() => Verify(() => size == 5); /*Primary*/
        }
        """;

    private static readonly Lazy<Assembly> Compiled = new(() => GeneratorHarness.Load((Path, Source)));

    public static IEnumerable<(string Method, string Expected)> Messages =>
    [
        ("Local", "Verify failed: x == 7\n  x → 4"),
        ("Parameter", "Verify failed: width == 5\n  width → 4"),
        ("MemberChain", "Verify failed: order.Total == 50\n  order.Total → 40\n  order       → order 7"),
        ("ShortCircuit",
            "Verify failed: order.Shipping.StartsWith(\"express\") && order.Lines.Count < 3\n" +
            "  order.Shipping.StartsWith(\"express\") → false\n" +
            "  order.Shipping                       → \"standard\"\n" +
            "  order                                → order 7\n" +
            "  order.Lines.Count < 3                → <not evaluated>\n" +
            "  order.Lines.Count                    → <not evaluated>\n" +
            "  order.Lines                          → <not evaluated>"),
        ("LoopScope", "Verify failed: doubled < most\n  doubled → 2\n  most    → 1"),
        ("ThisMember", "Verify failed: Width == 5\n  Width → 4"),
        ("ThisAndLocal", "Verify failed: this.Width + extra == 7\n  this.Width + extra → 5\n  this.Width         → 4\n  extra              → 1"),
        ("PrivateMembers", "Verify failed: _order.Total == Expected\n  _order.Total → 40\n  _order       → order 7\n  Expected     → 50"),
        ("PrivateMethod", "Verify failed: HasSku(sku)\n  sku → \"c\""),
        ("PrivateStatic", "Verify failed: count <= limit && count <= Most\n  count <= limit → false\n  count          → 4\n  limit          → 3\n  count <= Most  → <not evaluated>"),
        ("Protected", "Verify failed: Inherited == 10\n  Inherited → 9"),
        ("NestedLambda", "Verify failed: order.Lines.Any(line => line.Quantity >= minimum)\n  order.Lines → System.Collections.Generic.List`1[Sample.Line]\n  order       → order 7"),
        ("NoCaptures", "Verify failed: NewOrder().Id == 8\n  NewOrder().Id → 7\n  NewOrder()    → order 7"),
        ("SideEffect", "Verify failed: queue.Dequeue() == 7\n  queue.Dequeue() → 1\n  queue           → System.Collections.Generic.Queue`1[System.Int32]"),
        ("Primary", "Verify failed: size == 5\n  size → 4"),
    ];

    public static IEnumerable<(string Method, string Expected)> Fallbacks =>
    [
        ("Writes", "Verify failed: ++count == 2\n  expected ++count to equal 2"),
        ("LocalFunction", "Verify failed: IsEven(n)\n  expected IsEven(n) to return true"),
        ("PrivateType", "Verify failed: secret.Value == 4\n  expected secret.Value to equal 4"),
        ("Generic", "Verify failed: value!.Equals(5)\n  expected value!.Equals(5) to return true"),
        ("Delegate", "Verify failed: never"),
    ];

    [VerifyTest]
    [RowSource(nameof(Messages))]
    public void A_failed_lambda_lists_the_value_of_each_part(string method, string expected)
    {
        var failure = Fail(method);

        Assert.Equal(expected + $"\n  at Tests/Lambdas.cs:{LineOf(method)}", failure.Message);
    }

    [VerifyTest]
    [RowSource(nameof(Fallbacks))]
    public void A_lambda_the_check_cannot_copy_gets_the_tier_2_message(string method, string expected)
    {
        var failure = Fail(method);

        Assert.Equal(expected + $"\n  at Tests/Lambdas.cs:{LineOf(method)}", failure.Message);
    }

    [VerifyTest]
    public void The_condition_runs_once()
    {
        var cases = Create("Cases");

        Assert.Throws<TargetInvocationException>(() => cases.GetType().GetMethod("SideEffect")!.Invoke(cases, null));

        Assert.Equal(2, cases.GetType().GetProperty("Remaining")!.GetValue(cases));
    }

    [VerifyTest]
    public void An_exception_in_the_condition_reaches_the_test()
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Invoke("ThrowsInside"));

        Assert.IsType<NullReferenceException>(thrown.InnerException);
    }

    [VerifyTest]
    public async Task A_lambda_in_an_async_method_lists_its_values()
    {
        var cases = Create("Cases");
        var task = (Task)cases.GetType().GetMethod("Async")!.Invoke(cases, null)!;

        var failure = await Assert.ThrowsAsync<VerifyException>(() => task);

        Assert.Equal($"Verify failed: shipped == 3\n  shipped → 2\n  at Tests/Lambdas.cs:{LineOf("Async")}", failure.Message);
    }

    [VerifyTest]
    public Task A_check_lambda_without_a_scope_throws_with_its_values() => OutsideTheRunnersScope(() =>
    {
        var failure = Fail("CheckWithoutScope");

        Assert.Equal($"Check failed: x == 7\n  x → 4\n  at Tests/Lambdas.cs:{LineOf("CheckWithoutScope")}", failure.Message);
    });

    [VerifyTest]
    public void A_check_lambda_in_a_scope_is_reported_when_the_scope_ends()
    {
        var scope = VerifyScope.Begin();
        Invoke("CheckWithoutScope");

        var failure = Assert.Throws<VerifyException>(scope.Dispose);

        Assert.Equal($"1 check failed\n\nCheck failed: x == 7\n  x → 4\n  at Tests/Lambdas.cs:{LineOf("CheckWithoutScope")}", failure.Message);
    }

    [VerifyTest]
    public void Passing_lambdas_do_not_throw()
    {
        Invoke("Passes");
    }

    [VerifyTest]
    public void A_lambda_in_a_rewritten_method_is_rewritten_from_its_body()
    {
        var failure = FailRewritten("Body");

        Assert.Equal("Verify failed: total > 5\n  total → 3\n  at Tests/Rewritten.cs:11", failure.Message);
    }

    [VerifyTest]
    public void A_lambda_that_declares_a_variable_keeps_its_tier_2_form_in_a_rewritten_method()
    {
        var failure = FailRewritten("Declares");

        Assert.Equal("Verify failed: total is int n && n > 5\n  at Tests/Rewritten.cs:17", failure.Message);
    }

    private static VerifyException FailRewritten(string method)
    {
        var source = """
            using VerificationGlue;
            using static VerificationGlue.VerifySyntax;

            namespace Sample;

            public static partial class Rewritten
            {
                [Verify]
                public static void Body(int total)
                {
                    Verify(() => total > 5);
                }

                [Verify]
                public static void Declares(int total)
                {
                    Verify(() => total is int n && n > 5);
                    var n = 1;
                    n++;
                }

                public static void Call(string name)
                {
                    if (name == "Body") Body(3); else Declares(3);
                }
            }
            """;
        var assembly = GeneratorHarness.Load(rewrite: true, ("/repo/Project/Tests/Rewritten.cs", source));

        var thrown = Assert.Throws<TargetInvocationException>(() => assembly.GetType("Sample.Rewritten")!.GetMethod("Call")!.Invoke(null, [method]));
        return Assert.IsType<VerifyException>(thrown.InnerException);
    }

    // VerificationGlue.Tests runs each test inside a VerifyScope, and CheckWithoutScope needs none.
    private static Task OutsideTheRunnersScope(Action body)
    {
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(body);
        }
    }

    private static object Create(string type) => Activator.CreateInstance(Compiled.Value.GetType("Sample." + type, throwOnError: true)!)!;

    private static object? Invoke(string method)
    {
        var target = method == "Primary" ? Compiled.Value.GetType("Sample.Primary", throwOnError: true)!.GetConstructors()[0].Invoke([4]) : Create("Cases");
        return target.GetType().GetMethod(method == "Primary" ? "Check" : method)!.Invoke(target, null);
    }

    private static VerifyException Fail(string method)
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Invoke(method));
        return Assert.IsType<VerifyException>(thrown.InnerException);
    }

    private static int LineOf(string method)
    {
        var lines = Source.Split('\n');
        var marked = Array.FindIndex(lines, line => line.Contains($"/*{method}*/", StringComparison.Ordinal));
        return marked >= 0 ? marked + 1 : Array.FindIndex(lines, line => line.Contains($"void {method}(", StringComparison.Ordinal)) + 3;
    }
}
