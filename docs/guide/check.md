# Soft checks with Check

This guide shows how to check several things in one test and see every failure at once. After reading it you can use `Check` with a `VerifyScope` in any test framework.

## Check records a failure and continues

`Verify` throws at the first failure, so the checks after it do not run. `Check` records the failure in the current `VerifyScope` and returns. When the scope ends, it throws one `VerifyException` that lists every failure:

<!-- snippet: check-scope -->
<a id='snippet-check-scope'></a>
```cs
var order = new Order(7, 30, "standard");

using (VerifyScope.Begin())
{
    Check(order.Total == 40);
    Check(order.Shipping == "express");
    Check(order.Lines.Count == 2);
}
```
<sup><a href='/samples/Messages/CheckMessages.cs#L11-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-check-scope' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The test fails with:

```text
2 checks failed

Check failed: order.Total == 40
  expected order.Total to equal 40
  at CheckMessages.cs:16

Check failed: order.Shipping == "express"
  expected order.Shipping to equal "express"
  at CheckMessages.cs:17
```

The [messages page](messages.md#check) shows this output from a real run.

`Check(() => order.Total == 40)` takes the condition as a lambda. Its failure also lists the value of each part, as `Verify(() => ...)` does in the [tiers guide](tiers.md#get-values-in-any-method-with-a-lambda).

## Open a scope

`VerificationGlue.Tests` opens one scope per test, so a test there uses `Check` with no setup.

In xunit, NUnit, MSTest and TUnit, open the scope with `VerifyScope.Begin()` and end it with `using`, as the example above does. A `using` declaration ends the scope at the end of the test method:

```csharp
using var scope = VerifyScope.Begin();
Check(order.Total == 40);
Check(order.Shipping == "express");
```

The scope flows with `async` and `await`. A `Check` in a method that the test awaits reports to the test's scope. A scope begun inside another reports its own failures when it ends, and the outer scope becomes current again.

## Check with no scope

With no open scope, `Check` throws at once, as `Verify` does, so a failure is never lost. Analyzer [VG0007](../diagnostics/VG0007.md) warns about a `Check` in a static constructor, a finalizer or a module initializer, where no scope can be open.

## Checks that return a value

`Throws`, `ThrowsAsync`, `Single`, `Is` and `NotNull` return the value they checked. A failure has no value to return, so these throw inside a scope as well, and record nothing in it. Use them for a value the rest of the test needs. The scope still reports the `Check` failures recorded before the throw, as [When the test throws](#when-the-test-throws) describes.

## Values in a Check

In a `[Verify]` method that tier 3 rewrites, each `Check` lists its values, as `Verify` does:

<!-- snippet: check-tier3 -->
<a id='snippet-check-tier3'></a>
```cs
[Verify]
public static void CheckOrder(Order order)
{
    Check(order.Total == 40);
    Check(order.Shipping.Length > 8);
}
```
<sup><a href='/samples/Messages/CheckMessages.cs#L31-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-check-tier3' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The [tiers guide](tiers.md) explains how to turn tier 3 on.

## When the test throws

The scope ends when the `using` block exits, also when the test throws. If checks have failed by then, the scope's exception replaces the one the test threw. Put the checks after the code that can throw, or use `Verify` for a condition the rest of the test depends on.
