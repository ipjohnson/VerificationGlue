# VerificationGlue: Verify syntax and generator

Status: design. Parent: `docs/north-star.md`. Research: `docs/research/design-report.md`, sections 3 and 4.

## What the user gets

- `Verify(list.Count > 3)` fails with a message that names the expression, the file and line with the generator enabled, and captured sub-expression values when eligible tier 3 rewriting is enabled. Nobody writes the message by hand.
- The message is the same under Native AOT, where stack traces carry no file or line.
- `Check(cond)` records a failure and lets the test continue. The test then fails with every recorded failure listed.
- The package works in xunit, NUnit, MSTest, TUnit and plain application code. `VerificationGlue.Tests` adds nothing the user has to learn.
- Misuse is a build error with a clear message.

### Host and call-site capabilities

Tier 3 remains opt-in until the phase 0 gates pass. The value-rich message below requires an eligible method in a `partial` containing-type chain and enabled rewriting; installing the package alone does not guarantee values.

| Host or call site | Message capability |
|---|---|
| xunit or NUnit test body invoked through reflection | Tier 2 with the generator enabled, even if the body has `[Verify]` |
| MSTest, TUnit or another host invoking the original body without redirection | Tier 2 with the generator enabled; no automatic tier 3 promise |
| Direct call from user source to an eligible non-virtual `[Verify]` helper in a `partial` type chain | Tier 3 when rewriting is enabled and the call is redirected |
| Eligible `[VerifyTest]` method invoked by `VerificationGlue.Tests` | Tier 3 when rewriting is enabled and the runner calls the generated copy |
| Helper called from a rewritten body | Tier 3 only when that call is explicitly redirected to an eligible generated copy; copying the original call alone is insufficient |
| Rewriting disabled, a non-partial type chain, or an unsupported method | Original method with tier 2 when the generator is enabled |
| Generator disabled or not running | Tier 1 source text only |
| An individual value cannot be captured without changing behavior | Its text with the value marked unavailable; skipped expressions say `<not evaluated>` |

Phase 0 validates the advertised IDE support and the default; it does not remove these call-site restrictions.

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
| `Verify(bool, string? message = null, [CallerArgumentExpression] string? expression = null)` | Marked `[DoesNotReturnIf(false)]`, so nullable flow analysis knows the condition after the call. Throws `VerifyException` on failure. A plain string as the second argument is the message. The message comes before the expression so that the string never binds to the expression and replaces the condition's text. |
| `Verify(bool, ref VerifyMessageHandler)` | Interpolated message. `[InterpolatedStringHandlerArgument]` makes the holes run only on failure. Verified in the spike. A hole whose value throws while it is formatted shows `<ToString threw FormatException>`, naming the exception's type, and the `VerifyException` still surfaces (`CD-01`). A hole expression that throws, such as `{Load()}`, runs at the call site as C# evaluates it, so its own exception surfaces in every tier. |
| `.Because(string)`, `.WithMessage(string)` on a `Verify` call | Add text to the failure message. They work only in methods that tier 3 rewrites, because in tiers 1 and 2 `Verify` throws before the chained call runs. VG0001 reports them anywhere else. |
| `Check(bool, string? message = null, ...)` | Adds a failure to the current `VerifyScope`. With no scope it throws like `Verify`, so a failure is never lost. |
| `Throws<T>(Action)`, `ThrowsAsync<T>(Func<Task>)` | Return the exception. A different exception type fails with that exception's type and message. `ThrowsAsync` treats an exception the action throws before it returns a task like one the task throws. A cancelled task throws `TaskCanceledException`, which passes for `T` of `OperationCanceledException` and fails otherwise. |
| `Approx(double expected, double tolerance)` | A value whose `==` uses the tolerance. The explanation reports the difference. |
| `Eventually(Func<bool>, TimeSpan)` | Polls until true or timeout. On timeout it fails with the condition's source text. Neither tier captures values inside the lambda, so it reports no values. |
| `VerifyScope.Begin()` | Opens a scope for `Check` outside `VerificationGlue.Tests`, which opens one per test. Disposing the scope throws one `VerifyException` listing every failure. The scope keeps every failure from child tasks and threads that run in parallel in its async context. Their order is not part of the contract (`CD-07`). When the scope ends because code in it threw, that exception is the `InnerException` of the scope's exception (`CD-06`). The scope records the newest exception thrown in its async context, and `Dispose` uses it only when `Marshal.GetExceptionPointers` shows an exception is passing through. Native AOT does not support that call, so there the scope's exception has no `InnerException`. |

`Verify.Throws<T>` is not offered. A type named `Verify` cannot coexist with a `using static` method named `Verify` (CS0119, seen in the spike).

## Three tiers

The package gives the best message each call site allows. Tier 3 is opt-in until the debugging and performance gates below pass; tiers 1 and 2 do not require that opt-in.

| Tier | Applies to | Message | Mechanism |
|---|---|---|---|
| 1 | Every call, generator disabled or not running | Source text of the condition | `[CallerArgumentExpression]` |
| 2 | Every `Verify` and `Check` call in the project, except inside a method whose copy the runner always calls | Shape-specific text, for example "x can not be null", plus file and line embedded at compile time | One interceptor per call site |
| 3 | Eligible methods with `[Verify]` or a `[VerifyRewrite]` attribute, when rewriting is enabled and the copy runs | Captured values, with unavailable and skipped values identified | A rewritten copy of the method |

`[VerifyTest]` from `VerificationGlue.Tests` carries `[VerifyRewrite]`, so eligible test methods can get tier 3 when rewriting is enabled. Any other library can mark its own attribute the same way.

### Tier 2: interceptors

For each `Verify` or `Check` invocation, the generator emits a method with `[InterceptsLocation(1, data)]`, where `data` comes from `SemanticModel.GetInterceptableLocation`. The interceptor takes the same parameters, checks the boolean, and on failure throws a message built at compile time.

```csharp
[InterceptsLocation(1, "...")]
public static VerifyResult Verify_15_9(bool condition, string? message = null, string? expression = null)
{
    if (!condition)
    {
        throw VerifyFailure.Static(expression, "x can not be null", "OrderTests.cs", 15, message);
    }
}
```

The shape-specific text covers null checks, `==` and `!=`, relational operators, `is` type and property patterns, `!`, and calls returning `bool`. Anything else uses the source text.

A call inside a method gets no interceptor when tier 3 is enabled, `WillRewrite` is true for the method, and its attribute carries `[VerifyRewrite]`. The runner always calls that method's copy, so an interceptor on the original body would never run.

The package's `buildTransitive/VerificationGlue.props` adds `VerificationGlue.Generated` to `InterceptorsNamespaces`. Without it the build fails with CS9137. Props in `build/` do not reach a project that gets this package through `VerificationGlue.Tests`, so this file is in `buildTransitive/`.

### Tier 3: rewritten methods

The MSBuild property `VerificationGlueRewrite` set to `true` enables tier 3. When tier 3 is enabled, for each qualifying method in a `partial` type chain, the generator emits `{Name}__Verified` in the same type with the same signature. Its body is the original body with each `Verify` or `Check` statement replaced. Every other statement is copied as text, with the call changes below, and with `#line` directives that map it to the original file. Interceptors redirect direct calls of the original method to the copy. The test runner in `VerificationGlue.Tests` calls the copy directly.

Interceptors cannot reach a call inside generated code, because `InterceptsLocation` needs a location in a source file of the compilation the generators receive. The rewriter therefore handles calls in copied text itself. A direct call to a method for which `WillRewrite` is true becomes a call to its `{Name}__Verified` copy. A `Verify` or `Check` call that the rewriter cannot rewrite, such as one inside a lambda or a local function, is emitted inline in its tier 2 form. Each copy carries a generated marker attribute with the contract version, and a call into another assembly is redirected only to a copy that carries that marker.

Copied text must bind the way it did in its own file:

- A rewritten-method file repeats the using directives of the source file, including `using static` and aliases. This is an exception to the north star's rule that generated files carry no using directives.
- A method in a file that declares a `file` type is not rewritten, because the copy in another file cannot see that type. VG0008 reports it as information.
- Where a copied call omits a `[CallerMemberName]` argument, the rewriter passes the original method name, so the callee does not receive `{Name}__Verified`.

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

1. Once-only evaluation is a correctness requirement: preserve the original evaluation count, order and short-circuiting of every expression occurrence; explanations use original captured values or mark them unavailable, never evaluate them again. The spike checked this with a counter: `Next() + Next() == 10 || Next() == 0` called `Next()` three times.
2. Locals are typed, so the success path does not box or allocate. Formatting happens on the failure path only.
3. Constants and literals are not captured.
4. Preserve every semantically distinct read of a local or parameter across an intervening mutation. Captures may be shared only when no intervening operation can change the value, including through `ref`, `out` or a closure; deduplicating message labels must not reuse stale values.
5. A sub-expression on the skipped side of `&&`, `||`, `?:` or `??` reports `<not evaluated>`.
6. Each rewrite is an `if` statement, not a block, so `out var` declared in a `Verify` stays in scope for the following statements. Verified in the spike. When the `Verify` statement is an embedded statement, as in `if (ready) Verify(x);`, the rewrite is wrapped in braces, because the capture declarations and the `if` are several statements.
7. `await` inside the condition stays in place. Verified in the spike.
8. Property-pattern explanations use only values captured during the original evaluation, preserving its evaluation order and count. Never re-read a getter on the failure path. If a value cannot be captured while preserving pattern semantics and variable scope, retain the original pattern and report the value as unavailable; skipped members report `<not evaluated>`.
9. A sub-expression whose type cannot be stored in a local, such as a `ref struct` or a pointer, is not captured. The message shows its text, and analyzer VG0006 reports it as information.
10. A mutable struct receiver is not copied into a local, because a copy would take the mutation. It is reported by text only.
11. When the rewriter cannot name a sub-expression's type, as for an anonymous type or a member that another generator adds, the candidate is to declare its local as `var v2 = false ? <expr> : default;`. That declaration takes the expression's type without evaluating it. A sub-expression that contains such a member gets the same declaration, because Roslyn's error recovery can bind the call after it to an unrelated extension method, as `Enumerable.Count` for `calc.Calls().Count()`. A sub-expression that declares a variable, such as `out var`, cannot be repeated in a declaration, so it is captured as text only. Semantic tests pin this before the rewrite relies on it.

### Contract with `VerificationGlue.Tests`

Generators cannot see each other's output. The test generator emits a call to `{Name}__Verified` without seeing that method. Both generators therefore compile the same decision function from `src/Shared.Generator/VerifyRewriteRules.cs`:

```csharp
static bool WillRewrite(IMethodSymbol method)   // in a partial type chain, has [Verify] or a [VerifyRewrite] attribute, body has no syntax errors
```

The function does not check the body for semantic errors. Members that another generator adds, such as `calc.Calls()` from the mock generator, do not exist in the compilation the generators see, so such a check would turn tier 3 off for every test that uses them.

Both generators also use the same tier 3 enablement setting. When rewriting is disabled or the function returns false, the test generator calls the original method and the test gets tier 2 messages. A contract test in `test/Contract.Specs` builds a project with both generators and checks that every combination compiles. It includes `Verify(calc.Calls().Add(1, 2) == 1)` in a `[VerifyTest]`, with the mock generator also running.

`VerificationGlue.Tests` depends on the exact version of this package, and its generator reports an error when the referenced `VerificationGlue` has a different version, because two versions of `WillRewrite` can disagree.

### Explanations by shape

The generator knows the static types at each call site, so it picks an explanation without reflection.

| Shape | Extra lines in the message |
|---|---|
| `string == string` | First difference with index and the two characters |
| `a.SequenceEqual(b)` | First differing index and both elements, or the length difference |
| `items.Contains(y)` | `items` (capped) and `y` |
| `dict.ContainsKey(k)` | The key and up to 10 existing keys |
| `x == Approx(...)` | Difference and tolerance |
| `x is { ... }` | Original captured member values and match results, or explicit unavailable/not-evaluated markers |
| A call returning `VerifyOutcome` | The outcome's `Explanation` |

### Formatting values

- Strings are quoted and escaped. Long strings are cut at 200 characters with a marker.
- Collections that implement `ICollection` or `IReadOnlyCollection<T>` show up to 10 items. A lazy `IEnumerable` is not enumerated a second time. It shows its type name.
- Records and types that override `ToString` use it. Other types show their type name, or a member dump the generator emits when the type is in the compilation.
- Values are formatted on the failure path only, once per row, after the condition has run. A `ToString` that throws gives `<ToString threw FormatException>`, naming the exception's type, and the `VerifyException` still surfaces with the original condition (`CD-01`). A `ToString` that changes state runs only on that path, so the condition's captured values are the ones it ran with.
- A static method marked `[VerifyFormatter]` with signature `static string Format(T value)` replaces the default for `T`. The generator binds it by static type at each capture site, so there is no registry.

### Custom verifiers

Any method returning `bool` already shows its receiver and arguments. A method returning `VerifyOutcome` adds its own text:

```csharp
public static VerifyOutcome IsSorted<T>(IReadOnlyList<T> items) where T : IComparable<T> { ... }

Verify(IsSorted(prices));
```

`VerifyOutcome` converts implicitly to `bool`. An eligible helper marked `[Verify]` gets captured values from its own `Verify` calls when rewriting is enabled and its generated copy runs.

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
| VG0008 | Info | A method that is not rewritten because its file declares a `file` type |
| VG0009 | Info | `Verify(bool)` or `Check(bool)` in a test method where the lambda form loses nothing, with a code fix. `VerificationGlueLambdaSeverity` sets the severity. |

## Pipeline and caching

The generator has three independent pipelines. Neither model holds a Roslyn object. Collections are `EquatableArray<T>`. Types are CSharpAuthor `ITypeDefinition`. See the CSharpAuthor section of the north star for the rules.

| Pipeline | Discovered with | Model | Output |
|---|---|---|---|
| Call sites | `CreateSyntaxProvider` on invocations named `Verify` or `Check`, then a semantic check of the target | `VerifyCallModel`: file path, line, interceptable location data, condition shape (kind, operator, operand texts) | One file per source file holding all its interceptors |
| Methods | `ForAttributeWithMetadataName` on `[Verify]`, plus a second provider for attributes marked `[VerifyRewrite]` | `VerifyMethodModel`: containing type chain, signature, body statements (text, line, and for `Verify` statements a capture tree of `ITypeDefinition` and texts) | One file per rewritten method |
| Redirects | `CreateSyntaxProvider` on invocations whose name matches a rewritten method, then a semantic check of the target | The call's file path, interceptable location data, and the copy it calls | One file per source file holding all its redirects |

Why it is split this way:

- Interceptor location data changes on every edit above a call. Keeping it out of the method model means a rewritten method is regenerated only when its own text or line changes.
- One file per source file for interceptors keeps an edit to one file from regenerating the others.
- Line numbers are part of the method model, so an edit above a method regenerates that method. This is the price of `#line` mapping. The cold-build benchmark measures it.

CSharpAuthor use:

- `KnownTypes` holds `static readonly` definitions for `VerifyFailure`, `VerifyException`, `VerifyScope`, `VerifyOutcome` and `InterceptsLocationAttribute`.
- Shared `Ex` nodes for `VerifyFailure.Start`, `.Value`, `.Build` and `throw`.
- The capture tree in the model is turned into `Ex` nodes in the output step. Copied statements are `Raw` nodes preceded by `LineDirectiveComponent.At(line, file)`. The copy has `#pragma warning disable` around it so warnings are not reported twice.

## Performance

The phase 0 cold-build benchmark adds 53.9 s at 100,000 calls for tier 2 alone and 42.7 s for tier 3 with redirects. The CI job `cold-build` measures it on every push and pull request and uploads `report.md`.

The benchmark is `test/ColdBuild`. It writes 10,000 `[VerifyTest]` methods with 10 `Verify` calls and one `VerifyEquivalent` call each into three assemblies. Each test class also has one `[Verify]` helper that every test in it calls, which gives tier 3 a call to redirect. The helper has one `Verify` and one `VerifyEquivalent` call. That makes 112,004 calls, 11,002 of them `VerifyEquivalent`, and added seconds are scaled to 100,000. Each configuration is a cold rebuild of all three assemblies with build servers off, run three times in rotating order. The table gives the median of the summed build times.

Since Phase 2 the benchmark times the product generator, `src/VerificationGlue.Generator`, with `VerificationGlueRewrite` switching tier 3. The benchmark sources declare a `[VerifyRewrite]` attribute named `VerifyTest` in place of the one from `VerificationGlue.Tests`.

Phase 0 timed a stand-in generator, `test/ColdBuild.Generator`, which Phase 2 removed. Its first run, on macOS 27.0.1 Arm64 with 11 logical processors and .NET SDK 10.0.403, on 2026-11-17:

| Build | Median seconds | Added seconds at 100,000 calls | Ratio to generator off | Interceptors | Copies | Redirects |
|---|---:|---:|---:|---:|---:|---:|
| Generator off | 7.1 | 0 | 1.00 | 0 | 0 | 0 |
| Tier 2 alone | 61.5 | 53.9 | 8.67 | 101,002 | 0 | 0 |
| Tier 3 with redirects | 50.2 | 42.7 | 7.08 | 1,002 | 11,002 | 10,000 |

Tier 3 costs less than tier 2 alone. Under tier 3 the 100,000 calls in test bodies get no interceptor, because the runner calls the copies. One interceptor per call is the main cost of tier 2.

The `VerifyEquivalent` calls were added to the benchmark with `CheckEquivalent`. On this Mac, the fastest of five interleaved cold builds of one benchmark assembly took these times. Main had 33,674 `Verify` calls in the assembly, and the branch added 3,668 `VerifyEquivalent` calls to them:

| Build | Main | With `VerifyEquivalent` | Added |
|---|---:|---:|---:|
| Generator off | 2.4 s | 3.1 s | 0.7 s |
| Tier 2 alone | 21.5 s | 28.7 s | 7.2 s |
| Tier 3 with redirects | 16.8 s | 22.5 s | 5.7 s |

After the 0.7 s that the larger source costs without the generator, a `VerifyEquivalent` call adds about 1.8 ms to a cold build at tier 2 and 1.4 ms at tier 3. A `Verify` call adds about 0.57 ms at tier 2. The ratio to generator off grows by about 3% at tier 2 and 4% at tier 3.

The assertion spike measured about 0.7 ms per call with a generator that was not incremental, about 70 s at 100,000 calls. The benchmark replaces that figure.

The same stand-in on the 4-core ubuntu CI runner measured 104.3 s added at 100,000 calls with a ratio of 5.71 for tier 2 alone, and 80.7 s with a ratio of 4.65 for tier 3.

Budgets, with the numbers set by the phase 0 benchmark:

- Each cold-build budget, for tier 2 alone and for tier 3, is stated both as a ratio to the same build with the generators off, measured in the same CI job, and as total added seconds at 100,000 calls.
- An edit to one test file regenerates only that file's outputs.

The approved CI budget is 50% above the stand-in's CI figures: tier 2 alone at most 160 s added at 100,000 calls and a ratio of 8.5, and tier 3 at most 120 s and a ratio of 7.0. `test/ColdBuild/budgets.json` holds the limits, and the CI job `cold-build` fails when a configuration is over one. The budget applies to the CI job only and does not decide the tier 3 default.

On this Mac the product generator's generated code is the same size as the stand-in's for tier 2, 16.5 MB per assembly, and smaller for tier 3, 22.8 MB against 25.0 MB. Its single-assembly timings matched the stand-in's within the run-to-run noise of a shared machine.

The default-enablement gate requires a separately user-approved limit on total added cold-build seconds at 100,000 calls, measured against the same generator-off build in the same job. Report that total and the matched-baseline ratio for tier 2 alone and tier 3. Benchmark results inform the proposed budget, but neither a measured result nor missing approval counts as acceptance.

## Documentation

This package owns these pages. The rules for producing and checking them are in the north star.

- README and `docs/guide/getting-started-verify.md`, with a section and a sample per host framework.
- `docs/guide/messages.md`, generated from `samples/Messages`, one failing test per explanation shape and per tier.
- Guides: "How tiers work and how to get values", "Soft checks with `Check`", "Write a formatter", "Write a custom verifier with `VerifyOutcome`".
- Migration guides from xunit `Assert`, FluentAssertions and Shouldly. The xunit `Assert` guide ships with the first release. The other two ship with Phase 3, which delivers the explanations they compare against.
- `docs/diagnostics/VG0001.md` to `VG0009.md`.
- A generated-code page showing a tier 2 interceptor and a tier 3 rewritten method.

## Delivery

Phase 1 ships as a preview. Phases 1 and 2 together are the first public release. Phase 3 follows it. The release map for all three packages is in the north star.

### Phase 0: spikes that decide the approach

When done, we know whether tier 3 can be the default.

Needs: the assertion spike committed under `spikes/` and the benchmark harness, both from "Repository foundation" in the north star. The spikes run on CSharpAuthor 2.0.0.

The criteria in this list are fixed before the spikes run. A result does not change them.

- [ ] The debugging spike runs on a rewrite that covers a `[Verify]` helper called from a `[VerifyTest]` body, a body that depends on the file's `using` directives, and a line that uses the typed mock surface, `Verify(calc.Calls().Add(1, 2) == 1)`.
- [ ] An IDE that fails is named in `docs/research/ide-report.md` and on the IDE setup page, with the property that switches tier 3 as the documented way to change it there.
- [ ] Debugging spike on pinned .NET 10 SDK, MTP 3, IDE and IDE-plugin versions: breakpoints bind in the original source and hit the executing copy, and stepping stays in original source without exposing capture scaffolding, in every advertised IDE (Visual Studio, Rider and VS Code). Record Hot Reload separately; its limitations alone do not block the default.
- [ ] Record the combined results in `docs/research/ide-report.md`, alongside the Tests discovery, UID-filtered execution, navigation/debugging and warning-free AOT run on the same pinned stack. MTP 2.5.3 evidence is historical only; record supported limitations before release promises.
- [x] Cold-build benchmark: 10,000 tests with 10 calls each across three assemblies, measured for tier 2 alone and for tier 3 including the redirect pipeline, with an incremental generator built on CSharpAuthor.
- [x] Keep tier 3 opt-in unless every advertised IDE passes breakpoint binding and stepping and the separately user-approved total added cold-build budget passes at 100,000 calls; report the matched-baseline ratio too. Missing budget approval is not a pass. Record the evidence and resulting default explicitly.

Result: tier 3 stays opt-in. Two gates fail.

- No total added cold-build budget for the default has user approval. Phase 2 has an approved budget for the CI job, which does not decide the default. The measured cost is 42.7 s at 100,000 calls with a ratio of 7.08 for tier 3, and 53.9 s with a ratio of 8.67 for tier 2 alone. See [Performance](#performance).
- Rider passes breakpoint binding and stepping. Visual Studio and VS Code have no debugging result, as recorded in `docs/research/ide-report.md`.

Tier 3 can become the default only after the user approves a budget that the CI job `cold-build` meets, and Visual Studio and VS Code pass the debugging checks.

### Phase 1: messages from source text and shape

When done, a user on any test framework installs `VerificationGlue` and gets file, line and shape-specific messages, including under Native AOT.

Needs: "Repository foundation" in the north star. Nothing from the other packages.

- [x] `VerifySyntax` with `Verify`, `Throws`, `ThrowsAsync` and the interpolated message form.
- [x] Tier 2 interceptors for every listed shape.
- [x] `buildTransitive/VerificationGlue.props` sets `InterceptorsNamespaces`.
- [x] Analyzers VG0002, VG0003 and VG0004.
- [x] A Native AOT sample in CI with no warnings: `samples/VerifyAot`, published and run by `eng/pack-and-consume.sh`.
- [x] Sample tests in xunit, NUnit, MSTest and TUnit in CI: `samples/VerifyIn*`, run with the specs.
- [x] Pages: README, `docs/guide/getting-started-verify.md` with a section per host framework, `docs/diagnostics/VG0002.md` to `VG0004.md`, and the tier 2 part of the generated-code page, `docs/guide/generated/interceptors.md`.

### Phase 2: values

When done, a `Verify` in an eligible `[Verify]` method whose copy runs reports original captured values or explicit unavailable markers, preserving evaluation semantics. `Check` collects failures and reports them when its `VerifyScope` ends.

Needs: Phase 1 and the Phase 0 result. Nothing from the other packages. `tests.md` Phase 1 needs this phase for `WillRewrite` and the `__Verified` copies, and `tests.md` Phase 2 needs it for `Check` and `VerifyScope`.

- [x] Tier 3 rewrite for every rule listed above, with tests for each rule: `test/VerificationGlue.Generator.Specs/RewriteSpecs.cs`.
- [x] Before tier 3 ships, conformance cases prove side-effecting property getters are never re-read (including one that throws on a second read), skipped members remain unevaluated, and local/parameter reads separated by mutation retain their distinct values.
- [x] The shared `WillRewrite` function and the contract test, `test/Contract.Specs`. Until `mocks.md` Phase 1 and `tests.md` Phase 1 land, two stand-in generators in that project add `Calls()` and write the runner's calls.
- [x] Analyzers VG0001, VG0005, VG0006 and VG0008, and the code fix for VG0005 in `src/VerificationGlue.CodeFixes`.
- [x] The copy is skipped when the original body has syntax errors, so those errors are not reported twice.
- [x] Rewrite conformance cases for using aliases, extension methods, file-local types and `[CallerMemberName]`.
- [x] `Check`, `VerifyScope` and analyzer VG0007.
- [ ] The cold-build budget met in CI.
- [x] Pages: `docs/guide/messages.md` for tier 2 and tier 3, the guides "How tiers work and how to get values" (`tiers.md`) and "Soft checks with `Check`" (`check.md`), the migration guide from xunit `Assert`, `docs/diagnostics/VG0001.md` and `VG0005.md` to `VG0008.md`, the tier 3 part of the generated-code page (`generated/rewritten-methods.md`), and `llms.txt`.

### Phase 3: explanations

When done, strings, collections and patterns get specific explanations.

Needs: Phase 2.

- [ ] The explanation table above.
- [ ] Value formatting rules and `[VerifyFormatter]`.
- [ ] `VerifyOutcome`, `Approx` and `Eventually`.
- [ ] Pages: the entries in `docs/guide/messages.md` for each explanation shape, the guides "Write a formatter" and "Write a custom verifier with `VerifyOutcome`", and the migration guides from FluentAssertions and Shouldly.

## Risks and open questions

- Breakpoints and Hot Reload in the copy are untested. Phase 0 answers this.
- A copy of a virtual method would skip overrides when called through the redirect. Only non-virtual methods are redirected. Overrides get tier 2.
- Each rewritten body compiles twice. The budget above bounds the cost.
- Stack frames show `{Name}__Verified`. A runner can strip the suffix when it reports a stack trace. Other frameworks will show it.
- xunit and NUnit call test methods through reflection, so their test bodies get tier 2. Eligible `[Verify]` helpers directly called from those tests get tier 3 only when rewriting is enabled and the call redirects to the copy.
