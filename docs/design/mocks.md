# VerificationGlue.Mocks: source-generated mocks

Status: design. Parent: `docs/north-star.md`. Research: `docs/research/mocking-report.md`.

## What the user gets

- `var calc = Mock.Of<ICalculator>();` creates a mock in any test framework, or in no framework at all. It works under Native AOT and trimming with no warnings.
- Setup and verification read like NSubstitute: `calc.Add(1, 2).Returns(3)`, `Arg.Any<int>()`, `calc.Received().Add(1, 2)`.
- `calc.Add(0, Arg.Any<int>())` binds the matcher to the second argument. NSubstitute binds it to the first.
- `[Mock] ICalculator calc` as a test parameter works in `VerificationGlue.Tests` and in DependencyModules, and the system under test receives the same instance. This ships with `tests.md` Phase 2 and the DependencyModules adapter in `tests.md` Phase 4, not with the first mock release.
- A mock is regenerated when its interface changes, whether the interface lives in the same project, another project or a package.
- A type or member that cannot be mocked is a build error or warning with a location, never a run-time surprise.

The spike behind this design passed 28 of 28 checks in a Native AOT publish of 1.9 MB with no trim or AOT warnings.

## Package

`VerificationGlue.Mocks` is one package with no dependencies. It contains:

- `lib/net10.0`: the runtime. `MockAdapter`, `IMock`, `Mock`, `MockRegistry`, `MockFactory<T>`, `Arg`, `Raise`, the `Returns`, `Throws`, `When`, `Received` and `DidNotReceive` extensions, and `[Mock]`.
- `analyzers/dotnet/cs`: the generator and analyzers.
- `buildTransitive/VerificationGlue.Mocks.props`: adds `VerificationGlue.Mocks.Generated` to `InterceptorsNamespaces`. Without it, `Arg` interceptors fail with CS9137. Props in `build/` do not reach a project that gets this package through `VerificationGlue.Tests`, so this file is in `buildTransitive/`.

The runtime and generator ship together because generated code calls `MockAdapter` directly. Rocks, Mockolate, Imposter and TUnit.Mocks all ship a single package for the same reason.

Framework hooks for `[Mock]` parameters in xunit, NUnit, MSTest and TUnit are 7 to 12 lines each. They ship as documented snippets in the first release. They become packages only if users ask.

## Surface

```csharp
var calc = Mock.Of<ICalculator>();

calc.Add(1, 2).Returns(3);
calc.Add(Arg.Any<int>(), Arg.Is<int>(x => x > 10)).Returns(-1);
calc.Divide(Arg.Any<int>(), 0).Throws(new DivideByZeroException());
calc.Mode.Returns(Mode.Scientific);
calc.TryParse("7", out Arg.Any<int>()).Returns(x => { x[1] = 7; return true; });
calc.When(c => c.Reset()).Do(_ => resets++);
calc.ComputeAsync(5).Returns(25);                          // Task<int> wrapped for you
calc.Changed += Raise.Event<EventHandler<int>>(calc, 5);

calc.Received().Add(1, 2);
calc.Received(2).Reset();
calc.DidNotReceive().Divide(Arg.Any<int>(), Arg.Any<int>());
```

Unconfigured members return auto-values: `default`, a completed `Task` or `ValueTask`, an empty string for `string`, and an empty collection for array and common collection interfaces.

## How it works

### Discovery

The generator generates a mock for a type `T` when it finds any of these:

1. A `[Mock]` attribute on a parameter, property or field typed `T`.
2. A call to `Mock.Of<T>()` with `T` closed at the call site.
3. `[assembly: GenerateMock(typeof(T))]`, for helpers where `T` is still a type parameter at the call site. A call inside a generic helper cannot be resolved at compile time. Analyzer VGM0004 reports a `Mock.Of<T>()` whose `T` is never discovered anywhere.

The generator recognizes `[Mock]` by metadata name. That is how `VerificationGlue.Tests` gets mocks for `[Mock]` parameters without seeing this generator's output.

It also treats every attribute metadata name listed in the `VerificationGlueMockAttribute` MSBuild item like `[Mock]`. A host with its own mock attribute adds its name there. The DependencyModules support package adds `DependencyModules.Testing.Attributes.MockAttribute` from `buildTransitive`, and the NUnit snippet adds its own attribute. The spike needed the same list, hard-coded.

### The generated class

One sealed class per mocked type, holding its own adapter:

```csharp
internal sealed class Contracts_ICalculator_Mock : ICalculator, IMock
{
    private readonly MockAdapter _adapter = new("Contracts_ICalculator_Mock");
    MockAdapter IMock.Adapter => _adapter;

    int ICalculator.Add(int a, int b)
        => _adapter.Call<int>(0, "Add", null, new object?[] { a, b });

    bool ICalculator.TryParse(string text, out int value)
    {
        var args = new object?[] { text, default(int) };
        var result = _adapter.Call<bool>(5, "TryParse", null, args);
        value = (int)args[1]!;
        return result;
    }
}
```

- Each member passes a member ID, its name, the generic type arguments as `Type[]`, and the arguments.
- The adapter method is chosen from the return type: `Call<T>`, `CallVoid`, `CallTask`, `CallTask<T>`, `CallValueTask<T>`. Auto-values need no reflection as a result.
- `ref` and `out` values are written back from the argument array.
- Events use a typed delegate field and the generated accessors.
- Members are implemented explicitly, so name collisions between inherited interfaces do not matter.

### Registry

A module initializer registers every generated class:

```csharp
[ModuleInitializer]
internal static void Register()
{
    MockRegistry.Register<ICalculator>(static () => new Contracts_ICalculator_Mock());
}
```

`Mock.Of<T>()` reads `MockFactory<T>.Create`, a static generic field. `MockRegistry.Create(Type)` serves hooks that only have a `Type`, such as framework data attributes and DependencyModules. Neither uses reflection. Both were verified under Native AOT.

### The adapter and the last call

Each mock owns one `MockAdapter` instance. Recorded calls, responses and callbacks belong to that instance. Two mocks of one interface share nothing.

Setup syntax needs to link `.Returns(3)` back to the call before it. A `MockContext` does that. It is an immutable value held in an `AsyncLocal` and replaced on every change, so it flows across `await` and a child task's changes do not reach its parent. The next operation that uses each part consumes it:

1. The generated member calls its own `_adapter`.
2. The adapter records the call and stores `(adapter, call record, call spec)` as the last call in `MockContext`.
3. `Returns<T>(this T, T value)` consumes the last call, removes the setup call from the recorded calls, and adds the response to that adapter.
4. `Received()` stores a pending check for that adapter in `MockContext`. The next call on that adapter in the same flow consumes it and is checked against the recorded calls instead of being recorded. A call from another flow, such as one the system under test makes in the background, is recorded as usual.
5. `Arg.Any<T>()` and `Arg.Is<T>(...)` add a matcher to the queue in `MockContext`. The next mock call consumes the queue.

The spike used a thread-static context and ran 200 setups in parallel on one mock, and every one bound to the right call. None of those setups awaited. In a model of `calc.Add(1, 2).Returns(await LoadAsync())` run 200 times in parallel, the thread-static context bound 199 setups to the wrong call or none, because the continuation resumed on another thread. The async-local context bound all 200. Behaviour cases cover `Returns(await ...)`, a background caller during `Received()`, a child task that inherits the context, and an abandoned matcher.

### Matcher positions

NSubstitute gives a queued matcher to the first argument that holds a default value. For `calc.Add(0, Arg.Any<int>())` both arguments are `0`, so the matcher lands on the wrong one. The generator fixes this with one interceptor per `Arg` call site that passes the parameter ordinal read from the syntax. A matcher stored in a local has no syntax position. Analyzer VGM0002 reports it, and at run time it falls back to the NSubstitute rule.

### Typed setup surface

The generator also emits a typed setup and call-count surface for each mocked type. It uses no `MockContext` and no interceptors, as in Rocks, Mockolate and Imposter. Both styles drive the same `MockAdapter`, so a test can mix them.

```csharp
var calc = Mock.Of<ICalculator>();

calc.Setup().Add(1, Arg<int>.Any()).Returns(3);
calc.Setup().Divide(Arg<int>.Any(), 0).Throws(new DivideByZeroException());
calc.Setup().Mode.Returns(Mode.Scientific);

Verify(calc.Calls().Add(1, 2) == 1);
Verify(calc.Calls().Reset() == 0);
```

How it works:

- `Setup()` and `Calls()` are generated extension methods on the mocked type, for example `ICalculator`, in the `VerificationGlue.Mocks` namespace. They return classes named after the mock class, such as `Contracts_ICalculator_Setup` and `Contracts_ICalculator_Calls`, generated in the mock's file.
- Each member on those types takes `Arg<T>` for each parameter. `Arg<T>` is a struct with an implicit conversion from `T`, so `1` becomes an exact match and `Arg<int>.Any()` becomes a wildcard. Each matcher arrives as its own argument, so its position is known with no interceptor. C# applies no user-defined conversion from an interface type, so an exact match on an interface-typed parameter is written `Arg<IClock>.Is(clock)`.
- The surface has a member for each method, and for each property and indexer with a getter. It leaves out events, members that throw (VGM0003), and a member whose C# signature an earlier one already has, such as `M(int)` after `M(out int)`.
- `Setup()` members return a typed builder, `MockSetup<int>` for `Add`, with `Returns`, `Throws` and `Does`. A `void` member returns `MockSetup`, with `Throws` and `Does`. The value type is checked at compile time, and `Returns(25)` on a `Task<int>` member wraps the value.
- `Calls()` members return `CallCount`, the number of matching recorded calls, which compares with `int` through `==`, `!=`, `<`, `<=`, `>` and `>=`, so verification is a plain `Verify`. Its `ToString` lists up to 10 recorded calls to that member, such as `0 (Add received 2 calls: Add(3, 4), Add(5, 6))`, so a failed tier 3 `Verify` shows what the mock did receive. The failure text is checked through `samples/Messages` before the type is frozen in `PublicAPI.Shipped.txt`.
- The typed matchers are static members of the `Arg<T>` struct: `Arg<int>.Any()`, `Arg<int>.Is(x => x > 10)` and `Arg<int>.Is(5)`. They return `Arg<T>` and queue nothing. `Arg.Any<T>()` cannot serve both styles, because C# does not overload on return type, and two `Any<T>()` methods that differ only in return type fail with CS0111.
- An NSubstitute-style `Arg.Any<int>()` passed to a `Setup()` or `Calls()` parameter still compiles through the implicit conversion. It becomes an exact match on `0` and leaves a matcher queued, so VGM0002 reports it as an error. The diagnostics layer has a case through `Setup()` and one through `Calls()`.

The typed surface is the recommended style for new code and for libraries. The NSubstitute style stays for teams moving from NSubstitute.

### Recursive mocks

An unconfigured member whose return type can be mocked returns a mock, created on the first call and returned again for the same arguments. `calc.History.Last.Returns(5)` then works without setting up `History`.

- The generator generates mocks for those return types too, following interface-typed returns of every mocked type. Class-typed returns join in phase 3 with class mocks.
- The walk stops at a depth set by `VerificationGlueMocksRecursionDepth`, default 2, and never enters `System` namespaces. A member whose return type is beyond the depth returns `default` and reports VGM0007 as information.
- The shape sweep measures how much code the walk adds across the BCL and popular packages, and sets the default depth from that.

### Class mocks

A class mock calls the base member for every virtual member that has not been set up. A setup replaces the base call for matching arguments. `Received()` and `Calls()` count calls whichever path ran. An abstract member has no base and returns an auto-value. `Mock.Of<T>(MockOptions.NoBase)` turns the base calls off for one mock.

### Supported shapes

| Shape | First release |
|---|---|
| Interfaces, including generic and closed generic interfaces | Yes |
| Generic methods | Yes. Type arguments travel as `typeof(T)`. |
| `ref`, `out` and `in` parameters | Yes |
| `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` | Yes, for setup and auto-values |
| Properties, indexers and events | Yes |
| Default interface members | Yes. The default body does not run. |
| Recursive mocks | Yes, to the configured depth |
| Classes with public virtual members | Later, calling the base by default. The spike showed class mocks work with a parameterless constructor. |
| Static abstract members, sealed types, structs | No. Error VGM0001. |
| `ref struct` parameters | No in the first release. Warning VGM0003 and the member throws. See the open question. |
| Pointer members | No. Warning VGM0003 and the member throws. A project without `AllowUnsafeBlocks` gets error VGM0001 for the type. |
| `ReturnsForAnyArgs`, `Received.InOrder`, strict mode, delegates | Later |

## Use from other frameworks

| Framework | `Mock.Of<T>()` | `[Mock]` parameter |
|---|---|---|
| `VerificationGlue.Tests` | Works | Built in |
| DependencyModules | Works | One `IMockSupportAttribute` whose `ProvideMock(Type)` returns `MockRegistry.Create(type)`, and its `[Mock]` name in the `VerificationGlueMockAttribute` item |
| xunit v3 | Works | A 12-line `DataAttribute` |
| NUnit | Works | A 9-line `IParameterDataSource` on the parameter attribute, and that attribute's name in the `VerificationGlueMockAttribute` item |
| MSTest | Works | A 12-line `ITestDataSource` |
| TUnit | Works | A 7-line `UntypedDataSourceGeneratorAttribute` |

All eight combinations passed in the spike on the JIT. Native AOT inside xunit, NUnit, MSTest and TUnit was not tested. Their hooks read `ParameterInfo` through reflection, so `Mock.Of<T>()` is the recommended form in those frameworks.

## Interface changes

A mock must be regenerated when its type changes, and only then. The spike verified every row of this table with generator driver step reasons and real `dotnet build` runs.

| Change | Mock regenerated | `Arg` interceptors regenerated |
|---|---|---|
| Edit an unrelated file | No | No |
| Edit the test file above an `Arg` call | No | Yes |
| Add or change a member of the interface, same project | Yes | No |
| Referenced project adds a member | Yes | No |
| Referenced project changes a method body only | No. The test project is not even recompiled. | No |
| Referenced DLL replaced with a new member | Yes | No |

Rules that make this hold:

- Roslyn reruns the semantic transform when the compilation changes, including metadata references. The transform rebuilds `MockModel` from the symbol. The source output runs only when the model's value changed.
- `MockModel` holds everything the emitter reads, as strings, enums, CSharpAuthor `ITypeDefinition` values and `EquatableArray<T>`. It holds no `ISymbol`, `SyntaxNode`, `Location` or `Compilation`.
- A model that compares too little produces a stale mock and CS0535, only in a long-lived IDE process. The spike reproduced this with a name-only comparison. The generator driver harness stays in the test suite as a regression test.
- Interceptor locations live in a separate pipeline and output. They change on every edit to a calling file, and keeping them out of `MockModel` leaves the mock cached.

## Code generation

- One file per mocked type, and one file per source file for `Arg` interceptors.
- `KnownTypes` holds `static readonly` definitions for `MockAdapter`, `IMock`, `MockRegistry`, `MockFactory<T>`, `Task<T>`, `ValueTask<T>` and the attributes.
- Shared `Ex` nodes for `_adapter`, the `Call*` method names and the argument array.
- The member list of a model is an `EquatableArray<MockMemberModel>`. Each member model carries its ID, name, return kind, `ITypeDefinition` parameter types with ref kinds, and type parameters as `TypeParameterDefinition` with constraints.
- Diagnostics travel in the model as an `EquatableArray<DiagnosticModel>` with an ID and arguments, and no location. `MockAnalyzer` reports them at each place that names the type: the `Mock.Of<T>()` call, the `[Mock]` attribute or the `[assembly: GenerateMock]` attribute. A location in `MockModel` would change on every edit above a `Mock.Of` call and regenerate the mock, against the table above. The spike reported them without a location, which this fixes.

## Performance

Spike figures, single runs on one machine:

| Input | Generator time | Generated source | Added IL |
|---|---|---|---|
| 1 interface, 20 members | 2 ms | 183 lines | 4 KB |
| 1 interface, 500 members | 40 ms | 3,543 lines | 76 KB |
| 500 interfaces, 20 members | 797 ms | 77,528 lines | 1,689 KB |

Compiling the generated code costs more than generating it. After an unrelated edit the 500-interface case still took 278 ms, because every transform reruns and rebuilds its model. The phase 1 benchmark sets a budget for both.

Phase 1 figures, the median of 5 runs on an Apple silicon Mac, for 500 interfaces of 20 members with a `Mock.Of` call and an `Arg` matcher for each:

| Scenario | Median | Budget |
|---|---|---|
| Cold generator run | 380 to 672 ms | 2,500 ms |
| Generator run after an unrelated edit | 210 to 250 ms | 1,000 ms |
| Compile of the input and the generated code | 941 to 1,238 ms | 4,500 ms |

The budgets are about four times the local figures, because CI runners are slower. On CI the compile median before Preview B was 2,629 ms.

The typed surface of Preview B adds about a third to the compile time. In alternating runs on the same Mac, the compile median was 1,170 to 1,445 ms without it and 1,715 to 1,764 ms with it. The generator run times did not change beyond the noise. The `Check generator budgets` step in CI runs `test/Generator.Benchmarks` with `budget test/Generator.Benchmarks/mock-budgets.json` and fails when a median is over its budget. `MockGeneratorBenchmarks` gives the same scenarios through BenchmarkDotNet.

Every mock call allocates an argument array and boxes value types. That is acceptable for tests. A typed fast path for calls with no matchers can come later if the benchmark shows a need.

## Conformance suite

The suite records the problems other mocking libraries hit over the years, so this library does not repeat them. Every case names the library and issue it came from. Every case runs on the JIT and under Native AOT.

### Sources

1. **Issue trackers.** Closed bugs in NSubstitute, Moq, FakeItEasy, Castle.Core (DynamicProxy), Rocks, Mockolate, Imposter and TUnit.Mocks. Each bug becomes one case with a link.
2. **Existing test suites.** Rocks (about 1,400 tests), Mockolate (about 3,800) and Imposter (about 2,200) are MIT licensed. They are used as a checklist. Scenarios are ported, code is not copied.
3. **Real assemblies.** A sweep generates a mock for every interface in the BCL and in the most-downloaded NuGet packages and checks that the output compiles. Rocks does the same in `Rocks.CodeGenerationTest`.

### Layers

| Layer | Question | Cases come from |
|---|---|---|
| Shape | Does the generated mock compile for this type? | The automated sweep, plus hand-written files for known hard shapes |
| Behaviour | Do setup, matching and verification act correctly? | Ported scenarios and mined issues |
| Diagnostics | Does misuse fail at build time with the right ID and location? | One source snippet and expected diagnostic per case |
| Pipeline | Is generation incremental and deterministic? | The generator driver harness, with step reasons per change and stable output order |

Hand-written shape files cover explicit interface collisions and diamond inheritance; generic constraints including `default`, `notnull` and `allows ref struct`; `in`, `ref readonly`, `params` and optional parameters; keyword identifiers, `init` accessors and member hiding with `new`; nullable annotations and tuple names; internal types, nested generics and extern aliases.

Behaviour groups follow the failure classes the trackers show: argument matching (default values, `null`, `params`, collections); last-call hazards; setup precedence and sequences; async auto-values and faulted tasks; threading and parallel tests; events and properties; recursive mocks; failure message text.

### Expectations, not skips

Each case states one outcome: it passes, it reports a named diagnostic, or it is documented as unsupported. A static abstract interface is a passing case that expects VGM0001.

### Comparison against NSubstitute

The setup syntax mirrors NSubstitute, so one behaviour file can compile against either library with a `using` switch. NSubstitute on the JIT gives the expected answer. Every difference is either a bug or a recorded decision, such as the matcher position fix. This is untested. No file has been compiled against both yet. A bounded comparison is a first-release gate: it covers argument matching, setup precedence, response sequences, async results, last-call hazards and parallel use, including `Returns(await ...)` and a background caller during `Received()`. Each difference must be fixed or recorded as an intentional decision; a known NSubstitute defect is not the required behavior. The run generates the "Known differences from NSubstitute" page before the first release. Broad issue mining and exhaustive compatibility work remain in phase 2.

### Case format

Each case file starts with a header:

```csharp
// case: mocks-behaviour-0142
// layer: behaviour
// origin: NSubstitute issue <number> (<issue URL>)
// expect: pass
```

## Analyzers

| ID | Severity | Reports |
|---|---|---|
| VGM0001 | Error | A type that cannot be mocked: sealed, struct, with static abstract members, with an abstract member the project cannot see, or with pointer members in a project without `AllowUnsafeBlocks` |
| VGM0002 | Warning or Error | As a warning, an `Arg` matcher outside a mock call, or stored in a local. As an error, an NSubstitute-style `Arg` call passed to a `Setup()` or `Calls()` parameter |
| VGM0003 | Warning | A member that cannot be mocked, such as one with a `ref struct` or pointer parameter |
| VGM0004 | Warning | `Mock.Of<T>()` for a `T` that is not discovered anywhere |
| VGM0005 | Error | `Returns` or `Throws` on an expression that is not a mock call, such as `"abc".Length.Returns(3)` |
| VGM0006 | Error | Setup on a non-virtual member of a class mock |
| VGM0007 | Info | A return type beyond the recursion depth, so that member returns `default` |

VGM0005 catches the one hazard NSubstitute's design cannot catch at run time. After a mock call, `"abc".Length.Returns(3)` rebinds the earlier call without an error. The spike reproduced it.

## Documentation

This package owns these pages. The rules for producing and checking them are in the north star.

- README and `docs/guide/getting-started-mocks.md`, showing both the NSubstitute style and the typed style, with a section per host framework.
- One tested snippet per host framework for `[Mock]` parameters: xunit v3, NUnit, MSTest and TUnit.
- Guides: "Choose a setup style", "Mock async members", "Arguments and matchers", "Recursive mocks", "Mock a class", "Mock a type from a generic helper".
- Migration guides from NSubstitute and Moq, as paired samples that both compile. The NSubstitute guide ships with the first release. The Moq guide ships with Phase 3, which delivers class mocks and `Received.InOrder`, and it lists the Moq features that are still unsupported.
- A "Known differences from NSubstitute" page generated from the comparison run, so each recorded difference has its case.
- `docs/diagnostics/VGM0001.md` to `VGM0007.md`.
- A generated-code page showing a mock class, its registry entry and its typed setup surface.

## Delivery

Phase 1 ships as three previews and ends in the first public release, as 0.x. Phases 2 and 3 follow it. The release map for all three packages is in the north star.

### Phase 0: shape sweep

When done, we know which shapes the generator gets wrong before any user does.

Needs: the mock spike committed under `spikes/` and the generator driver harness, both from "Repository foundation" in the north star. The sweep runs on CSharpAuthor 2.0.0. Nothing from the other packages.

- [x] Class emitter for interfaces, built on CSharpAuthor, starting from the spike's 887-line generator. The sweep runs it, so it is built here and not in Phase 1.
- [x] Sweep harness that generates and compiles a mock for every public interface in the BCL and the top 100 NuGet packages.
- [x] Hand-written shape files from the list above.
- [x] Results triaged into fixed, diagnostic, or documented as unsupported.

The results and the triage are in [mocks-shape-sweep.md](mocks-shape-sweep.md).

### Phase 1: first release

When done, a user in any framework writes `Mock.Of<T>()` with either the NSubstitute-style setup or the typed `Setup()` and `Calls()` surface, gets recursive mocks for unconfigured members, and it runs under Native AOT.

Needs: Phase 0. Nothing from the other packages for the release. Values for `Verify(calc.Calls()...)` in a `[VerifyTest]` need the tier 3 rewrite from `verification-glue.md` Phase 2. `tests.md` Phase 2 and Phase 4 need this phase for `[Mock]` parameters.

The phase is built in this order, and each preview ships when its items are done:

1. Preview A: NSubstitute-style setup.
   - [x] Runtime and generator for interfaces, on the Phase 0 emitter.
   - [x] Registry, `Mock.Of<T>()`, `MockRegistry.Create(Type)` and `[assembly: GenerateMock]`.
   - [x] `Returns`, `Throws`, `When`/`Do`, `Received`, `DidNotReceive`, `Arg.Any`, `Arg.Is`, `Raise.Event`.
   - [x] The async-local `MockContext`, with its behaviour cases.
   - [x] `Arg` interceptors with ordinals.
   - [x] Analyzers VGM0001 to VGM0005, each with locations.
   - [x] Pipeline layer of the conformance suite, including the stale-model regression test.
   - [x] Generator time and compile time budgets set from a benchmark and enforced in CI.

   The behaviour cases are in `test/VerificationGlue.Mocks.Specs/Behaviour`, the diagnostics and pipeline layers in `test/VerificationGlue.Mocks.Generator.Specs`, and `samples/MocksAot` runs each feature under Native AOT in `eng/pack-and-consume.sh`. Not yet done: the `VerificationGlueMockAttribute` MSBuild item for other `[Mock]` attributes, and the auto-values for `string` and collections, which still return `default`.
2. Preview B: typed surface.
   - [x] The typed `Setup()` and `Calls()` surface, with `Arg<T>.Any()`, `Arg<T>.Is(...)` and the call-count type.

   The behaviour cases are in `test/VerificationGlue.Mocks.Specs/Behaviour/TypedSurface.cs`, including a tier 3 `Verify` failure, and the VGM0002 error cases in `MockAnalyzerSpecs`. Not yet done: the `CallCount` case in `samples/Messages`, which is needed before `CallCount` is frozen.
3. Preview C: recursive mocks.
   - [ ] Recursive mocks with `VerificationGlueMocksRecursionDepth`, its default set from the Phase 0 sweep, and analyzer VGM0007.
4. First release.
   - [ ] Framework hook snippets in the README, each tested in CI.
   - [ ] Bounded NSubstitute behavior comparison covering matching, precedence, sequences, async results, last-call hazards and parallel use; all differences fixed or recorded, and the generated "Known differences from NSubstitute" page checked in CI before the first release.
   - [ ] Pages: README, `docs/guide/getting-started-mocks.md` with a section per host framework, the guides "Choose a setup style", "Mock async members", "Arguments and matchers", "Recursive mocks" and "Mock a type from a generic helper", the migration guide from NSubstitute, the "Known differences from NSubstitute" page, `docs/diagnostics/VGM0001.md` to `VGM0005.md` and `VGM0007.md`, and the generated-code page.

### Phase 2: comparison and mining

When done, every behaviour NSubstitute users rely on is either matched or documented as a deliberate difference.

Needs: Phase 1. Nothing from the other packages.

- [ ] Expand the first-release NSubstitute comparison toward exhaustive compatibility, keeping the generated known-differences page current.
- [ ] Issue mining, one pass per library, queued as separate pieces of work.
- [ ] Ported scenarios from Rocks, Mockolate and Imposter.
- [ ] Pages: the "Known differences from NSubstitute" page regenerated from the expanded comparison.

### Phase 3: wider shapes

When done, users can mock classes and use the remaining NSubstitute features.

Needs: Phase 1, and the issue mining from Phase 2 for the `ref struct` decision. Nothing from the other packages.

- [ ] Classes with virtual members, calling the base by default, and VGM0006.
- [ ] `ReturnsForAnyArgs` and `Received.InOrder`.
- [ ] The `ref struct` decision below.
- [ ] Pages: the guide "Mock a class", the migration guide from Moq, `docs/diagnostics/VGM0006.md`, and the class mock part of the generated-code page.

## Risks and open questions

- An unused matcher stays queued until the next mock call in the same async flow. VGM0002 catches most cases. Because `MockContext` is async-local, a leftover matcher stays in the flow of the test that left it.
- `GetInterceptableLocation` needs a newer Roslyn than the 4.10 DependencyModules references. The generator targets the Roslyn that ships with the .NET 10 SDK.
- The `Arg` interceptor file is regenerated on each edit to a test file that uses `Arg`. The IDE cost on large files is unknown.
- GitHub code search was denied to the research session, so mining may need a credential or plain web reads.

- Recursive generation can multiply the generated code for types with deep interface graphs. The depth limit and the shape sweep bound it.

Open question: should `ref struct` parameters get a typed path?

A `ref struct` such as `Span<T>` or `ReadOnlySpan<char>` cannot be boxed into the argument array, so the uniform `MockAdapter.Call` path cannot carry it. A typed path would pass such arguments to a generated per-member matcher instead, as Rocks and Mockolate do. It costs a second code path through the adapter and the generator.

Recommendation: leave it out of the first release, which warns with VGM0003. The issue mining shows how often users of other libraries asked for it. Decide in phase 3 from that number and the sweep's.

The shape sweep found 8 such members in 7 of 1,452 interfaces in the BCL and the top 100 packages. They include `ISpanFormattable.TryFormat` and `IBufferWriter<T>.GetSpan`.
