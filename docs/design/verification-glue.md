# VerificationGlue: Verify syntax and generator

Status: design. Parent: `docs/north-star.md`. Research: `docs/research/design-report.md`, sections 3 and 4.

## What the user gets

- `Verify(list.Count > 3)` fails with a message that names the expression, the value of every sub-expression, and the file and line. Nobody writes the message by hand.
- The message is the same under Native AOT, where stack traces carry no file or line.
- `Check(cond)` records a failure and lets the test continue. The test then fails with every recorded failure listed.
- The package works in xunit, NUnit, MSTest, TUnit and plain application code. `VerificationGlue.Tests` adds nothing the user has to learn.
- Misuse is a build error with a clear message.

```
Verify failed: s.StartsWith("x") && s.Length < 10
  s.StartsWith("x") → false
  s                 → "hello"
  s.Length < 10     → <not evaluated>
  at OrderTests.cs:25
```

## Surface

```csharp
using static VerificationGlue.VerifySyntax;

Verify(order.Total == 40);                               // throws at once
Verify(order.Total == 40, $"order {order.Id}");          // message holes evaluate only on failure
Verify(order.Total == 40).Because("discount applies");   // only inside [Verify] methods
Check(order.Lines.Count == 2);                           // recorded, test continues
var ex = Throws<InvalidOperationException>(() => cart.Checkout());
var ex2 = await ThrowsAsync<TimeoutException>(() => client.GetAsync(url));
Verify(actual == Approx(3.14, 0.001));
await Eventually(() => queue.Count == 3, TimeSpan.FromSeconds(2));
Verify(await repo.CountAsync() == 3);                    // await works inside Verify
```

| Member | Behaviour |
|---|---|
| `Verify(bool, [CallerArgumentExpression] string? = null, ...)` | Marked `[DoesNotReturnIf(false)]`, so nullable flow analysis knows the condition after the call. Throws `VerifyException` on failure. |
| `Verify(bool, ref VerifyMessageHandler)` | Interpolated message. `[InterpolatedStringHandlerArgument]` makes the holes run only on failure. Verified in the spike. |
| `Check(bool, ...)` | Adds a failure to the current `VerifyScope`. With no scope it throws like `Verify`, so a failure is never lost. |
| `Throws<T>(Action)`, `ThrowsAsync<T>(Func<Task>)` | Return the exception. A different exception type fails with that exception's type and message. |
| `Approx(double expected, double tolerance)` | A value whose `==` uses the tolerance. The explanation reports the difference. |
| `Eventually(Func<bool>, TimeSpan)` | Polls until true or timeout. On timeout it reports the last values. |
| `VerifyScope.Begin()` | Opens a scope for `Check` outside `VerificationGlue.Tests`, which opens one per test. Disposing the scope throws one `VerifyException` listing every failure. |

`Verify.Throws<T>` is not offered. A type named `Verify` cannot coexist with a `using static` method named `Verify` (CS0119, seen in the spike).

## Three tiers

The package gives the best message each call site allows. A user never has to opt in to a tier.

| Tier | Applies to | Message | Mechanism |
|---|---|---|---|
| 1 | Every call, generator disabled or not running | Source text of the condition | `[CallerArgumentExpression]` |
| 2 | Every `Verify` and `Check` call in the project | Shape-specific text, for example "x can not be null", plus file and line embedded at compile time | One interceptor per call site |
| 3 | Calls inside `[Verify]` methods and methods whose attribute is marked `[VerifyRewrite]` | Every sub-expression's value | A rewritten copy of the method |

`[VerifyTest]` from `VerificationGlue.Tests` carries `[VerifyRewrite]`, so every test method gets tier 3. Any other library can mark its own attribute the same way.

### Tier 2: interceptors

For each `Verify` or `Check` invocation, the generator emits a method with `[InterceptsLocation(1, data)]`, where `data` comes from `SemanticModel.GetInterceptableLocation`. The interceptor takes the same parameters, checks the boolean, and on failure throws a message built at compile time.

```csharp
[InterceptsLocation(1, "...")]
public static void Verify_15_9(bool condition, string? expression = null)
{
    if (!condition)
    {
        throw VerifyFailure.Static("x is not null", "x can not be null", "OrderTests.cs", 15);
    }
}
```

The shape-specific text covers null checks, `==` and `!=`, relational operators, `is` type and property patterns, `!`, and calls returning `bool`. Anything else uses the source text.

The package `build/VerificationGlue.props` adds `VerificationGlue.Generated` to `InterceptorsNamespaces`. Without it the build fails with CS9137.

### Tier 3: rewritten methods

For each qualifying method in a `partial` type, the generator emits `{Name}__Verified` in the same type with the same signature. Its body is the original body with each `Verify` or `Check` statement replaced. Every other statement is copied as text with `#line` directives that map it to the original file. Interceptors redirect direct calls of the original method to the copy. The test runner in `VerificationGlue.Tests` calls the copy directly.

The rewrite assigns each sub-expression to a typed local as it is evaluated, `(v0 = expr)`. The expression keeps its shape, so evaluation order and short-circuiting do not change.

```csharp
// Verify(s.StartsWith("x") && s.Length < 10);
string v0 = default!; bool v1 = default; int v3 = default; bool v4 = default;
if (!((v1 = (v0 = s).StartsWith("x")) && (v4 = (v3 = v0.Length) < 10)))
{
    throw VerifyFailure.Start("s.StartsWith(\"x\") && s.Length < 10", "OrderTests.cs", 25)
        .Value("s.StartsWith(\"x\")", v1)
        .Value("s", v0)
        .Value("s.Length < 10", v4, evaluated: v1)
        .Value("s.Length", v3, evaluated: v1)
        .Build();
}
```

Rules:

1. Each sub-expression is evaluated exactly once. The spike checked this with a counter: `Next() + Next() == 10 || Next() == 0` called `Next()` three times.
2. Locals are typed, so the success path does not box or allocate. Formatting happens on the failure path only.
3. Constants and literals are not captured.
4. Repeated reads of the same local or parameter are captured once. The spike printed `s` twice, which this rule fixes.
5. A sub-expression on the skipped side of `&&`, `||`, `?:` or `??` reports `<not evaluated>`.
6. Each rewrite is an `if` statement, not a block, so `out var` declared in a `Verify` stays in scope for the following statements. Verified in the spike.
7. `await` inside the condition stays in place. Verified in the spike.
8. Property patterns are explained member by member on the failure path. Each member is re-read only when the pattern has already failed, using `x is { Name: var p0 }`.
9. A sub-expression whose type cannot be stored in a local, such as a `ref struct` or a pointer, is not captured. The message shows its text, and analyzer VG0006 reports it as information.
10. A mutable struct receiver is not copied into a local, because a copy would take the mutation. It is reported by text only.

### Contract with `VerificationGlue.Tests`

Generators cannot see each other's output. The test generator emits a call to `{Name}__Verified` without seeing that method. Both generators therefore compile the same decision function from `src/Shared.Generator/VerifyRewriteRules.cs`:

```csharp
static bool WillRewrite(IMethodSymbol method)   // in a partial type chain, has [Verify] or a [VerifyRewrite] attribute, body has no errors
```

When the function returns false, the test generator calls the original method and the test gets tier 2 messages. A contract test in `test/Contract.Specs` builds a project with both generators and checks that every combination compiles.

### Explanations by shape

The generator knows the static types at each call site, so it picks an explanation without reflection.

| Shape | Extra lines in the message |
|---|---|
| `string == string` | First difference with index and the two characters |
| `a.SequenceEqual(b)` | First differing index and both elements, or the length difference |
| `items.Contains(y)` | `items` (capped) and `y` |
| `dict.ContainsKey(k)` | The key and up to 10 existing keys |
| `x == Approx(...)` | Difference and tolerance |
| `x is { ... }` | Each member: value and whether it matched |
| A call returning `VerifyOutcome` | The outcome's `Explanation` |

### Formatting values

- Strings are quoted and escaped. Long strings are cut at 200 characters with a marker.
- Collections that implement `ICollection` or `IReadOnlyCollection<T>` show up to 10 items. A lazy `IEnumerable` is not enumerated a second time. It shows its type name.
- Records and types that override `ToString` use it. Other types show their type name, or a member dump the generator emits when the type is in the compilation.
- A static method marked `[VerifyFormatter]` with signature `static string Format(T value)` replaces the default for `T`. The generator binds it by static type at each capture site, so there is no registry.

### Custom verifiers

Any method returning `bool` already shows its receiver and arguments. A method returning `VerifyOutcome` adds its own text:

```csharp
public static VerifyOutcome IsSorted<T>(IReadOnlyList<T> items) where T : IComparable<T> { ... }

Verify(IsSorted(prices));
```

`VerifyOutcome` converts implicitly to `bool`. A helper marked `[Verify]` gets full values from its own `Verify` calls.

## Analyzers

| ID | Severity | Reports |
|---|---|---|
| VG0001 | Error | `.WithMessage` or `.Because` outside a method that tier 3 rewrites |
| VG0002 | Warning | `Verify` on a constant, such as `Verify(true)` |
| VG0003 | Warning | A side effect in the condition, such as `i++` or an assignment |
| VG0004 | Warning | `==` on `float` or `double`. The fix uses `Approx`. |
| VG0005 | Info | A `[Verify]` method in a type that is not `partial`, with a code fix that adds `partial` up the chain |
| VG0006 | Info | A sub-expression that cannot be captured |
| VG0007 | Warning | `Check` in a method that returns before any scope can report, such as a `static` constructor |

## Pipeline and caching

The generator has two independent pipelines. Neither model holds a Roslyn object. Collections are `EquatableArray<T>`. Types are CSharpAuthor `ITypeDefinition`. See the CSharpAuthor section of the north star for the rules.

| Pipeline | Discovered with | Model | Output |
|---|---|---|---|
| Call sites | `CreateSyntaxProvider` on invocations named `Verify` or `Check`, then a semantic check of the target | `VerifyCallModel`: file path, line, interceptable location data, condition shape (kind, operator, operand texts) | One file per source file holding all its interceptors |
| Methods | `ForAttributeWithMetadataName` on `[Verify]`, plus a second provider for attributes marked `[VerifyRewrite]` | `VerifyMethodModel`: containing type chain, signature, body statements (text, line, and for `Verify` statements a capture tree of `ITypeDefinition` and texts) | One file per rewritten method |

Why it is split this way:

- Interceptor location data changes on every edit above a call. Keeping it out of the method model means a rewritten method is regenerated only when its own text or line changes.
- One file per source file for interceptors keeps an edit to one file from regenerating the others.
- Line numbers are part of the method model, so an edit above a method regenerates that method. This is the price of `#line` mapping. The cold-build benchmark measures it.

CSharpAuthor use:

- `KnownTypes` holds `static readonly` definitions for `VerifyFailure`, `VerifyException`, `VerifyScope`, `VerifyOutcome` and `InterceptsLocationAttribute`.
- Shared `Ex` nodes for `VerifyFailure.Start`, `.Value`, `.Build` and `throw`.
- The capture tree in the model is turned into `Ex` nodes in the output step. Copied statements are `Raw` nodes preceded by `LineDirectiveComponent.At(line, file)`. The copy has `#pragma warning disable` around it so warnings are not reported twice.

## Performance

Figures from the spike, on a loaded machine with a generator that was not incremental. The real targets come from phase 0.

- 2000 `[Verify]` methods with 6000 calls: 2.5 s without the generator, 6.6 s with it. About 0.7 ms per call.
- At 100,000 calls that is about 70 s of total build time across all assemblies.

Proposed budgets, to be confirmed by the phase 0 benchmark:

- Tier 2 adds no more than 0.1 ms per call to a cold build.
- Tier 3 adds no more than 0.4 ms per call to a cold build.
- An edit to one test file regenerates only that file's outputs.

## Documentation

This package owns these pages. The rules for producing and checking them are in the north star.

- README and `docs/guide/getting-started-verify.md`, with one sample per host framework.
- `docs/guide/messages.md`, generated from `samples/Messages`, one failing test per explanation shape and per tier.
- Guides: "How tiers work and how to get values", "Soft checks with `Check`", "Write a formatter", "Write a custom verifier with `VerifyOutcome`".
- Migration guides from xunit `Assert`, FluentAssertions and Shouldly.
- `docs/diagnostics/VG0001.md` to `VG0007.md`.
- A generated-code page showing a tier 2 interceptor and a tier 3 rewritten method.

## Delivery

### Phase 0: spikes that decide the approach

When done, we know whether tier 3 can be the default.

- [ ] Debugging spike: breakpoints, stepping and Hot Reload in a tier 3 method in Visual Studio, Rider and VS Code, with the result written down per IDE.
- [ ] Cold-build benchmark: 10,000 tests with 10 calls each across three assemblies, measured for tier 2 alone and for tier 3, with an incremental generator built on CSharpAuthor.
- [ ] Decision recorded: tier 3 on by default, or opt-in through a property.

### Phase 1: messages from source text and shape

When done, a user on any test framework installs `VerificationGlue` and gets file, line and shape-specific messages, including under Native AOT.

- [ ] `VerifySyntax` with `Verify`, `Throws`, `ThrowsAsync` and the interpolated message form.
- [ ] Tier 2 interceptors for every listed shape.
- [ ] `build/VerificationGlue.props` sets `InterceptorsNamespaces`.
- [ ] Analyzers VG0002, VG0003 and VG0004.
- [ ] A Native AOT sample in CI with no warnings.
- [ ] Sample tests in xunit, NUnit, MSTest and TUnit in CI.

### Phase 2: values

When done, a `Verify` in a `[Verify]` method reports every sub-expression's value, each evaluated once.

- [ ] Tier 3 rewrite for every rule listed above, with tests for each rule.
- [ ] The shared `WillRewrite` function and the contract test.
- [ ] Analyzers VG0001, VG0005 and VG0006, and the code fix for VG0005.
- [ ] Duplicate compiler errors suppressed by skipping the copy when the original body has errors.
- [ ] The cold-build budget met in CI.

### Phase 3: soft checks and explanations

When done, `Check` collects failures, and strings, collections and patterns get specific explanations.

- [ ] `Check`, `VerifyScope` and analyzer VG0007.
- [ ] The explanation table above.
- [ ] Value formatting rules and `[VerifyFormatter]`.
- [ ] `VerifyOutcome`, `Approx` and `Eventually`.

## Risks and open questions

- Breakpoints and Hot Reload in the copy are untested. Phase 0 answers this.
- A copy of a virtual method would skip overrides when called through the redirect. Only non-virtual methods are redirected. Overrides get tier 2.
- Each rewritten body compiles twice. The budget above bounds the cost.
- Stack frames show `{Name}__Verified`. A runner can strip the suffix when it reports a stack trace. Other frameworks will show it.
- xunit and NUnit call test methods through reflection, so their test bodies get tier 2. `[Verify]` helpers called from those tests get tier 3.
