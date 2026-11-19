using System.Diagnostics;
using ClosureGlue;
using Shop.Tests;
using static ClosureGlue.Checks;

var cases = new (string Name, Action Run)[]
{
    ("captured local", Cases.Local),
    ("parameter", () => Cases.Parameter(4)),
    ("member chain on a local", Cases.MemberChain),
    ("short-circuit &&", Cases.ShortCircuit),
    ("nested scope", Cases.NestedScope),
    ("loop variable", Cases.Loop),
    ("const local", Cases.ConstLocal),
    ("nested lambda in the condition", Cases.NestedLambda),
    ("static member of the test class", Cases.StaticMember),
    ("this, public member", () => new Cases().ThisPublic()),
    ("this, private field", () => new Cases().ThisPrivateField()),
    ("this, private method", () => new Cases().ThisPrivateMethod()),
    ("this and a local", () => new Cases().ThisAndLocal()),
    ("primary constructor parameter", () => new PrimaryCases(4).Check()),
    ("async method", () => Cases.Async().GetAwaiter().GetResult()),
    ("null-conditional", Cases.NullConditional),
    ("pattern", Cases.Pattern),
    ("anonymous type", Cases.Anonymous),
    ("generic method", () => Cases.Generic(4)),
    ("local function", Cases.LocalFunction),
    ("side effect runs twice", Cases.SideEffect),
    ("no captures", Cases.NoCaptures),
    ("private nested type", Cases.PrivateType),
    ("loop scope chain", Cases.LoopChain),
    ("exception inside the condition", Cases.Throws),
    ("condition writes a captured variable", Cases.Writes),
    ("passing call", Cases.Passing),
};

foreach (var (name, run) in cases)
{
    Console.WriteLine("## " + name);
    try
    {
        run();
        Console.WriteLine("passed");
    }
    catch (VerifyException exception)
    {
        Console.WriteLine(exception.Message);
    }
    catch (Exception exception)
    {
        Console.WriteLine("threw " + exception.GetType().Name);
    }

    Console.WriteLine();
}

if (args.Contains("--perf"))
{
    Perf.Run();
}

namespace Shop.Tests
{
    public sealed record Line(string Sku, int Quantity);

    public sealed class Order(int id, string shipping, List<Line> lines)
    {
        public int Id { get; } = id;
        public string Shipping { get; } = shipping;
        public List<Line> Lines { get; } = lines;
        public decimal Total => Lines.Sum(line => line.Quantity * 10m);
        public override string ToString() => "order " + Id;
    }

    public partial class Cases
    {
        private readonly Order _order = new(7, "standard", [new("a", 1), new("b", 3)]);
        private int Expected => 50;
        public int Width { get; } = 4;
        private static int Limit = 3;

        public static Order NewOrder() => new(7, "standard", [new("a", 1), new("b", 3)]);

        public static void Local()
        {
            var x = 4;
            Verify(() => x == 7);
        }

        public static void Parameter(int width) => Verify(() => width == 5);

        public static void MemberChain()
        {
            var order = NewOrder();
            Verify(() => order.Total == 50);
        }

        public static void ShortCircuit()
        {
            var order = NewOrder();
            Verify(() => order.Shipping.StartsWith("express") && order.Lines.Count < 3);
        }

        public static void NestedScope()
        {
            var outer = 1;
            {
                var inner = 2;
                Verify(() => outer + inner == 4);
            }
        }

        public static void Loop()
        {
            for (var i = 0; i < 3; i++)
            {
                Verify(() => i < 1);
            }
        }

        public static void ConstLocal()
        {
            const int Max = 2;
            var count = 3;
            Verify(() => count <= Max);
        }

        public static void NestedLambda()
        {
            var order = NewOrder();
            var minimum = 5;
            Verify(() => order.Lines.Any(line => line.Quantity >= minimum));
        }

        public static void StaticMember()
        {
            var count = 4;
            Verify(() => count <= Limit);
        }

        public void ThisPublic() => Verify(() => Width == 5);

        public void ThisPrivateField() => Verify(() => _order.Total == Expected);

        public void ThisPrivateMethod()
        {
            var sku = "c";
            Verify(() => HasSku(sku));
        }

        private bool HasSku(string sku) => _order.Lines.Any(line => line.Sku == sku);

        public void ThisAndLocal()
        {
            var extra = 1;
            Verify(() => this.Width + extra == 7);
        }

        public static async Task Async()
        {
            var order = NewOrder();
            await Task.Yield();
            var shipped = order.Lines.Count;
            Verify(() => shipped == 3);
        }

        public static void NullConditional()
        {
            Order? order = null;
            Verify(() => order?.Shipping == "standard");
        }

        public static void Pattern()
        {
            var order = NewOrder();
            Verify(() => order.Lines is [_, { Quantity: > 5 }]);
        }

        public static void Anonymous()
        {
            var pair = new { Left = 1, Right = 2 };
            Verify(() => pair.Left == pair.Right);
        }

        public static void Generic<T>(T value)
        {
            Verify(() => value!.Equals(5));
        }

        public static void LocalFunction()
        {
            var n = 3;
            Verify(() => IsEven(n));
            static bool IsEven(int value) => value % 2 == 0;
        }

        public static void SideEffect()
        {
            var queue = new Queue<int>([1, 2, 3]);
            try
            {
                Verify(() => queue.Dequeue() == 7);
            }
            finally
            {
                Console.WriteLine("queue left: " + queue.Count);
            }
        }

        public static void NoCaptures() => Verify(() => NewOrder().Id == 8);

        private sealed record Secret(int Value);

        public static void PrivateType()
        {
            var secret = new Secret(3);
            Verify(() => secret.Value == 4);
        }

        public static void LoopChain()
        {
            var limit = 1;
            for (var i = 0; i < 3; i++)
            {
                var doubled = i * 2;
                Verify(() => doubled < limit);
            }
        }

        public static void Throws()
        {
            Order? order = null;
            Verify(() => order!.Total == 1);
        }

        public static void Writes()
        {
            var count = 0;
            Verify(() => ++count == 2);
        }

        public static void Passing()
        {
            var x = 7;
            Verify(() => x == 7);
        }
    }

    public sealed class PrimaryCases(int size)
    {
        public void Check() => Verify(() => size == 5);
    }

    public static class Perf
    {
        public static void Run()
        {
            const int Calls = 10_000_000;
            for (var round = 0; round < 3; round++)
            {
                var x = round + 1;
                var watch = Stopwatch.StartNew();
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < Calls; i++)
                {
                    Verify(x > 0);
                }

                var boolTime = watch.Elapsed;
                var boolBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;

                watch.Restart();
                allocated = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < Calls; i++)
                {
                    Verify(() => x > 0);
                }

                var lambdaTime = watch.Elapsed;
                var lambdaBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Console.WriteLine($"bool:   {boolTime.TotalMilliseconds * 1_000_000 / Calls:F2} ns/call, {boolBytes / (double)Calls:F1} B/call");
                Console.WriteLine($"lambda: {lambdaTime.TotalMilliseconds * 1_000_000 / Calls:F2} ns/call, {lambdaBytes / (double)Calls:F1} B/call");
            }
        }
    }
}
