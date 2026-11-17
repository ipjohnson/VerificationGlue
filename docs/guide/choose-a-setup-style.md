# Choose a setup style

After reading this page you can pick between the NSubstitute style and the typed `Setup()` and `Calls()` surface, and mix them on one mock.

Use the typed surface for new code and for libraries. Use the NSubstitute style when you move tests from NSubstitute, or when you prefer it.

## The NSubstitute style

Call the member, then call `Returns` or `Throws` on its result. Check calls with `Received` and `DidNotReceive`:

<!-- snippet: mocks-style-nsubstitute -->
<a id='snippet-mocks-style-nsubstitute'></a>
```cs
var calc = Mock.Of<ICalculator>();
calc.Add(1, Arg.Any<int>()).Returns(3);
calc.Divide(Arg.Any<int>(), 0).Throws(new DivideByZeroException());
calc.Mode.Returns(Mode.Scientific);

var sum = calc.Add(1, 2);

calc.Received(1).Add(1, 2);
calc.DidNotReceive().Reset();
```
<sup><a href='/samples/MocksGuide/SetupStyles.cs#L12-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-style-nsubstitute' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Returns` applies to the last mock call in the same async flow. The generator checks that the receiver of `Returns` is a mock call and reports [VGM0005](../diagnostics/VGM0005.md) when it is not. An `Arg` matcher must be an argument of the mock call, and [VGM0002](../diagnostics/VGM0002.md) reports one that is not.

## The typed surface

`Setup()` and `Calls()` are generated extension methods on each mocked interface. Each of their members has the member's parameters, typed `Arg<T>`:

<!-- snippet: mocks-style-typed -->
<a id='snippet-mocks-style-typed'></a>
```cs
var calc = Mock.Of<ICalculator>();
calc.Setup().Add(1, Arg<int>.Any()).Returns(3);
calc.Setup().Divide(Arg<int>.Any(), 0).Throws(new DivideByZeroException());
calc.Setup().Mode.Returns(Mode.Scientific);

var sum = calc.Add(1, 2);

Verify(calc.Calls().Add(1, 2) == 1);
Verify(calc.Calls().Reset() == 0);
```
<sup><a href='/samples/MocksGuide/SetupStyles.cs#L31-L41' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-style-typed' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A plain value converts to an exact match, and `Arg<int>.Any()` and `Arg<int>.Is(...)` are matchers. `Returns` takes the member's return type, so the compiler checks the value. `Calls()` returns a `CallCount`, which compares with an `int`, so a plain `Verify` checks it. With [tier 3](tiers.md) on, a failed `Verify` lists the calls the member received.

The typed surface uses no state between statements, so no setup can bind to the wrong call. It has no member for an event, for a member that [VGM0003](../diagnostics/VGM0003.md) reports, or for a second member with the same C# signature as an earlier one.

## Mix them

Both styles drive the same mock, so a test can use each where it reads better:

<!-- snippet: mocks-style-mixed -->
<a id='snippet-mocks-style-mixed'></a>
```cs
var calc = Mock.Of<ICalculator>();
calc.Add(1, 2).Returns(3);
calc.Setup().Add(5, 5).Returns(10);

calc.Add(1, 2);
calc.Add(5, 5);

calc.Received().Add(5, 5);
Verify(calc.Calls().Add(Arg<int>.Any(), Arg<int>.Any()) == 2);
```
<sup><a href='/samples/MocksGuide/SetupStyles.cs#L50-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-style-mixed' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Side by side

| Task | NSubstitute style | Typed surface |
|---|---|---|
| Return a value | `calc.Add(1, 2).Returns(3)` | `calc.Setup().Add(1, 2).Returns(3)` |
| Return values in turn | `calc.Add(1, 2).Returns(1, 2, 3)` | `calc.Setup().Add(1, 2).Returns(1, 2, 3)` |
| Throw | `calc.Divide(1, 0).Throws(error)` | `calc.Setup().Divide(1, 0).Throws(error)` |
| Run code on a call | `calc.When(c => c.Reset()).Do(...)` | `calc.Setup().Reset().Does(...)` |
| Any value | `Arg.Any<int>()` | `Arg<int>.Any()` |
| A value that passes a test | `Arg.Is<int>(x => x > 0)` | `Arg<int>.Is(x => x > 0)` |
| Check a call count | `calc.Received(2).Add(1, 2)` | `Verify(calc.Calls().Add(1, 2) == 2)` |
| Check that a call did not happen | `calc.DidNotReceive().Reset()` | `Verify(calc.Calls().Reset() == 0)` |

C# applies no implicit conversion from an interface type, so an exact match on an interface-typed parameter is `Arg<IClock>.Is(clock)` in the typed surface. [Arguments and matchers](arguments-and-matchers.md#typed-matchers) shows it.
