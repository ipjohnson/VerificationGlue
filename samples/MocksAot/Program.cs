using VerificationGlue.Mocks;

[assembly: GenerateMock(typeof(IClock))]

var failures = new List<string>();

void Check(bool condition, string what)
{
    if (!condition)
    {
        failures.Add(what);
    }
}

void Fails<TException>(Action action, string what)
    where TException : Exception
{
    try
    {
        action();
        failures.Add(what + ": nothing was thrown");
    }
    catch (TException)
    {
    }
}

var calc = Mock.Of<ICalculator>();

calc.Add(0, Arg.Any<int>()).Returns(9);
Check(calc.Add(0, 5) == 9, "Returns with Arg.Any in the second argument");
Check(calc.Add(1, 5) == 0, "Arg.Any binds to the argument it is written in");

calc.Add(Arg.Is<int>(a => a > 100), 1).Returns(-1);
Check(calc.Add(101, 1) == -1 && calc.Add(100, 1) == 0, "Arg.Is with a predicate");

calc.Divide(Arg.Any<int>(), 0).Throws(new DivideByZeroException());
Fails<DivideByZeroException>(() => calc.Divide(4, 0), "Throws");

calc.TryParse("7", out Arg.Any<int>()).Returns(call =>
{
    call[1] = 7;
    return true;
});
Check(calc.TryParse("7", out var parsed) && parsed == 7, "Returns with a factory that sets an out argument");

calc.ComputeAsync(5).Returns(25);
Check(await calc.ComputeAsync(5) == 25, "Returns on a Task<int> member");

calc.Add(2, 2).Returns(await LoadAsync());
Check(calc.Add(2, 2) == 40, "Returns(await ...) binds to the call before the await");

var resets = 0;
calc.When(c => c.Reset()).Do(_ => resets++);
calc.Reset();
Check(resets == 1, "When/Do");

var raised = 0;
calc.Changed += (_, value) => raised = value;
calc.Changed += Raise.Event<EventHandler<int>>(calc, 5);
Check(raised == 5, "Raise.Event");

calc.Received().Reset();
calc.Received(1).Add(0, 5);
calc.DidNotReceive().Add(3, 3);
Fails<ReceivedCallsException>(() => calc.Received().Add(7, 7), "Received on a call that was not made");
Fails<ReceivedCallsException>(() => calc.DidNotReceive().Reset(), "DidNotReceive on a call that was made");

var clock = Make<IClock>();
clock.Now.Returns(new DateTime(2026, 11, 17));
Check(clock.Now == new DateTime(2026, 11, 17), "Mock.Of<T>() in a generic helper with [assembly: GenerateMock]");
Check(MockRegistry.Create(typeof(ICalculator)) is ICalculator, "MockRegistry.Create(Type)");

foreach (var failure in failures)
{
    Console.Error.WriteLine("MocksAot failed: " + failure);
}

Console.WriteLine(failures.Count == 0 ? "MocksAot passed." : $"MocksAot: {failures.Count} checks failed.");
return failures.Count == 0 ? 0 : 1;

static async Task<int> LoadAsync()
{
    await Task.Yield();
    return 40;
}

static T Make<T>()
    where T : class => Mock.Of<T>();

public interface ICalculator
{
    int Add(int a, int b);

    int Divide(int a, int b);

    bool TryParse(string text, out int value);

    void Reset();

    Task<int> ComputeAsync(int value);

    event EventHandler<int>? Changed;
}

public interface IClock
{
    DateTime Now { get; }
}
