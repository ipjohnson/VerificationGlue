using System.Runtime.CompilerServices;
using Contracts;
using Glue.Mocks;

var failed = 0;

void Check(string name, Action body)
{
    try
    {
        body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception e)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {e.GetType().Name}: {e.Message}");
    }
}

void Equal<T>(T expected, T actual, [CallerArgumentExpression(nameof(actual))] string? text = null)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new Exception($"{text}: expected {expected}, got {actual}");
    }
}

void Throws<TException>(Action body)
    where TException : Exception
{
    try
    {
        body();
    }
    catch (TException)
    {
        return;
    }

    throw new Exception($"expected {typeof(TException).Name}");
}

Console.WriteLine($"Dynamic code supported: {RuntimeFeature.IsDynamicCodeSupported}");

Check("Substitute.For is intercepted", () =>
{
    var calc = Substitute.For<ICalculator>();
    Equal(true, calc is IMock);
});

Check("Mock.Of resolves through the generated registry", () =>
{
    var calc = Mock.Of<ICalculator>();
    Equal(true, calc is IMock);
});

Check("Returns binds a value to the last call", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(1, 2).Returns(3);
    Equal(3, calc.Add(1, 2));
    Equal(0, calc.Add(5, 5));
});

Check("state is per mock", () =>
{
    var first = Substitute.For<ICalculator>();
    var second = Substitute.For<ICalculator>();
    first.Add(1, 2).Returns(3);
    second.Add(1, 2).Returns(30);
    Equal(3, first.Add(1, 2));
    Equal(30, second.Add(1, 2));
    first.Received(1).Add(1, 2);
    second.Received(1).Add(1, 2);
    Equal(false, ReferenceEquals(((IMock)first).Adapter, ((IMock)second).Adapter));
});

Check("Arg.Any and Arg.Is", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(Arg.Any<int>(), Arg.Is<int>(b => b > 10)).Returns(99);
    Equal(99, calc.Add(7, 11));
    Equal(0, calc.Add(7, 10));
});

Check("a matcher mixed with a literal", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(Arg.Any<int>(), 5).Returns(50);
    Equal(50, calc.Add(123, 5));
    Equal(0, calc.Add(123, 6));
});

Check("a matcher after a default-valued literal binds to the right argument", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(0, Arg.Any<int>()).Returns(1);
    Equal(1, calc.Add(0, 5));
    Equal(0, calc.Add(5, 0));
});

Check("a matcher held in a local has no position and falls back to the run-time rule", () =>
{
    var calc = Substitute.For<ICalculator>();
    var any = Arg.Any<int>();
    calc.Add(any, 5).Returns(50);
    Equal(50, calc.Add(9, 5));
});

Check("without a position the run-time rule binds the matcher to the first default-valued argument", () =>
{
    var calc = Substitute.For<ICalculator>();
    var any = Arg.Any<int>();
    calc.Add(0, any).Returns(1);
    Equal(0, calc.Add(0, 5));
    Equal(1, calc.Add(5, 0));
});

Check("Received and DidNotReceive", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(1, 2);
    calc.Add(1, 3);
    calc.Received().Add(1, 2);
    calc.Received(2).Add(1, Arg.Any<int>());
    calc.DidNotReceive().Add(9, 9);
    Throws<ReceivedCallsException>(() => calc.Received().Add(9, 9));
});

Check("the setup call is not counted as received", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(1, 2).Returns(3);
    calc.DidNotReceive().Add(1, 2);
});

Check("sequence of return values", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(1, 1).Returns(1, 2, 3);
    Equal(1, calc.Add(1, 1));
    Equal(2, calc.Add(1, 1));
    Equal(3, calc.Add(1, 1));
    Equal(3, calc.Add(1, 1));
});

Check("Throws", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(1, 2).Throws(new InvalidOperationException("boom"));
    Throws<InvalidOperationException>(() => calc.Add(1, 2));
});

Check("When..Do on a void method", () =>
{
    var calc = Substitute.For<ICalculator>();
    var resets = 0;
    calc.When(c => c.Reset()).Do(_ => resets++);
    calc.Reset();
    calc.Reset();
    Equal(2, resets);
    calc.Received(2).Reset();
});

Check("property setup and automatic backing value", () =>
{
    var calc = Substitute.For<ICalculator>();
    Equal(null, calc.Mode);
    calc.Mode.Returns("deg");
    Equal("deg", calc.Mode);
    calc.Mode = "rad";
    Equal("rad", calc.Mode);
    calc.Received().Mode = "rad";
});

Check("indexer", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc["x"].Returns(7);
    Equal(7, calc["x"]);
    Equal(0, calc["y"]);
    calc["y"] = 9;
    Equal(9, calc["y"]);
});

Check("events", () =>
{
    var calc = Substitute.For<ICalculator>();
    var seen = 0;
    calc.Changed += (_, value) => seen += value;
    calc.Changed += Raise.Event<EventHandler<int>>(calc, 5);
    calc.Changed += Raise.Event<EventHandler<int>>(calc, 6);
    Equal(11, seen);
});

Check("Task<T> and ValueTask<T> setup and auto values", () =>
{
    var calc = Substitute.For<ICalculator>();
    Equal(0, calc.AddAsync(1, 2).GetAwaiter().GetResult());
    Equal(true, calc.SaveAsync().IsCompletedSuccessfully);
    Equal(null, calc.NameAsync().GetAwaiter().GetResult());
    calc.AddAsync(1, 2).Returns(3);
    calc.NameAsync().Returns("calc");
    Equal(3, calc.AddAsync(1, 2).GetAwaiter().GetResult());
    Equal("calc", calc.NameAsync().GetAwaiter().GetResult());
    calc.Received(2).AddAsync(1, 2);
});

Check("out and ref parameters", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.TryParse("42", out Arg.Any<int>()).Returns(info =>
    {
        info[1] = 42;
        return true;
    });
    Equal(true, calc.TryParse("42", out var parsed));
    Equal(42, parsed);
    Equal(false, calc.TryParse("x", out var other));
    Equal(0, other);

    var counter = 1;
    calc.When(c => c.Bump(ref Arg.Any<int>())).Do(info => info[0] = info.ArgAt<int>(0) + 1);
    calc.Bump(ref counter);
    Equal(2, counter);
});

Check("generic method", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Convert<int>("1").Returns(1);
    calc.Convert<string>("1").Returns("one");
    Equal(1, calc.Convert<int>("1"));
    Equal("one", calc.Convert<string>("1"));
    Equal(0.0, calc.Convert<double>("1"));
});

Check("default interface member is mocked", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(2, 2).Returns(4);
    Equal(0, calc.Twice(2));
    calc.Twice(2).Returns(8);
    Equal(8, calc.Twice(2));
});

Check("closed generic interface", () =>
{
    var orders = Substitute.For<IRepository<Order>>();
    orders.Find(1).Returns(new Order(1, "abc"));
    Equal("abc", orders.Find(1)!.Sku);
    orders.Save(new Order(2, "x"));
    orders.Received().Save(Arg.Is<Order>(o => o.Id == 2));
});

Check("system under test gets the same instances", () =>
{
    var calc = Substitute.For<ICalculator>();
    var orders = Substitute.For<IRepository<Order>>();
    orders.Find(7).Returns(new Order(7, "sku"));
    calc.Add(7, 100).Returns(107);
    Equal(107, new OrderService(calc, orders).Total(7));
    calc.Received(1).Add(7, 100);
});

Check("class with virtual members", () =>
{
    var clock = Substitute.For<Clock>();
    clock.Now.Returns(new DateTime(2026, 1, 1));
    Equal(2026, clock.Now.Year);
    Equal(null, clock.Format(default));
    Equal("real", clock.NotVirtual());
});

Check("ref struct parameter is refused at run time", () =>
{
    var reader = Substitute.For<ISpanReader>();
    reader.Length.Returns(4);
    Equal(4, reader.Length);
    Throws<NotSupportedException>(() => reader.Read(new byte[1]));
});

Check("Returns on a non-mock call with no last call throws", () =>
{
    Exception? caught = null;
    var thread = new Thread(() =>
    {
        try
        {
            "abc".Length.Returns(3);
        }
        catch (Exception e)
        {
            caught = e;
        }
    });
    thread.Start();
    thread.Join();
    Equal(true, caught is NoLastCallException);
});

Check("Returns on a non-mock call silently rebinds the previous mock call", () =>
{
    var calc = Substitute.For<ICalculator>();
    calc.Add(1, 2);
    "abc".Length.Returns(3);
    Equal(3, calc.Add(1, 2));
});

Check("parallel threads do not share the last call", () =>
{
    var calc = Substitute.For<ICalculator>();
    Parallel.For(0, 200, i => calc.Add(i, i).Returns(i * 2));
    for (var i = 0; i < 200; i++)
    {
        Equal(i * 2, calc.Add(i, i));
    }
});

Console.WriteLine(failed == 0 ? "ALL PASSED" : $"{failed} FAILED");
return failed;
