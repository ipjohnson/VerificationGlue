# Research spikes

These are the research spikes behind `docs/research/`. They are not product code. Nothing in `src/` may reference them, they are not packaged or tested in CI, and they are kept unchanged as evidence. The names in them (`VerifyGlue`, `Glue.Mocks`, `VerificationGlue` in the runner spike) are placeholders.

Each spike was copied from its temporary directory without `bin/` and `obj/`. The `generated/` and `Generated/` folders hold the generator output from the spike's own run. The `#line` paths in them point at the original temporary directory. Each spike pins the .NET SDK 10.0.403 in its `global.json`, or builds on .NET 10 when it has none.

Some projects fail to build on purpose. `assertions/Dup` has a type error inside a `[Verify]` method. `assertions/Name` shows error CS0119. `mocks/Scratch` triggers GM002.

## `assertions/`

The assertion rewrite spike from `docs/research/design-report.md` section 3. `Gen/VerifyGenerator.cs` clones each `[Verify]` method with every `Verify(...)` statement rewritten, and `Lib/VerifyGlue.cs` is the runtime.

It proved:

- The clone reports the value of every sub-expression. It calls each operand once and prints `<not evaluated>` for a short-circuited right side (`App`).
- `out var` and `await` inside `Verify(...)` work, and `#line` keeps stack traces on the original line (`App`).
- A Native AOT publish of `App` has no warnings and is 1.35 MB.
- Expression trees cannot compile ten C# forms, such as `is` patterns and `?.` (`Expr`).
- An interpolated message's holes run only on failure (`Msg`).
- An error inside a `[Verify]` body is reported twice (`Dup`).
- `Verify.Throws<T>` cannot coexist with `using static` `Verify(...)` (`Name`).
- About 2000 `[Verify]` methods with 6000 calls add about 4 s to a build, roughly 0.7 ms per call (`Perf`).

## `closure/`

The spike behind `Verify(() => ...)`, ipjohnson/VerificationGlue#118. `Gen/ClosureGenerator.cs` intercepts each `Verify(() => ...)` call and writes a copy of the lambda's body that reads the captured variables from the delegate's target. `Lib/Checks.cs` is the runtime. Build with `-p:ClosureGlueMode=rerun` for the first design, which runs the delegate and then the copy on failure. The default runs the copy once in place of the delegate.

It proved:

- Captured locals, parameters, `this`, loop scopes, async methods, primary constructor parameters and private members can be read back on SDK 10.0.403 (`App`, 27 cases).
- A Native AOT publish keeps the closure fields when each copy carries `[DynamicDependency(All, typeof(ContainingType))]`, and its output matches the JIT run. Without it, every read fails and the call falls back (`App`).
- It works in a TUnit test body under JIT and Native AOT (`TUnitApp`).
- Running the copy once fixes the second run of a side effect, such as `queue.Dequeue()` (`App`).
- With one captured `int`, a passing call cost 8 to 24 ns and 24 bytes, against 2 to 3 ns for the rerun design and about 1 ns for `Verify(bool)` (`App --perf`).
- A cached `FieldInfo.GetValue` costs 14 to 27 ns. An `[UnsafeAccessor]` with `[UnsafeAccessorType]` costs 0.35 ns, but it needs the compiler's display class name, which the generator cannot know (`Bench`).

## `ide/`

The Phase 0 IDE spike from `docs/research/ide-report.md`. One source set in `src/` builds against MTP 2.5.3 (`Mtp2`) and MTP 3.0.0-preview.26566.9 (`Mtp3`). `src/Generated` holds hand-written tier 3 copies with `#line` spans, test registration and a mock. `PdbCheck` prints the sequence points of the copies, and `tools/server_probe.py` drives the test host in server mode. Unlike the other spikes, its `global.json` sets `rollForward` to `disable`.

It proved:

- Discovery, UID-filtered runs and server-mode navigation data work on both MTP versions with no source change.
- A Native AOT publish has no trim or AOT warnings on both versions.
- Every visible sequence point of a copy maps to its original statement, and a passing `Verify` line takes one step.

The IDE results are recorded in the report.

## `mocks/`

The mock spike from `docs/research/mocking-report.md`. `src/Glue.Mocks` is the 633-line runtime and `src/Glue.Mocks.Generator` is the 887-line generator.

It proved:

- A generated mock per interface supports `Returns`, `Arg.Any<T>()`, `Received()`, `Throws`, `When(...).Do(...)`, properties, indexers and events. A Native AOT publish of `Demo` has no trim or AOT warnings, is 1.9 MB and passes 28 of 28 checks.
- A generated module initializer that fills `MockFactory<T>` creates mocks without reflection or interceptors.
- An interceptor per `Arg` call site binds `calc.Add(0, Arg.Any<int>())` to the right argument.
- `[Mock]` on a parameter needs a hook of 7 to 12 lines per framework. It works with xunit v3, NUnit, MSTest and TUnit on the JIT (`Fx.*`).
- Interfaces from a compiled assembly can be mocked (`DllConsumer`, with `DllLib` as the source of `libs/Vendor.Contracts.dll`).
- A model that compares too little leaves a stale mock and CS0535 in a long-lived generator driver (`GenTests`).
- Unmockable types are reported as GM001 and GM002 (`Scratch`).

## `runner/`

The runner spike from `docs/research/design-report.md` section 2. `Core.cs` is a hand-written runtime and `Generated.cs` is a hand-written version of the code the test generator would emit for `UserTests.cs`.

It proved that the generated shape builds against Microsoft.Testing.Platform 2.5.3 alone, and that `--list-tests` and `--filter-uid` work. A Native AOT publish has no trim warnings, is 10.9 MB and runs 2 tests in 32 ms. The north star treats this MTP 2.5.3 result as historical evidence only.

## `aspnet/`

The app testing spike from `docs/research/aspnet-report.md`. `ShopApi.Tests/WebApp.cs` is an `ITestContainerBootstrap` that starts an ASP.NET Core app for a test, a class or the assembly. Unlike the other spikes, it references the projects under `src/`, so it uses the real names. It ran against commit 670d373.

Run `dotnet run` in `ShopApi.Tests` for the JIT results. Set `WEBAPP_WATCH_CONFIG=1` to leave the configuration watcher on, and pass `-p:ShowFailure=true` to add the test that fails on purpose. Run `dotnet publish -c Release -o bin/aot` in `ShopApi.Aot.Tests` for the Native AOT binary.

It proved:

- An unchanged top-level `Program.cs` starts inside the test process with no reflection. The bootstrap calls the compiler-written entry point through `UnsafeAccessor` and takes the host from the hosting `DiagnosticListener`.
- A `[Mock]` parameter reaches a minimal API endpoint and an MVC controller, and the app's log reaches the test's output.
- 46 tests with 45 app starts run in about 400 ms, with a median start of 3.1 ms. With the configuration watcher on, the median start is 64 ms or more.
- The app runs on TestServer by default and on a free Kestrel port with `Kestrel = true`.
- A Native AOT publish of `ShopApi.Aot.Tests` has no warnings, is 15.8 MB and runs 3 tests in 14 to 29 ms.
- A controller returns 404 unless the bootstrap passes the app's assembly name as `--applicationName`.

## `aspnet/Shapes/`

The entry point shapes spike from the "Entry point shapes", "An app that stops before it builds a host" and "Apps that start at once" sections of `docs/research/aspnet-report.md`. `Apps/` holds one app for each shape: `VoidApp`, `IntApp`, `TaskApp`, `TaskIntApp`, `MainApp`, `InternalApp`, `InternalIvtApp` and `StartupApp`, plus `ThrowApp` and `ReturnApp`, which stop before they build a host. `EntryGenerator` writes the entry type for each app from a `[WebAppEntry]` class in `Shapes.Tests`. `SymbolProbe` prints what a referencing compilation sees of each app's entry point. `ConcurrentBuild` builds hosts on many threads with no test bootstrap. Like `aspnet/`, it references the projects under `src/`.

Run `dotnet run --project Shapes.Tests` and `dotnet run --project SymbolProbe` in `aspnet/Shapes` for the JIT results. Pass `-p:ShowFailure=true` to add the two tests that fail on purpose, and `-p:ShowCompileError=true` to add the hand-written entry type that fails with CS0122. Run `dotnet publish Shapes.Aot.Tests -c Release -o <dir>` for the Native AOT binary, and set `WEBAPP_SERIAL_BUILD=1` when you run it to build one host at a time. Run `ConcurrentBuild <threads> slim|startup|mixed` after a publish to build hosts at once.

It proved:

- The bootstrap starts top-level statements that return `void`, `int`, `Task` and `Task<int>`, an explicit `Main` in a static class, an internal `Program` with and without `InternalsVisibleTo`, and a `Startup` app built with `Host.CreateDefaultBuilder`. Each shape's test passes on the JIT and under Native AOT. Under Native AOT the `Startup` app's test passes only when hosts build one at a time.
- A generator in the test project can read every shape after it sets `MetadataImportOptions.All`. In a build it sees only the PE entry point, which is the compiler's `<Main>` for an async shape. The generated accessor names the app's type as a string and needs no `InternalsVisibleTo`.
- An accessor for a method that does not exist throws `MissingMethodException` on the JIT and under Native AOT.
- An app that throws before it builds a host fails the test with the app's exception. An app that returns first fails it with the exit code.
- 16 apps that start at once on 16 threads each get their own host and their own mock.
- Under Native AOT, hosts with a `Startup` class that build at once can fail with a `NullReferenceException` in `ActivatorUtilities.CreateInstance`, with no test bootstrap involved. A Native AOT publish of `ConcurrentBuild` fails in 3 of 100 runs with `Startup` hosts only, in 14 of 100 with mixed hosts, and in 0 of 100 with `CreateSlimBuilder` hosts.
