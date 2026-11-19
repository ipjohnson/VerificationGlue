# How tiers work and how to get values

This guide explains which message a failed check gives and why. After reading it you can turn on captured values for a method, and tell why a method still gives a message without them.

## The three tiers

Each call gets the best message its call site allows. The [messages page](messages.md) shows an example of each.

| Tier | Applies to | Message |
|---|---|---|
| 1 | Every call when the generator does not run | The source text of the condition |
| 2 | Every call when the generator runs | A message for the shape of the condition, with the file and line |
| 3 | A `[Verify]` method that tier 3 rewrites, when its copy runs | The value of each part of the condition, with the file and line |

Tiers 1 and 2 need nothing beyond the package. Tier 3 is off until the project turns it on.

## Get values in any method with a lambda

Write the condition as a lambda to get the value of each part without tier 3:

```cs
Verify(() => order.Shipping.StartsWith("express") && order.Lines.Count < 3);
Check(() => order.Total == 40);
```

The generator writes a check for each call. The check reads the variables the lambda captured from the delegate, then runs the lambda's body once with each part kept in a local, as a tier 3 copy does. The message lists the same values as tier 3. The [messages page](messages.md#a-lambda-values-in-any-method) shows it.

This works in any method, including a test body in xunit, NUnit, MSTest and TUnit, which tier 3 cannot reach. It needs no `[Verify]`, no `partial` and no `VerificationGlueRewrite`. Under Native AOT the check keeps the captured fields itself, so the project needs no trimming settings.

The lambda form has these limits:

- The body must be one expression. A block body gives the tier 2 message.
- When the body uses something the check cannot reach, the call gives the tier 2 message. This happens for a local function, a private nested type, a variable whose type is a type parameter or an anonymous type, a write to a captured variable, and `base`. Private fields, properties and methods of the type work.
- `Verify(() => ...)` returns nothing, so `.Because(...)` does not apply. Use an interpolated message with `Verify(condition, $"...")` for a reason.
- `Verify(() => order != null)` does not tell the compiler that `order` is not null afterwards. Use `NotNull(order)` for that.
- The lambda itself runs only when the check cannot read its closure. Otherwise the check runs a copy of the body, which `#line` maps to the line of the call.

## Turn on tier 3

Set `VerificationGlueRewrite` in the test project:

<!-- snippet: enable-tier-3 -->
<a id='snippet-enable-tier-3'></a>
```csproj
<PropertyGroup>
  <VerificationGlueRewrite>true</VerificationGlueRewrite>
</PropertyGroup>
```
<sup><a href='/samples/Messages/Messages.csproj#L6-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-enable-tier-3' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Then mark each method whose checks should report values with `[Verify]`, and make every type that contains it `partial`:

<!-- snippet: message-tier3-values -->
<a id='snippet-message-tier3-values'></a>
```cs
[Verify]
public static void VerifyShipping(Order order)
{
    Verify(order.Shipping.StartsWith("express") && order.Lines.Count < 3);
}
```
<sup><a href='/samples/Messages/Tier3Messages.cs#L21-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-message-tier3-values' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A failed check in that method lists each value:

```text
Verify failed: order.Shipping.StartsWith("express") && order.Lines.Count < 3
  order.Shipping.StartsWith("express") → false
```

The [messages page](messages.md#values) has the full message.

## What tier 3 does

For each eligible method, the generator writes a copy named `{Name}__Verified` into the same type. In the copy, each `Verify` or `Check` statement stores each part of its condition in a local as it runs. The condition keeps its shape, so each part runs as often and in the same order as in the original. On failure the message reads those locals. Nothing in the condition runs a second time.

The generator also sends each direct call of the method to the copy. A call from your code, such as `VerifyShipping(order)`, runs `VerifyShipping__Verified(order)`. `#line` directives map the copy to your file, so breakpoints and stack traces point at your source. A stack frame shows the `__Verified` name.

The [generated code page](generated/rewritten-methods.md) shows a copy.

## When a method gets tier 3

The method must meet all of these. Otherwise it keeps tier 2 messages.

- It has `[Verify]`, or an attribute marked `[VerifyRewrite]` such as `[VerifyTest]` from `VerificationGlue.Tests`.
- Every type that contains it is `partial`, not generic, and not `private` or `protected`. Analyzer [VG0005](../diagnostics/VG0005.md) reports a type that is not `partial` and has a fix that adds it.
- It is an ordinary method that is not `virtual`, `abstract`, `override`, generic or `extern`, and it returns by value. In a struct it is `static`.
- Its parameter and return types are not `private` or `protected` nested types, pointers or `dynamic`.
- Its body has no syntax errors and no `#if` directives.
- Its file declares no `file` type. Analyzer [VG0008](../diagnostics/VG0008.md) reports this.

The checks in the copy that get values are `Verify` and `Check` statements. A call inside a lambda or a local function keeps its tier 2 message, because the copy cannot store values across that boundary.

## When the copy runs

The copy runs only when something calls it.

| Call | What runs |
|---|---|
| A direct call from your code to an eligible `[Verify]` method | The copy, through the redirect |
| A call to an eligible method from inside another copy | The copy |
| An xunit or NUnit test method marked `[Verify]` | The original, because the framework calls it through reflection. Its checks give tier 2 messages. |
| A `[VerifyTest]` method in `VerificationGlue.Tests` | The copy, because the runner calls it directly |

In xunit, NUnit, MSTest and TUnit, write the checks that need values in the test body as `Verify(() => ...)`, or put them in a `[Verify]` helper and call it from the test. A `Verify(condition)` in the test body itself gets tier 2.

## Values that are not captured

Some parts cannot be stored in a local without changing what the code does. The message shows `<unavailable>` for them, and analyzer [VG0006](../diagnostics/VG0006.md) reports them as information:

- a `ref struct` value, such as `Span<int>`, or a pointer,
- the receiver of a call when the receiver is a mutable struct,
- the members a property pattern tests.

A part on the side of `&&`, `||`, `?:` or `??` that did not run shows `<not evaluated>`.

## Turn tier 3 off for one IDE

Breakpoints bind in your source and hit the copy in Rider. Visual Studio and VS Code have not passed the debugging checks yet, as [`docs/research/ide-report.md`](../research/ide-report.md) records. To debug a `[Verify]` method there, build with tier 3 off:

```sh
dotnet build -p:VerificationGlueRewrite=false
```
