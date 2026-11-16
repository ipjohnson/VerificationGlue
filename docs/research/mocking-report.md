# AOT-friendly mocks from source generation

Research for VerificationGlue. Date: 2026-11-16. SDK: .NET 10.0.403, Roslyn 4.14.

## Result

The idea works. A spike generates a mock class per interface, gives each mock its own `MockAdapter` instance, and supports `calc.Add(1, 2).Returns(3)`, `Arg.Any<T>()`, `Received()`, `Throws`, `When(...).Do(...)`, properties, indexers and events. It publishes with Native AOT with no trim or AOT warnings. The 1.9 MB binary passes 28 of 28 checks.

Three findings change the plan:

1. Creating a mock does not need C# interceptors. A generated module initializer that fills `MockFactory<T>` needs no reflection and also serves `[Mock]` parameters.
2. Interceptors are worth using for `Arg` matchers instead. NSubstitute's run-time rule binds `calc.Add(0, Arg.Any<int>())` to the wrong argument. An interceptor that carries the argument position fixes it.
3. `[Mock]` on a parameter does need a hook per framework, as the author suspected. Each hook is 7 to 12 lines. A plain `Mock.Of<T>()` call needs none.

Each claim below is marked **(verified)** when a spike ran it, or **(inference)** when it comes from reading code or documentation.

## The spike

The spike has a runtime library (`Glue.Mocks`, 633 lines), an incremental generator (`Glue.Mocks.Generator`, 887 lines), a demo published with Native AOT, a generator driver harness, and one test project each for xunit v3 3.2.2, NUnit 4.6.1, MSTest 4.5.3 and TUnit 1.85.0. The names are placeholders. The spike is committed under `spikes/mocks`.

A generated member looks like this:

```csharp
internal sealed class Contracts_ICalculator_Mock : ICalculator, IMock
{
    private readonly MockAdapter _adapter = new("Contracts_ICalculator_Mock");
    MockAdapter IMock.Adapter => _adapter;

    bool ICalculator.TryParse(string text, out int value)
    {
        var __args = new object?[] { text, default(int) };
        var __result = _adapter.Call<bool>(5, "TryParse", null, __args);
        value = (int)__args[1]!;
        return __result;
    }
}
```

## 1. Packaging

Ship one package that people install, plus thin optional integrations.

`Glue.Mocks` holds the runtime (`MockAdapter`, `IMock`, `Arg`, the `Returns` and `Received` extensions, `Mock`, `MockRegistry`, `[Mock]`). It also holds the generator and analyzers under `analyzers/dotnet/cs`, and a `build/*.props` file that appends `Glue.Mocks.Generated` to `InterceptorsNamespaces`. It depends on nothing. Without that property the build fails with CS9137 **(verified)**.

Do not publish the runtime and the generator as two packages. Generated code calls `MockAdapter` directly, so the two must always match in version. Rocks, Mockolate and Imposter each ship a single package with the generator inside **(verified by reading their project files)**.

The integrations each depend on `Glue.Mocks` and one other library:

- `Glue.Mocks.Xunit3`, `Glue.Mocks.NUnit`, `Glue.Mocks.MSTest`, `Glue.Mocks.TUnit`: one attribute each.
- DependencyModules: one `IMockSupportAttribute` implementation whose `ProvideMock(Type)` returns `MockRegistry.Create(type)`. The existing `[Mock]` handling in `DependencyModules.Testing` then works unchanged **(inference from reading `MockAttribute.cs` and `IMockSupportAttribute.cs`)**.
- VerificationGlue needs no package. Its generator emits `Mock.Of<IFoo>()` for a `[Mock] IFoo foo` parameter and registers the instance in the container. The mock generator finds the type from the same `[Mock]` parameter. One generator cannot see another's output, so VerificationGlue must call `Mock.Of<T>()` and not name the generated class **(inference; both halves are verified separately)**.

## 2. Use from other frameworks

A creation call works everywhere with no integration. `[Mock]` parameters need one small type per framework. All eight tests below pass **(verified)**.

| Framework | Hook for `[Mock]` parameters | Size | Test is written as |
| --- | --- | --- | --- |
| xunit v3 | `DataAttribute.GetData(MethodInfo, DisposalTracker)` | 12 lines | `[Theory, MockData]` |
| NUnit | `IParameterDataSource` on the parameter attribute | 9 lines | `[Test]`, with an NUnit-specific `[Mock]` |
| MSTest | `ITestDataSource` | 12 lines | `[TestMethod, MockData]` |
| TUnit | `UntypedDataSourceGeneratorAttribute` | 7 lines | `[Test, MockData]` |

Every hook only has a `System.Type`, so it calls `MockRegistry.Create(Type)`. The generator fills that registry from a module initializer:

```csharp
[ModuleInitializer]
internal static void Register()
{
    MockRegistry.Register<ICalculator>(static () => new Contracts_ICalculator_Mock());
}
```

The options compare as follows.

- **Creation through the registry, `Mock.Of<T>()`.** It reads `MockFactory<T>.Create`, a static generic field. It works under Native AOT, for a `Type`, and for an open type argument **(verified)**.
- **Creation through an interceptor, `Substitute.For<T>()`.** The generator replaces each call site with `new ..._Mock()` **(verified)**. It saves one static field read. It needs `InterceptorsNamespaces`. A call inside a generic helper, where `T` is still open, is not intercepted and the build reports nothing **(verified)**.
- **Per-framework parameter injection.** It works, with costs. Three of four frameworks need a second attribute on the method, and the test is reported as data-driven. NUnit creates the mock at discovery **(inference)**. The hooks reflect over `ParameterInfo`, and Native AOT in those frameworks was not tested.
- **Container integration.** This is the right form for VerificationGlue and DependencyModules, because the system under test must receive the same instance.

Recommendation: use the registry for creation and drop the creation interceptor. Document `Mock.Of<T>()` as the form for other frameworks. Ship parameter injection for VerificationGlue and DependencyModules first. Add the four framework packages only on demand.

## 3. Detecting interface changes

The mock regenerates in every placement tested **(verified)**. The generator driver reports this per step:

| Change | Mock model | Mock source | Interceptor source |
| --- | --- | --- | --- |
| Edit an unrelated file | Cached | Cached | Cached |
| Edit the test file above the call | Cached | Cached | Modified |
| Add a member to the interface (other file) | Modified | Modified | Cached |
| Change a parameter type | Modified | Modified | Cached |
| Add a comment to the interface file | Cached | Cached | Cached |
| Referenced assembly rebuilt, same API | Cached | Cached | Cached |
| Referenced assembly adds a member | Modified | Modified | Cached |

How it works:

- Roslyn reruns the semantic transform for every discovered node when the compilation changes. That covers another file and a changed metadata reference, so the generator keeps no dependency list **(inference, consistent with the table)**.
- The transform rebuilds a `MockModel` from the symbols. The source output runs only when the model's value differs.
- The model must hold everything the emitter reads, as strings and enums. It must hold no `ISymbol`, `SyntaxNode`, `Location` or `Compilation`.
- Interceptor locations must stay out of the mock model. They change on every edit to the calling file. The spike keeps them in a separate output, which is why the second row leaves the mock cached.

Builds from the command line **(verified)**:

- Project reference, member added: both projects compile, and the mock has the new member.
- Project reference, method body changed only: the test project's `CoreCompile` is skipped, because the reference assembly did not change. That is correct.
- DLL reference through `HintPath`, DLL replaced: the test project recompiles, and the mock has the new member.
- A NuGet upgrade uses the same mechanism as the DLL case **(inference)**.
- Removing a member produces CS1061 only at the test lines that use it.

A stale mock comes from an equality bug, and only in an IDE. The spike has a switch that compares models by type name only. After a member is added, the driver reports the model as `Unchanged`, keeps the old source, and the compilation fails with CS0535 in the generated file. A fresh driver, which is what a command-line build uses, generates it correctly **(verified)**. Keep the driver harness as a regression test for the generator. IDE behaviour was not tested.

## 4. NSubstitute-style syntax without reflection

Each mock owns one `MockAdapter` instance. Recorded calls, responses and callbacks are per mock. The link from `Returns` to the right adapter is a thread-static "last call":

1. The generated member boxes its arguments into `object?[]` and calls its own `_adapter`.
2. The adapter records the call and stores `(adapter, call record, call spec)` in the thread-static `MockContext`.
3. `Returns<T>(this T, T value)` takes that entry, removes the recorded setup call, and adds the response to that adapter.
4. `Received()` flags the adapter. The next call on it is counted against recorded calls and is not recorded.
5. `Arg.Any<T>()` and `Arg.Is<T>(...)` queue a matcher on the thread. The next mock call consumes the queue.

Two mocks of one interface keep separate state, and 200 parallel setups on one mock bind correctly **(verified)**.

The generator picks the adapter method from the return type: `Call<T>`, `CallTask<T>`, `CallValueTask<T>`, `CallTask`, `CallVoid`. Auto-values such as a completed `Task<T>` therefore need no reflection **(verified)**.

Matcher positions are the weak point of the queue. NSubstitute gives a queued matcher to the first argument that holds a default value. For `calc.Add(0, Arg.Any<int>())` both arguments are `0`, and the matcher lands on the first one. The spike reproduces this **(verified)**. The fix is an interceptor per `Arg` call site that passes the parameter ordinal read from syntax. With it the same line binds correctly **(verified)**. A matcher stored in a local has no position and falls back to the run-time rule **(verified)**.

| Feature | Status under Native AOT |
| --- | --- |
| Generic methods, closed generic interfaces | Works. Type arguments travel as `typeof(T)`. **(verified)** |
| `ref` and `out` parameters | Works. Values are written back from the argument array. `Arg` returns `ref T`, so `out Arg.Any<int>()` compiles. **(verified)** |
| `Task<T>`, `ValueTask<T>`, `Task` | Works, for setup and auto-values. **(verified)** |
| Properties and indexers | Works. An assigned value becomes the getter's response. **(verified)** |
| Events | Works with `calc.Changed += Raise.Event<EventHandler<int>>(calc, 5)`. The generated accessor invokes a typed delegate field. **(verified)** |
| Default interface members | Mocked like any member. The default body does not run. **(verified)** |
| Classes with virtual members | Works for public virtual members and a parameterless constructor. Non-virtual members run real code. **(verified)** |
| Static abstract members | Ruled out. The compiler rejects the type argument with CS8920 and the generator reports GM002. **(verified)** |
| Sealed classes, structs | Ruled out with GM002. **(verified)** |
| `ref struct` parameters | Ruled out in the spike. The member throws and the build warns GM001. **(verified)** |

Not covered: recursive mocks, `ReturnsForAnyArgs`, `Received.InOrder`, strict mode, `init` accessors, delegates, and constructor arguments for classes. Support interfaces only in the first release.

One hazard carries over from NSubstitute. `"abc".Length.Returns(3)` after a mock call rebinds that earlier call without an error **(verified)**. Only an analyzer can catch it.

## 5. Prior art

| Library | Finds types by | Setup syntax | Package |
| --- | --- | --- | --- |
| Rocks | `[assembly: Rock(typeof(T), BuildType.Create)]` | Generated `TCreateExpectations` class, strict | One package: library plus analyzers |
| Mockolate | Syntax scan for the creation call | `IFoo.CreateMock()`, then `.Mock.Setup` and `.Mock.Verify` | One package, no dependencies, needs the .NET 10 SDK |
| Imposter | `[assembly: GenerateImposter(typeof(T))]` | `ICalculator.Imposter()`, generated setup members | One package, generator built per Roslyn version |
| TUnit.Mocks | Syntax scan for `Mock.Of<T>()` | `Mock<T>` with generated members | Own package, plus `.Http`, `.Logging`, `.Assertions` |
| NSubstitute | Run time, Castle DynamicProxy | Thread-local last call and matcher queue | Depends on Castle.Core |
| Moq | Run time, Castle DynamicProxy | Expression trees | Depends on Castle.Core |

The first four rows come from reading each project's source and package page **(verified)**. The NSubstitute and Moq rows are **inference** from prior knowledge.

Copy:

- Discovery from the usage site, as Mockolate and TUnit.Mocks do. No assembly attribute per type.
- A module-initializer registry, as TUnit.Mocks does.
- Analyzers and code fixes in the same package. Mockolate also ships fixers that migrate Moq and NSubstitute tests.
- From DependencyModules, reuse `InterceptedMemberReader` and the refusal model that carries diagnostics to the output stage.

Avoid:

- TUnit.Mocks falls back to `MakeGenericType` for open generics. That is reflection.
- Do not build on the DependencyModules `IInterceptor` runtime. It is typed per member for wrapping a real implementation, and its package depends on `Microsoft.Extensions.DependencyInjection.Abstractions`. Matching needs a uniform argument list.
- Rocks, Mockolate and Imposter keep no thread-static state and use no interceptors **(verified by search)**. They get that from a generated setup surface per type. The NSubstitute syntax is what requires ambient state. That is the main trade-off in the author's design.

## 6. Use cases unlocked

- NSubstitute-style tests that run under Native AOT and trimming **(verified)**.
- `[Mock] IFoo foo` in VerificationGlue, with the same instance in the container **(inference)**.
- AOT mocks for existing DependencyModules tests through one `IMockSupportAttribute` **(inference)**.
- The same mocks in xunit v3, NUnit, MSTest and TUnit **(verified on the JIT)**.
- Build-time diagnostics for types and members that cannot be mocked **(verified)**.
- Internal interfaces need `InternalsVisibleTo` for the test assembly only, not for a proxy assembly **(inference)**.

## 7. Risks and open questions

**Generated size and build time (verified, single runs on one machine).**

| Input | Generator | Generated source | Added IL |
| --- | --- | --- | --- |
| 1 interface, 20 members | 2 ms | 183 lines | 4 KB |
| 1 interface, 500 members | 40 ms | 3,543 lines | 76 KB |
| 100 interfaces, 20 members | 78 ms | 15,528 lines | 334 KB |
| 500 interfaces, 20 members | 797 ms | 77,528 lines | 1,689 KB |

After an unrelated edit the 500-interface case still took 278 ms, because every transform reruns and rebuilds its model. Compiling the generated code costs more than generating it.

**Analyzers needed.** The spike has none of these:

- `Returns` or `Throws` on an expression that is not a mock call.
- A matcher outside a mock call, or stored in a local.
- Setup on a non-virtual member of a class mock.
- `Mock.Of<T>()` for a type that is not discovered anywhere.

**Diagnostics.** The spike reports GM001 and GM002 without a location. The model should carry the file path and span as plain values.

**Other risks.**

- Every mock call allocates an array and boxes value arguments **(verified in the generated code)**.
- The interceptor file is regenerated on each edit to a test file that uses `Arg` **(verified)**. The IDE cost on large files is unknown.
- `GetInterceptableLocation` needs a newer Roslyn than the 4.10 that DependencyModules references **(inference)**.
- The thread-static context assumes the call and `Returns` run on one thread in one statement. An unused matcher stays queued until the next mock call on that thread **(inference)**.

**Open questions.**

1. Keep only the NSubstitute syntax, or also generate a typed setup surface per interface that needs no ambient state?
2. Should a class mock call the base member by default, or return an auto-value as the spike does?
3. Should `ref struct` parameters get a typed path, as Rocks and Mockolate have?
4. Should the first release include recursive mocks?
